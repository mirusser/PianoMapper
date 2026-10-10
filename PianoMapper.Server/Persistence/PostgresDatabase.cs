using Microsoft.Extensions.Logging;
using Npgsql;

namespace PianoMapper.Server.Persistence;

/// <summary>
/// The server's one door to PostgreSQL: owns the data source and prepares the schema the first time it is needed.
/// Nothing connects at startup, so the host boots with the database down; a failed preparation is not remembered and
/// is tried again by the next use, so a database that comes up later is picked up without a restart.
/// </summary>
internal sealed class PostgresDatabase : IAsyncDisposable
{
    internal const string ConnectionStringName = "PianoMapper";

    /// <summary>Serialises schema preparation across server instances sharing a database.</summary>
    private const long SchemaLockKey = 0x504D_5343_4845_4D41;

    private const string AcquireSchemaLockSql = "SELECT pg_advisory_xact_lock(@key);";

    private const string CreateMigrationTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version integer PRIMARY KEY,
            name text NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now()
        );
        """;

    private const string ReadAppliedVersionsSql = "SELECT version FROM schema_migrations;";

    private const string RecordStepSql = "INSERT INTO schema_migrations (version, name) VALUES (@version, @name);";

    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<PostgresDatabase> logger;
    private readonly SemaphoreSlim schemaGate = new(1, 1);
    private volatile bool isSchemaReady;

    internal PostgresDatabase(string connectionString, ILogger<PostgresDatabase> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        dataSource = NpgsqlDataSource.Create(connectionString);
        this.logger = logger;
    }

    /// <summary>
    /// The data source, once the schema is up to date. Throws what Npgsql throws when the database cannot be reached;
    /// the caller maps that (see <see cref="PersistenceFailure"/>).
    /// </summary>
    internal async ValueTask<NpgsqlDataSource> GetDataSourceAsync(CancellationToken cancellationToken)
    {
        if (!isSchemaReady)
        {
            await PrepareSchemaAsync(cancellationToken).ConfigureAwait(false);
        }

        return dataSource;
    }

    public async ValueTask DisposeAsync()
    {
        await dataSource.DisposeAsync().ConfigureAwait(false);
        schemaGate.Dispose();
    }

    private async Task PrepareSchemaAsync(CancellationToken cancellationToken)
    {
        await schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isSchemaReady)
            {
                return;
            }

            await ApplyStepsAsync(cancellationToken).ConfigureAwait(false);
            isSchemaReady = true;
        }
        finally
        {
            schemaGate.Release();
        }
    }

    private async Task ApplyStepsAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var acquireLock = new NpgsqlCommand(AcquireSchemaLockSql, connection, transaction))
        {
            acquireLock.Parameters.AddWithValue("key", SchemaLockKey);
            await acquireLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var createMigrationTable = new NpgsqlCommand(CreateMigrationTableSql, connection, transaction))
        {
            await createMigrationTable.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        HashSet<int> appliedVersions =
            await ReadAppliedVersionsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        SchemaStep[] pendingSteps =
        [
            .. SchemaSteps.All.Where(step => !appliedVersions.Contains(step.Version)).OrderBy(step => step.Version),
        ];
        foreach (SchemaStep step in pendingSteps)
        {
            await using (var apply = new NpgsqlCommand(step.Sql, connection, transaction))
            {
                await apply.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var record = new NpgsqlCommand(RecordStepSql, connection, transaction))
            {
                record.Parameters.AddWithValue("version", step.Version);
                record.Parameters.AddWithValue("name", step.Name);
                await record.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        foreach (SchemaStep step in pendingSteps)
        {
            logger.LogInformation("Applied database schema step {Version} ({Name})", step.Version, step.Name);
        }
    }

    private static async Task<HashSet<int>> ReadAppliedVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var versions = new HashSet<int>();
        await using var command = new NpgsqlCommand(ReadAppliedVersionsSql, connection, transaction);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }
}
