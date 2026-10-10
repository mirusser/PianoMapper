using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PianoMapper.Practice;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ProgressSessionRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid Learner = LearnerScope.DefaultLearnerId;
    private static readonly Guid AnotherLearner = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000002");

    private PostgresDatabase database = null!;
    private ProgressSessionRepository repository = null!;

    public async Task InitializeAsync()
    {
        database = new PostgresDatabase(await postgres.CreateDatabaseAsync(), NullLogger<PostgresDatabase>.Instance);
        repository = new ProgressSessionRepository(database);
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task SaveAsync_NewSessions_StoresThemAndReportsHowManyWereNew()
    {
        SightReadingSessionSummary[] sessions = [Session(1), Session(2), Session(3)];

        int stored = await repository.SaveAsync(Learner, sessions, CancellationToken.None);

        Assert.Equal(3, stored);
        Assert.Equal(
            sessions.Select(session => session.SessionId).Order(),
            (await repository.ListNewestAsync(Learner, 10, CancellationToken.None))
                .Select(session => session.SessionId)
                .Order());
    }

    [Fact]
    public async Task SaveAsync_SameSessionIdAgain_ChangesNothing()
    {
        SightReadingSessionSummary original = Session(1);
        await repository.SaveAsync(Learner, [original], CancellationToken.None);

        int stored = await repository.SaveAsync(
            Learner,
            [original with { PromptCount = 99, PresetId = "Changed" }],
            CancellationToken.None);

        Assert.Equal(0, stored);
        SightReadingSessionSummary kept = Assert.Single(
            await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
        Assert.Equal(original.PromptCount, kept.PromptCount);
        Assert.Equal(original.PresetId, kept.PresetId);
    }

    [Fact]
    public async Task SaveAsync_BatchWithSomeSessionsAlreadyStored_AddsOnlyTheNewOnes()
    {
        await repository.SaveAsync(Learner, [Session(1), Session(2)], CancellationToken.None);

        int stored = await repository.SaveAsync(Learner, [Session(2), Session(3)], CancellationToken.None);

        Assert.Equal(1, stored);
        Assert.Equal(3, (await repository.ListNewestAsync(Learner, 10, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task SaveAsync_NoSessions_StoresNothing()
    {
        Assert.Equal(0, await repository.SaveAsync(Learner, [], CancellationToken.None));
    }

    [Fact]
    public async Task ListNewestAsync_SessionsSavedOutOfOrder_ReturnsNewestFirstAndRespectsTheLimit()
    {
        await repository.SaveAsync(Learner, [Session(3), Session(1), Session(5), Session(2), Session(4)], CancellationToken.None);

        IReadOnlyList<SightReadingSessionSummary> newest =
            await repository.ListNewestAsync(Learner, 3, CancellationToken.None);

        Assert.Equal([Session(5).CompletedAt, Session(4).CompletedAt, Session(3).CompletedAt], newest.Select(session => session.CompletedAt));
    }

    [Fact]
    public async Task ListNewestAsync_NothingStored_ReturnsAnEmptyList()
    {
        Assert.Empty(await repository.ListNewestAsync(Learner, 100, CancellationToken.None));
    }

    [Fact]
    public async Task ListNewestAsync_LimitBelowOne_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListNewestAsync(Learner, 0, CancellationToken.None));
    }

    [Fact]
    public async Task ListNewestAsync_SessionsOfTheCurrentSchema_ComeBackInExactlyTheStoredShape()
    {
        string fixture = (await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v3.json"))).Trim();
        SightReadingSessionSummary[] sessions = ParseAll(fixture);

        await repository.SaveAsync(Learner, sessions, CancellationToken.None);
        IReadOnlyList<SightReadingSessionSummary> listed =
            await repository.ListNewestAsync(Learner, 10, CancellationToken.None);

        Assert.Equal(fixture, SightReadingSessionSerializer.SerializeAll(listed));
    }

    [Theory]
    [InlineData("sight-reading-history-v1.json")]
    [InlineData("sight-reading-history-v2.json")]
    public async Task SaveAsync_SessionsFromOlderSchemas_KeepTheirVersionAndDerivedIdentity(string fixture)
    {
        SightReadingSessionSummary[] sessions = ParseAll(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)));

        await repository.SaveAsync(Learner, sessions, CancellationToken.None);
        int storedAgain = await repository.SaveAsync(Learner, sessions, CancellationToken.None);
        IReadOnlyList<SightReadingSessionSummary> listed =
            await repository.ListNewestAsync(Learner, 10, CancellationToken.None);

        Assert.Equal(0, storedAgain);
        Assert.Equal(
            sessions.Select(session => (session.SchemaVersion, session.SessionId)),
            listed.Select(session => (session.SchemaVersion, session.SessionId)));
    }

    [Fact]
    public async Task SaveAsync_CompletionTimeWithAnotherUtcOffset_StoresTheSameInstant()
    {
        SightReadingSessionSummary session = Session(1) with
        {
            CompletedAt = new DateTimeOffset(2026, 10, 9, 20, 30, 15, 250, TimeSpan.FromHours(2)),
        };

        await repository.SaveAsync(Learner, [session], CancellationToken.None);

        SightReadingSessionSummary listed = Assert.Single(
            await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
        Assert.Equal(session.CompletedAt.ToUniversalTime(), listed.CompletedAt.ToUniversalTime());
    }

    [Fact]
    public async Task SaveAsync_UnsupportedSchemaVersion_RejectsTheWholeBatchAndStoresNothing()
    {
        SightReadingSessionSummary[] batch = [Session(1), Session(2) with { SchemaVersion = 999 }];

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Learner, batch, CancellationToken.None));

        Assert.Empty(await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_EmptySessionId_RejectsTheWholeBatchAndStoresNothing()
    {
        SightReadingSessionSummary[] batch = [Session(1), Session(2) with { SessionId = Guid.Empty }];

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Learner, batch, CancellationToken.None));

        Assert.Empty(await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesOnlyTheGivenLearnersSessions()
    {
        await repository.SaveAsync(Learner, [Session(1), Session(2)], CancellationToken.None);
        await repository.SaveAsync(AnotherLearner, [Session(3)], CancellationToken.None);

        int deleted = await repository.DeleteAllAsync(Learner, CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Empty(await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
        Assert.Equal(
            Session(3).SessionId,
            Assert.Single(await repository.ListNewestAsync(AnotherLearner, 10, CancellationToken.None)).SessionId);
    }

    [Fact]
    public async Task ListNewestAsync_SessionsOfAnotherLearner_AreNotReturned()
    {
        await repository.SaveAsync(AnotherLearner, [Session(1)], CancellationToken.None);

        Assert.Empty(await repository.ListNewestAsync(Learner, 10, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAllAsync_NothingStored_ReportsZero()
    {
        Assert.Equal(0, await repository.DeleteAllAsync(Learner, CancellationToken.None));
    }

    [Fact]
    public async Task ListNewestAsync_AStoredDocumentThisVersionCannotRead_IsSkippedNotFatal()
    {
        await repository.SaveAsync(Learner, [Session(1), Session(2)], CancellationToken.None);
        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);
        await using NpgsqlCommand insertFromTheFuture = dataSource.CreateCommand(
            "INSERT INTO progress_sessions (session_id, learner_id, completed_at, document_version, session_document) " +
            "VALUES (@id, @learner, '2026-10-09T19:00:00Z', 4, '{\"schemaVersion\":4}')");
        insertFromTheFuture.Parameters.AddWithValue("id", Guid.NewGuid());
        insertFromTheFuture.Parameters.AddWithValue("learner", Learner);
        await insertFromTheFuture.ExecuteNonQueryAsync();

        IReadOnlyList<SightReadingSessionSummary> listed =
            await repository.ListNewestAsync(Learner, 10, CancellationToken.None);

        Assert.Equal([Session(2).SessionId, Session(1).SessionId], listed.Select(session => session.SessionId));
    }

    private static SightReadingSessionSummary Session(int minute) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            new Guid(minute, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]),
            new DateTimeOffset(2026, 10, 9, 18, minute, 0, TimeSpan.Zero),
            "FiveNote",
            PianoMapper.Music.Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(20),
            6,
            2,
            []);

    private static SightReadingSessionSummary[] ParseAll(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(element => SightReadingSessionSerializer.TryParse(element)!)];
    }
}
