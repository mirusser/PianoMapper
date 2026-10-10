using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PianoMapper.Music;
using PianoMapper.Scores;
using PianoMapper.Web.Scores;

namespace PianoMapper.Tests.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SavedScoreEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private PianoMapperServerFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        factory = new PianoMapperServerFactory(await postgres.CreateDatabaseAsync());
        client = factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task SavedScores_ThroughTheRealClient_CreateFindListUpdateAndDelete()
    {
        var scores = new SavedScoreClient(client);

        SavedScoreDetails created = await scores.CreateAsync(SavedScoreSamples.Create("Prelude", 2));
        SavedScoreDetails found = await scores.FindAsync(created.Id);
        SavedScorePage listed = await scores.ListAsync(1, "prel");
        SavedScoreDetails updated = await scores.UpdateAsync(created.Id, SavedScoreSamples.Create("Prelude in C", 3));
        await scores.DeleteAsync(created.Id);

        Assert.Equal("Prelude", found.Score.Title);
        Assert.Equal(2, found.Score.Measures.Count);
        Assert.Equal(created.Id, Assert.Single(listed.Scores).Id);
        Assert.Equal("Prelude in C", updated.Score.Title);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(SavedScoreApiRoutes.ById(created.Id))).StatusCode);
    }

    [Fact]
    public async Task PostScore_BlankTitle_Returns400AsAProblem()
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            SavedScoreApiRoutes.Collection,
            SavedScoreSamples.Create("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(400, problem?.Status);
    }

    [Fact]
    public async Task PutScore_BlankTitle_Returns400AndLeavesTheScoreAlone()
    {
        var scores = new SavedScoreClient(client);
        SavedScoreDetails created = await scores.CreateAsync(SavedScoreSamples.Create("Original"));

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            SavedScoreApiRoutes.ById(created.Id),
            SavedScoreSamples.Create(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Original", (await scores.FindAsync(created.Id)).Score.Title);
    }

    [Fact]
    public async Task GetScore_UnknownId_Returns404()
    {
        using HttpResponseMessage response = await client.GetAsync(SavedScoreApiRoutes.ById(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetScores_InvalidPage_Returns400()
    {
        using HttpResponseMessage response = await client.GetAsync(SavedScoreApiRoutes.List(0, 10, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
