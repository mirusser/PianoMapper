using System.Net;
using System.Text;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ProgressServerClientTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_ServerAnswers_GetsTheNewestHundredAndParsesThem()
    {
        SightReadingSessionSummary newer = CreateSummary(2);
        SightReadingSessionSummary older = CreateSummary(1);
        var handler = new StubHttpMessageHandler(_ => Json(SightReadingSessionSerializer.SerializeAll([newer, older])));
        var client = CreateClient(handler);

        ProgressServerSessions listed = await client.ListAsync();

        Assert.Equal(ProgressServerReach.Reached, listed.Reach);
        Assert.Equal([newer.SessionId, older.SessionId], listed.Sessions.Select(session => session.SessionId));
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal(ProgressApiRoutes.Sessions, handler.RequestUri?.AbsolutePath);
        Assert.Equal("?limit=100", handler.RequestUri?.Query);
    }

    [Fact]
    public async Task ListAsync_AnEntryThisVersionCannotRead_SkipsOnlyThatEntry()
    {
        SightReadingSessionSummary readable = CreateSummary(1);
        string readableJson = SightReadingSessionSerializer.Serialize(readable);
        var client = CreateClient(new StubHttpMessageHandler(
            _ => Json($$"""[{{readableJson}}, {"schemaVersion": 99}, "not a session"]""")));

        ProgressServerSessions listed = await client.ListAsync();

        Assert.Equal(ProgressServerReach.Reached, listed.Reach);
        Assert.Equal([readable.SessionId], listed.Sessions.Select(session => session.SessionId));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task ListAsync_StaticHostWithoutRoutes_ReportsNoServer(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));

        ProgressServerSessions listed = await client.ListAsync();

        Assert.Equal(ProgressServerReach.NoServer, listed.Reach);
        Assert.Empty(listed.Sessions);
    }

    [Fact]
    public async Task ListAsync_StaticHostAnswersWithItsIndexPage_ReportsNoServer()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!DOCTYPE html><html></html>", Encoding.UTF8, "text/html"),
        }));

        Assert.Equal(ProgressServerReach.NoServer, (await client.ListAsync()).Reach);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ListAsync_ServerFailing_ReportsUnreachable(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));

        ProgressServerSessions listed = await client.ListAsync();

        Assert.Equal(ProgressServerReach.Unreachable, listed.Reach);
        Assert.Empty(listed.Sessions);
    }

    [Fact]
    public async Task ListAsync_NetworkFailure_ReportsUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("Failed to fetch")));

        Assert.Equal(ProgressServerReach.Unreachable, (await client.ListAsync()).Reach);
    }

    [Fact]
    public async Task ListAsync_RequestTimesOut_ReportsUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException("The request timed out.")));

        Assert.Equal(ProgressServerReach.Unreachable, (await client.ListAsync()).Reach);
    }

    [Fact]
    public async Task ListAsync_CallerCancels_ThrowsInsteadOfReportingUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => Json("[]")));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListAsync(cancellation.Token));
    }

    [Fact]
    public async Task UploadAsync_Sessions_PostsThemAsOneJsonArray()
    {
        SightReadingSessionSummary[] sessions = [CreateSummary(2), CreateSummary(1)];
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        ProgressServerReach reach = await client.UploadAsync(sessions);

        Assert.Equal(ProgressServerReach.Reached, reach);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal(ProgressApiRoutes.Sessions, handler.RequestUri?.AbsolutePath);
        Assert.Equal("application/json", handler.RequestContentType);
        Assert.Equal(SightReadingSessionSerializer.SerializeAll(sessions), handler.RequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task UploadAsync_StaticHostWithoutRoutes_ReportsNoServer(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));

        Assert.Equal(ProgressServerReach.NoServer, await client.UploadAsync([CreateSummary(1)]));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task UploadAsync_ServerRefuses_ReportsUnreachable(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));

        Assert.Equal(ProgressServerReach.Unreachable, await client.UploadAsync([CreateSummary(1)]));
    }

    [Fact]
    public async Task UploadAsync_NetworkFailure_ReportsUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("Failed to fetch")));

        Assert.Equal(ProgressServerReach.Unreachable, await client.UploadAsync([CreateSummary(1)]));
    }

    [Fact]
    public async Task ClearAsync_Server_DeletesTheSessionsRoute()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        ProgressServerReach reach = await client.ClearAsync();

        Assert.Equal(ProgressServerReach.Reached, reach);
        Assert.Equal(HttpMethod.Delete, handler.RequestMethod);
        Assert.Equal(ProgressApiRoutes.Sessions, handler.RequestUri?.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task ClearAsync_StaticHostWithoutRoutes_ReportsNoServer(HttpStatusCode status)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));

        Assert.Equal(ProgressServerReach.NoServer, await client.ClearAsync());
    }

    [Fact]
    public async Task ClearAsync_ServerUnavailable_ReportsUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        Assert.Equal(ProgressServerReach.Unreachable, await client.ClearAsync());
    }

    [Fact]
    public async Task ClearAsync_NetworkFailure_ReportsUnreachable()
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("Failed to fetch")));

        Assert.Equal(ProgressServerReach.Unreachable, await client.ClearAsync());
    }

    private static ProgressServerClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static SightReadingSessionSummary CreateSummary(int minutesAfterStart) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Guid.NewGuid(),
            Start.AddMinutes(minutesAfterStart),
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(20),
            6,
            2,
            []);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        internal HttpMethod? RequestMethod { get; private set; }

        internal Uri? RequestUri { get; private set; }

        internal string? RequestContentType { get; private set; }

        internal string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
