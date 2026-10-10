using Microsoft.Extensions.Logging.Abstractions;
using PianoMapper.Music;
using PianoMapper.Scores;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.IntegrationTests;

/// <summary>
/// Pins what saved scores do against a real PostgreSQL today, so moving them onto the shared persistence module
/// cannot change it unnoticed.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SavedScoreRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private string connectionString = null!;
    private PostgresDatabase database = null!;
    private SavedScoreRepository repository = null!;

    public async Task InitializeAsync()
    {
        connectionString = await postgres.CreateDatabaseAsync();
        database = new PostgresDatabase(connectionString, NullLogger<PostgresDatabase>.Instance);
        repository = new SavedScoreRepository(database);
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task ListAsync_FirstUseOfAnEmptyDatabase_PreparesTheSchemaAndReturnsNoScores()
    {
        SavedScorePage page = await repository.ListAsync(1, 10, null, CancellationToken.None);

        Assert.Empty(page.Scores);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task FindAsync_ANewServerInstanceOnAPopulatedDatabase_StillSeesEveryRowUnchanged()
    {
        SavedScoreDetails created = await repository.CreateAsync(SavedScoreSamples.Create("Keep me"), CancellationToken.None);

        await using var restarted = new PostgresDatabase(connectionString, NullLogger<PostgresDatabase>.Instance);
        var afterRestart = new SavedScoreRepository(restarted);

        SavedScoreDetails? found = await afterRestart.FindAsync(created.Id, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal("Keep me", found.Score.Title);
        Assert.Equal(created.CreatedAt, found.CreatedAt);
        Assert.Equal(created.UpdatedAt, found.UpdatedAt);
        Assert.Equal(1, (await afterRestart.ListAsync(1, 10, null, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task CreateAsync_ValidScore_StoresItUnderANewIdAndReadsItBack()
    {
        Score score = SavedScoreSamples.Create("Fur Elise", measureCount: 3);

        SavedScoreDetails created = await repository.CreateAsync(score, CancellationToken.None);
        SavedScoreDetails? found = await repository.FindAsync(created.Id, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Fur Elise", found.Score.Title);
        Assert.Equal(3, found.Score.Measures.Count);
        Assert.Equal(score.Measures[0].Notes, found.Score.Measures[0].Notes);
        Assert.Equal(created.CreatedAt, found.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_BlankTitle_ThrowsAndStoresNothing()
    {

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => repository.CreateAsync(SavedScoreSamples.Create("   "), CancellationToken.None));

        Assert.Equal(0, (await repository.ListAsync(1, 10, null, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {

        Assert.Null(await repository.FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_SeveralScores_ReturnsTheMostRecentlyUpdatedFirstWithTheFullCount()
    {
        SavedScoreDetails first = await repository.CreateAsync(SavedScoreSamples.Create("First", 1), CancellationToken.None);
        SavedScoreDetails second = await repository.CreateAsync(SavedScoreSamples.Create("Second", 2), CancellationToken.None);
        SavedScoreDetails third = await repository.CreateAsync(SavedScoreSamples.Create("Third", 3), CancellationToken.None);
        await repository.UpdateAsync(first.Id, SavedScoreSamples.Create("First, edited", 1), CancellationToken.None);

        SavedScorePage page = await repository.ListAsync(1, 10, null, CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([first.Id, third.Id, second.Id], page.Scores.Select(score => score.Id));
        Assert.Equal(["First, edited", "Third", "Second"], page.Scores.Select(score => score.Title));
        Assert.Equal([1, 3, 2], page.Scores.Select(score => score.MeasureCount));
    }

    [Fact]
    public async Task ListAsync_SecondPage_ReturnsTheRemainingScoresAndTheTotalAcrossPages()
    {
        for (int index = 1; index <= 5; index++)
        {
            await repository.CreateAsync(SavedScoreSamples.Create($"Score {index}"), CancellationToken.None);
        }

        SavedScorePage page = await repository.ListAsync(3, 2, null, CancellationToken.None);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["Score 1"], page.Scores.Select(score => score.Title));
    }

    [Fact]
    public async Task ListAsync_TitleFilter_MatchesAnywhereIgnoringCaseAndSurroundingSpaces()
    {
        await repository.CreateAsync(SavedScoreSamples.Create("Moonlight Sonata"), CancellationToken.None);
        await repository.CreateAsync(SavedScoreSamples.Create("Sonatina in C"), CancellationToken.None);
        await repository.CreateAsync(SavedScoreSamples.Create("Clair de Lune"), CancellationToken.None);

        SavedScorePage filtered = await repository.ListAsync(1, 10, "  SONATA ", CancellationToken.None);
        SavedScorePage blankFilter = await repository.ListAsync(1, 10, "   ", CancellationToken.None);

        Assert.Equal(["Moonlight Sonata"], filtered.Scores.Select(score => score.Title));
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(3, blankFilter.TotalCount);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    public async Task ListAsync_PageOrPageSizeBelowOne_Throws(int page, int pageSize)
    {

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.ListAsync(page, pageSize, null, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ExistingScore_ReplacesTheDocumentKeepsCreatedAtAndMovesUpdatedAt()
    {
        SavedScoreDetails created = await repository.CreateAsync(SavedScoreSamples.Create("Draft", 1), CancellationToken.None);

        SavedScoreDetails? updated = await repository.UpdateAsync(
            created.Id,
            SavedScoreSamples.Create("Final", 4),
            CancellationToken.None);
        SavedScoreDetails? found = await repository.FindAsync(created.Id, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
        Assert.NotNull(found);
        Assert.Equal("Final", found.Score.Title);
        Assert.Equal(4, found.Score.Measures.Count);
        Assert.Equal(updated.UpdatedAt, found.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNullAndStoresNothing()
    {

        SavedScoreDetails? updated = await repository.UpdateAsync(
            Guid.NewGuid(),
            SavedScoreSamples.Create("Ghost"),
            CancellationToken.None);

        Assert.Null(updated);
        Assert.Equal(0, (await repository.ListAsync(1, 10, null, CancellationToken.None)).TotalCount);
    }

    [Fact]
    public async Task UpdateAsync_BlankTitle_ThrowsAndLeavesTheScoreAsItWas()
    {
        SavedScoreDetails created = await repository.CreateAsync(SavedScoreSamples.Create("Original"), CancellationToken.None);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => repository.UpdateAsync(created.Id, SavedScoreSamples.Create(""), CancellationToken.None));

        Assert.Equal("Original", (await repository.FindAsync(created.Id, CancellationToken.None))!.Score.Title);
    }

    [Fact]
    public async Task DeleteAsync_ExistingScore_RemovesItOnceAndThenReportsNothingToDelete()
    {
        SavedScoreDetails created = await repository.CreateAsync(SavedScoreSamples.Create("Temporary"), CancellationToken.None);

        bool firstDelete = await repository.DeleteAsync(created.Id, CancellationToken.None);
        bool secondDelete = await repository.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(firstDelete);
        Assert.False(secondDelete);
        Assert.Null(await repository.FindAsync(created.Id, CancellationToken.None));
    }
}
