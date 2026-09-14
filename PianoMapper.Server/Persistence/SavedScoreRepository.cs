using Npgsql;
using NpgsqlTypes;
using PianoMapper.Music;
using PianoMapper.Scores;

namespace PianoMapper.Server.Persistence;

internal sealed class SavedScoreRepository(NpgsqlDataSource dataSource)
{
    internal const string ConnectionStringName = "PianoMapper";

    private const string InitializeDatabaseSql = """
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

    private const string ListScoresSql = """
        SELECT id, title, measure_count, created_at, updated_at
        FROM scores
        ORDER BY updated_at DESC, id;
        """;

    private const string FindScoreSql = """
        SELECT id, title, score_document::text, document_version, created_at, updated_at
        FROM scores
        WHERE id = @id;
        """;

    private const string CreateScoreSql = """
        INSERT INTO scores (id, title, measure_count, score_document, document_version)
        VALUES (@id, @title, @measureCount, @scoreDocument, @documentVersion)
        RETURNING created_at, updated_at;
        """;

    private const string UpdateScoreSql = """
        UPDATE scores
        SET title = @title,
            measure_count = @measureCount,
            score_document = @scoreDocument,
            document_version = @documentVersion,
            updated_at = now()
        WHERE id = @id
        RETURNING created_at, updated_at;
        """;

    private const string DeleteScoreSql = """
        DELETE FROM scores
        WHERE id = @id;
        """;

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(InitializeDatabaseSql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<SavedScoreSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var scores = new List<SavedScoreSummary>();
        await using var command = dataSource.CreateCommand(ListScoresSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            scores.Add(new SavedScoreSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return scores;
    }

    internal async Task<SavedScoreDetails?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(FindScoreSql);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        string title = reader.GetString(1);
        var score = ScoreDocumentSerializer.Deserialize(title, reader.GetString(2), reader.GetInt32(3));
        return new SavedScoreDetails(
            reader.GetGuid(0),
            score,
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    internal async Task<SavedScoreDetails> CreateAsync(Score score, CancellationToken cancellationToken)
    {
        ValidateScore(score);

        Guid id = Guid.NewGuid();
        await using var command = dataSource.CreateCommand(CreateScoreSql);
        AddScoreParameters(command, id, score);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new SavedScoreDetails(
            id,
            score,
            reader.GetFieldValue<DateTimeOffset>(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    internal async Task<SavedScoreDetails?> UpdateAsync(
        Guid id,
        Score score,
        CancellationToken cancellationToken)
    {
        ValidateScore(score);

        await using var command = dataSource.CreateCommand(UpdateScoreSql);
        AddScoreParameters(command, id, score);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SavedScoreDetails(
            id,
            score,
            reader.GetFieldValue<DateTimeOffset>(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    internal async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(DeleteScoreSql);
        command.Parameters.AddWithValue("id", id);
        int deletedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return deletedRows == 1;
    }

    private static void AddScoreParameters(NpgsqlCommand command, Guid id, Score score)
    {
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("title", NpgsqlDbType.Text, score.Title);
        command.Parameters.AddWithValue("measureCount", score.Measures.Count);
        command.Parameters.AddWithValue(
            "scoreDocument",
            NpgsqlDbType.Jsonb,
            ScoreDocumentSerializer.Serialize(score));
        command.Parameters.AddWithValue("documentVersion", ScoreDocumentSerializer.CurrentVersion);
    }

    private static void ValidateScore(Score score)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentException.ThrowIfNullOrWhiteSpace(score.Title);
        ArgumentNullException.ThrowIfNull(score.Measures);
    }
}
