using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PianoMapper.Practice;
using PianoMapper.Scores;
using Testcontainers.PostgreSql;

namespace PianoMapper.Tests.IntegrationTests;

/// <summary>
/// The server with its database down: it still boots and serves everything that needs no database, the database routes
/// answer 503 (not 500), and once the database is up they work without restarting the server.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DatabaseOutageTests : IAsyncLifetime
{
    private const string Password = "outage-test-secret-password";

    private readonly int port = GetFreePort();
    private PianoMapperServerFactory factory = null!;
    private HttpClient client = null!;
    private PostgreSqlContainer? database;

    public Task InitializeAsync()
    {
        factory = new PianoMapperServerFactory(
            $"Host=127.0.0.1;Port={port};Database=pianomapper;Username=pianomapper;Password={Password};Timeout=3");
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Host_DatabaseUnreachable_StillStartsAndServesRoutesThatNeedNoDatabase()
    {
        // The score-image route answers its own validation error before touching Audiveris or the database.
        using HttpResponseMessage response = await client.PostAsync(
            "/api/score-images/convert",
            new ByteArrayContent([1, 2, 3]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Host_DatabaseUnreachable_StillServesTheApp()
    {
        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.webassembly.js", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DatabaseRoutes_DatabaseUnreachable_Return503AsAProblemNot500()
    {
        using HttpResponseMessage listScores = await client.GetAsync(SavedScoreApiRoutes.List(1, 10, null));
        using HttpResponseMessage createScore = await client.PostAsJsonAsync(
            SavedScoreApiRoutes.Collection,
            SavedScoreSamples.Create("Offline"));
        using HttpResponseMessage listSessions = await client.GetAsync(ProgressApiRoutes.Sessions);
        using HttpResponseMessage saveSessions = await client.PostAsync(
            ProgressApiRoutes.Sessions,
            new StringContent(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v3.json")),
                Encoding.UTF8,
                "application/json"));
        using HttpResponseMessage clearSessions = await client.DeleteAsync(ProgressApiRoutes.Sessions);

        foreach (HttpResponseMessage response in new[] { listScores, createScore, listSessions, saveSessions, clearSessions })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(503, (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
        }
    }

    [Fact]
    public async Task SavedScoreRoutes_DatabaseUnreachable_LogTheFailureWithoutTheConnectionString()
    {
        using HttpResponseMessage response = await client.GetAsync(SavedScoreApiRoutes.List(1, 10, null));
        string responseBody = await response.Content.ReadAsStringAsync();

        var failures = factory.Logs.Entries.Where(entry => entry.Level >= LogLevel.Warning && entry.Exception is not null).ToArray();
        Assert.NotEmpty(failures);
        Assert.All(failures, entry => Assert.DoesNotContain(Password, entry.FullText));
        Assert.DoesNotContain(Password, responseBody);
        Assert.DoesNotContain("pianomapper;", responseBody);
    }

    [Fact]
    public async Task SavedScoreRoutes_DatabaseComesUpLater_WorkWithoutRestartingTheServer()
    {
        using (HttpResponseMessage down = await client.GetAsync(SavedScoreApiRoutes.List(1, 10, null)))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
        }

        database = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("pianomapper")
            .WithUsername("pianomapper")
            .WithPassword(Password)
            .WithPortBinding(port, 5432)
            .Build();
        await database.StartAsync();

        using HttpResponseMessage up = await client.GetAsync(SavedScoreApiRoutes.List(1, 10, null));
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        SavedScorePage? page = await up.Content.ReadFromJsonAsync<SavedScorePage>();
        Assert.Equal(0, page?.TotalCount);
        using HttpResponseMessage sessions = await client.GetAsync(ProgressApiRoutes.Sessions);
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
