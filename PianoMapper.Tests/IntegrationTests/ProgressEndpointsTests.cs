using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ProgressEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int TenMebibytes = 10 * 1024 * 1024;

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
    public async Task GetSessions_NothingStored_ReturnsAnEmptyArray()
    {
        using HttpResponseMessage response = await client.GetAsync(ProgressApiRoutes.Sessions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostSessions_ACurrentHistory_ComesBackAsTheSameEntriesNewestFirst()
    {
        string history = ReadFixture("sight-reading-history-v3.json");

        using HttpResponseMessage posted = await PostAsync(history);
        string returned = await GetAsync(ProgressApiRoutes.Sessions);

        Assert.Equal(HttpStatusCode.NoContent, posted.StatusCode);
        Assert.Equal(history, returned);
    }

    [Fact]
    public async Task PostSessions_TheSameHistoryTwice_StoresEachSessionOnce()
    {
        string history = ReadFixture("sight-reading-history-v3.json");

        using HttpResponseMessage first = await PostAsync(history);
        using HttpResponseMessage second = await PostAsync(history);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(2, CountEntries(await GetAsync(ProgressApiRoutes.Sessions)));
    }

    [Theory]
    [InlineData("sight-reading-history-v1.json", 3, 1)]
    [InlineData("sight-reading-history-v2.json", 2, 2)]
    public async Task PostSessions_HistoryFromAnOlderBrowser_IsAcceptedAndKeepsItsSchemaVersion(
        string fixture,
        int expectedCount,
        int expectedSchemaVersion)
    {
        using HttpResponseMessage posted = await PostAsync(ReadFixture(fixture));
        using JsonDocument returned = JsonDocument.Parse(await GetAsync(ProgressApiRoutes.Sessions));

        Assert.Equal(HttpStatusCode.NoContent, posted.StatusCode);
        JsonElement[] entries = [.. returned.RootElement.EnumerateArray()];
        Assert.Equal(expectedCount, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.Equal(expectedSchemaVersion, entry.GetProperty("schemaVersion").GetInt32());
            Assert.NotEqual(Guid.Empty, entry.GetProperty("sessionId").GetGuid());
        });
    }

    [Fact]
    public async Task PostSessions_OldBrowserHistoryUploadedByTwoBrowsers_IsStoredOnce()
    {
        string oldHistory = ReadFixture("sight-reading-history-v1.json");

        using HttpResponseMessage fromFirstBrowser = await PostAsync(oldHistory);
        using HttpResponseMessage fromSecondBrowser = await PostAsync(oldHistory);

        Assert.Equal(HttpStatusCode.NoContent, fromSecondBrowser.StatusCode);
        Assert.Equal(3, CountEntries(await GetAsync(ProgressApiRoutes.Sessions)));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"schemaVersion":3}""")]
    [InlineData("\"text\"")]
    [InlineData("[1, 2]")]
    [InlineData("[{}]")]
    public async Task PostSessions_InvalidBody_Returns400AsAProblemAndStoresNothing(string body)
    {
        using HttpResponseMessage response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("[]", await GetAsync(ProgressApiRoutes.Sessions));
    }

    [Fact]
    public async Task PostSessions_UnsupportedSchemaVersion_Returns400AsAProblemAndStoresNothing()
    {
        string unsupported = ReadFixture("sight-reading-history-v3.json")
            .Replace("\"schemaVersion\":3", "\"schemaVersion\":99", StringComparison.Ordinal);

        using HttpResponseMessage response = await PostAsync(unsupported);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, JsonSerializer.Deserialize<ProblemDetails>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Status);
        Assert.Equal("[]", await GetAsync(ProgressApiRoutes.Sessions));
    }

    [Fact]
    public async Task PostSessions_OneBadEntryAmongGoodOnes_Returns400AndStoresNone()
    {
        using JsonDocument fixture = JsonDocument.Parse(ReadFixture("sight-reading-history-v3.json"));
        string good = fixture.RootElement[0].GetRawText();

        using HttpResponseMessage response = await PostAsync($"[{good},{{\"schemaVersion\":3,\"sessionId\":\"not-a-guid\"}}]");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("[]", await GetAsync(ProgressApiRoutes.Sessions));
    }

    [Fact]
    public async Task PostSessions_AFullHundredWorstCaseSessions_IsAcceptedUnderTheRequestLimit()
    {
        using JsonDocument fixture = JsonDocument.Parse(ReadFixture("sight-reading-history-v3.json"));
        SightReadingSessionSummary template = SightReadingSessionSerializer.TryParse(fixture.RootElement[0])!;
        SightReadingSessionSummary[] sessions =
        [
            .. Enumerable.Range(0, ProgressApiRoutes.MaximumLimit).Select(index => template with
            {
                SessionId = Guid.NewGuid(),
                CompletedAt = template.CompletedAt.AddMinutes(index),
                PitchAttempts =
                [
                    .. Enumerable.Range(0, 60).Select(midi => new PitchAttemptSummary(PitchFromMidi(24 + midi), 1, 2)),
                ],
                NoteAttempts =
                [
                    .. Enumerable.Range(0, 60).Select(midi => new NoteAttemptSummary(
                        PitchFromMidi(24 + midi),
                        midi % 2 == 0 ? Staff.Bass : Staff.Treble,
                        2,
                        1,
                        2,
                        3456)),
                ],
                Confusions =
                [
                    .. Enumerable.Range(0, SightReadingSessionSummary.MaximumConfusions).Select(midi => new ConfusionSummary(
                        PitchFromMidi(30 + midi),
                        PitchFromMidi(31 + midi),
                        Staff.Treble,
                        3)),
                ],
            }),
        ];
        string body = SightReadingSessionSerializer.SerializeAll(sessions);

        using HttpResponseMessage response = await PostAsync(body);

        Assert.True(Encoding.UTF8.GetByteCount(body) < TenMebibytes);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ProgressApiRoutes.MaximumLimit, CountEntries(await GetAsync(ProgressApiRoutes.Sessions)));
    }

    [Fact]
    public async Task GetSessions_WithALimit_ReturnsOnlyThatManyOfTheNewest()
    {
        string history = ReadFixture("sight-reading-history-v3.json");
        using HttpResponseMessage posted = await PostAsync(history);
        using JsonDocument expected = JsonDocument.Parse(history);

        using JsonDocument returned = JsonDocument.Parse(await GetAsync(ProgressApiRoutes.List(1)));

        JsonElement only = Assert.Single(returned.RootElement.EnumerateArray());
        Assert.Equal(
            expected.RootElement[0].GetProperty("sessionId").GetGuid(),
            only.GetProperty("sessionId").GetGuid());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("many")]
    public async Task GetSessions_LimitOutOfRange_Returns400(string limit)
    {
        using HttpResponseMessage response = await client.GetAsync($"{ProgressApiRoutes.Sessions}?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSessions_RemovesEverythingAndMayBeRepeated()
    {
        using HttpResponseMessage posted = await PostAsync(ReadFixture("sight-reading-history-v3.json"));

        using HttpResponseMessage first = await client.DeleteAsync(ProgressApiRoutes.Sessions);
        using HttpResponseMessage second = await client.DeleteAsync(ProgressApiRoutes.Sessions);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal("[]", await GetAsync(ProgressApiRoutes.Sessions));
    }

    private Task<HttpResponseMessage> PostAsync(string json) =>
        client.PostAsync(ProgressApiRoutes.Sessions, new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task<string> GetAsync(string route)
    {
        using HttpResponseMessage response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static int CountEntries(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetArrayLength();
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)).Trim();

    private static Pitch PitchFromMidi(int midi)
    {
        int[] semitoneToLetter = [0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6];
        int[] semitoneToAlter = [0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0];
        return new Pitch((NoteLetter)semitoneToLetter[midi % 12], semitoneToAlter[midi % 12], (midi / 12) - 1);
    }
}
