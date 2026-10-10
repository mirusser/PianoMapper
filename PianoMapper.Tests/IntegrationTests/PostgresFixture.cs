using Npgsql;
using Testcontainers.PostgreSql;

namespace PianoMapper.Tests.IntegrationTests;

/// <summary>
/// One real PostgreSQL container for every integration test class, started once (Docker must be running). Each test
/// asks for its own empty database, so tests cannot see each other's rows and schema creation is tested from scratch.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await container.StartAsync();

    public async Task DisposeAsync() => await container.DisposeAsync();

    /// <summary>Creates an empty database on the shared container and returns a connection string for it.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        string databaseName = $"test_{Guid.NewGuid():N}";
        await CreateDatabaseNamedAsync(databaseName);
        return ConnectionStringFor(databaseName);
    }

    public async Task CreateDatabaseNamedAsync(string databaseName)
    {
        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", admin);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>A connection string for a database of that name, which may not exist yet.</summary>
    public string ConnectionStringFor(string databaseName) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = databaseName }.ConnectionString;
}
