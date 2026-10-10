using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PianoMapper.Server.Persistence;

namespace PianoMapper.Tests.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PostgresDatabaseTests(PostgresFixture postgres)
{
    /// <summary>The DDL the server ran at startup before the persistence module existed, kept verbatim.</summary>
    private const string LegacyStartupSql = """
        CREATE TABLE IF NOT EXISTS scores (
            id uuid PRIMARY KEY,
            title text NOT NULL CONSTRAINT scores_title_ck CHECK (btrim(title) <> ''),
            measure_count integer NOT NULL CONSTRAINT scores_measure_count_ck CHECK (measure_count >= 0),
            score_document jsonb NOT NULL,
            document_version integer NOT NULL CONSTRAINT scores_document_version_ck CHECK (document_version > 0),
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS scores_updated_at_idx ON scores (updated_at DESC, id);
        """;

    [Fact]
    public async Task GetDataSourceAsync_EmptyDatabase_CreatesTheSchemaAndRecordsEveryStep()
    {
        await using PostgresDatabase database = await CreateDatabaseAsync();

        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);

        Assert.True(await ScalarAsync<bool>(dataSource, "SELECT to_regclass('public.scores') IS NOT NULL"));
        Assert.Equal(
            SchemaSteps.All.Select(step => step.Version),
            await ReadAppliedVersionsAsync(dataSource));
    }

    [Fact]
    public async Task GetDataSourceAsync_CalledAgain_DoesNotApplyAnyStepTwice()
    {
        await using PostgresDatabase database = await CreateDatabaseAsync();
        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);
        string firstAppliedAt = await ScalarAsync<string>(
            dataSource,
            "SELECT string_agg(applied_at::text, ',' ORDER BY version) FROM schema_migrations");

        await database.GetDataSourceAsync(CancellationToken.None);

        Assert.Equal(SchemaSteps.All.Count, await ScalarAsync<long>(dataSource, "SELECT count(*) FROM schema_migrations"));
        Assert.Equal(
            firstAppliedAt,
            await ScalarAsync<string>(
                dataSource,
                "SELECT string_agg(applied_at::text, ',' ORDER BY version) FROM schema_migrations"));
    }

    [Fact]
    public async Task GetDataSourceAsync_SecondInstanceOnTheSameDatabase_FindsTheStepsAlreadyApplied()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using PostgresDatabase first = CreateDatabase(connectionString);
        await using PostgresDatabase second = CreateDatabase(connectionString);

        await first.GetDataSourceAsync(CancellationToken.None);
        NpgsqlDataSource dataSource = await second.GetDataSourceAsync(CancellationToken.None);

        Assert.Equal(
            SchemaSteps.All.Select(step => step.Version),
            await ReadAppliedVersionsAsync(dataSource));
    }

    [Fact]
    public async Task GetDataSourceAsync_ManyFirstUsesAtOnce_ApplyEachStepExactlyOnce()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        PostgresDatabase[] instances = [.. Enumerable.Range(0, 8).Select(_ => CreateDatabase(connectionString))];

        try
        {
            NpgsqlDataSource[] dataSources = await Task.WhenAll(instances.Select(instance =>
                instance.GetDataSourceAsync(CancellationToken.None).AsTask()));

            Assert.Equal(
                SchemaSteps.All.Select(step => step.Version),
                await ReadAppliedVersionsAsync(dataSources[0]));
        }
        finally
        {
            foreach (PostgresDatabase instance in instances)
            {
                await instance.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task GetDataSourceAsync_DatabaseCreatedBeforeTheModule_UpgradesInPlaceWithoutChangingTheRows()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        Guid existingId = Guid.NewGuid();
        await using (var legacy = NpgsqlDataSource.Create(connectionString))
        {
            await using NpgsqlCommand create = legacy.CreateCommand(LegacyStartupSql);
            await create.ExecuteNonQueryAsync();
            await using NpgsqlCommand insert = legacy.CreateCommand(
                "INSERT INTO scores (id, title, measure_count, score_document, document_version, created_at, updated_at) " +
                "VALUES (@id, 'Old score', 2, '{\"kept\":true}', 1, '2026-01-02T03:04:05Z', '2026-01-03T03:04:05Z')");
            insert.Parameters.AddWithValue("id", existingId);
            await insert.ExecuteNonQueryAsync();
        }

        await using PostgresDatabase database = CreateDatabase(connectionString);
        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);

        await using NpgsqlCommand read = dataSource.CreateCommand(
            "SELECT title, measure_count, score_document::text, document_version, created_at, updated_at " +
            "FROM scores WHERE id = @id");
        read.Parameters.AddWithValue("id", existingId);
        await using NpgsqlDataReader reader = await read.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Old score", reader.GetString(0));
        Assert.Equal(2, reader.GetInt32(1));
        Assert.Equal("{\"kept\": true}", reader.GetString(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), reader.GetFieldValue<DateTimeOffset>(4));
        Assert.Equal(new DateTimeOffset(2026, 1, 3, 3, 4, 5, TimeSpan.Zero), reader.GetFieldValue<DateTimeOffset>(5));
        await reader.DisposeAsync();
        Assert.Equal(
            SchemaSteps.All.Select(step => step.Version),
            await ReadAppliedVersionsAsync(dataSource));
    }

    [Fact]
    public async Task GetDataSourceAsync_FirstAttemptFails_RetriesOnTheNextUseWithoutARestart()
    {
        string databaseName = $"test_{Guid.NewGuid():N}";
        await using PostgresDatabase database = CreateDatabase(postgres.ConnectionStringFor(databaseName));

        await Assert.ThrowsAsync<PostgresException>(() => database.GetDataSourceAsync(CancellationToken.None).AsTask());

        await postgres.CreateDatabaseNamedAsync(databaseName);
        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);
        Assert.Equal(
            SchemaSteps.All.Select(step => step.Version),
            await ReadAppliedVersionsAsync(dataSource));
    }

    [Fact]
    public async Task GetDataSourceAsync_CancelledToken_StopsWithoutMarkingTheSchemaPrepared()
    {
        await using PostgresDatabase database = await CreateDatabaseAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => database.GetDataSourceAsync(cancelled.Token).AsTask());

        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(CancellationToken.None);
        Assert.Equal(
            SchemaSteps.All.Select(step => step.Version),
            await ReadAppliedVersionsAsync(dataSource));
    }

    private async Task<PostgresDatabase> CreateDatabaseAsync() => CreateDatabase(await postgres.CreateDatabaseAsync());

    private static PostgresDatabase CreateDatabase(string connectionString) =>
        new(connectionString, NullLogger<PostgresDatabase>.Instance);

    private static async Task<T> ScalarAsync<T>(NpgsqlDataSource dataSource, string sql)
    {
        await using NpgsqlCommand command = dataSource.CreateCommand(sql);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int[]> ReadAppliedVersionsAsync(NpgsqlDataSource dataSource)
    {
        var versions = new List<int>();
        await using NpgsqlCommand command = dataSource.CreateCommand(
            "SELECT version FROM schema_migrations ORDER BY version");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(reader.GetInt32(0));
        }

        return [.. versions];
    }
}
