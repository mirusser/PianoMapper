using Npgsql;
using NpgsqlTypes;
using PianoMapper.Practice;

namespace PianoMapper.Server.Persistence;

/// <summary>
/// The learner's finished sessions, one row each and append-only. Rows are keyed by <c>SessionId</c>, so uploading a
/// session twice (a retry, or two browsers sharing an old entry) stores it once. Every method takes the learner it is
/// about; see <see cref="LearnerScope"/> for who that is.
/// </summary>
internal sealed class ProgressSessionRepository(PostgresDatabase database)
{
    private const string InsertSessionsSql = """
        INSERT INTO progress_sessions (session_id, learner_id, completed_at, document_version, session_document)
        SELECT incoming.session_id, @learnerId, incoming.completed_at, incoming.document_version, incoming.session_document
        FROM unnest(@sessionIds, @completedAts, @documentVersions, @sessionDocuments)
            AS incoming(session_id, completed_at, document_version, session_document)
        ON CONFLICT (session_id) DO NOTHING;
        """;

    private const string ListSessionsSql = """
        SELECT session_document::text
        FROM progress_sessions
        WHERE learner_id = @learnerId
        ORDER BY completed_at DESC, session_id
        LIMIT @limit;
        """;

    private const string DeleteSessionsSql = """
        DELETE FROM progress_sessions
        WHERE learner_id = @learnerId;
        """;

    /// <summary>
    /// Stores the sessions that are not stored yet and returns how many that was. The whole batch is checked first: a
    /// session that is not a current-format document (an unsupported schema version, or no id) rejects the batch and
    /// stores nothing.
    /// </summary>
    internal async Task<int> SaveAsync(
        Guid learnerId,
        IReadOnlyList<SightReadingSessionSummary> sessions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        foreach (SightReadingSessionSummary session in sessions)
        {
            ValidateSession(session);
        }

        if (sessions.Count == 0)
        {
            return 0;
        }

        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = dataSource.CreateCommand(InsertSessionsSql);
        command.Parameters.AddWithValue("learnerId", learnerId);
        command.Parameters.AddWithValue(
            "sessionIds",
            NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            sessions.Select(session => session.SessionId).ToArray());
        command.Parameters.AddWithValue(
            "completedAts",
            NpgsqlDbType.Array | NpgsqlDbType.TimestampTz,
            sessions.Select(session => session.CompletedAt.UtcDateTime).ToArray());
        command.Parameters.AddWithValue(
            "documentVersions",
            NpgsqlDbType.Array | NpgsqlDbType.Integer,
            sessions.Select(session => session.SchemaVersion).ToArray());
        command.Parameters.AddWithValue(
            "sessionDocuments",
            NpgsqlDbType.Array | NpgsqlDbType.Jsonb,
            sessions.Select(SightReadingSessionSerializer.Serialize).ToArray());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The newest sessions first, at most <paramref name="limit"/>. A stored document this version cannot read (one
    /// written by a newer version of the app) is left out rather than failing the list, as the browser cache does.
    /// </summary>
    internal async Task<IReadOnlyList<SightReadingSessionSummary>> ListNewestAsync(
        Guid learnerId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = dataSource.CreateCommand(ListSessionsSql);
        command.Parameters.AddWithValue("learnerId", learnerId);
        command.Parameters.AddWithValue("limit", limit);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var sessions = new List<SightReadingSessionSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (SightReadingSessionSerializer.TryParse(reader.GetString(0)) is { } session)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    /// <summary>Removes every session of the learner, and only theirs. Returns how many were removed.</summary>
    internal async Task<int> DeleteAllAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        NpgsqlDataSource dataSource = await database.GetDataSourceAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = dataSource.CreateCommand(DeleteSessionsSql);
        command.Parameters.AddWithValue("learnerId", learnerId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateSession(SightReadingSessionSummary session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.SchemaVersion is < SightReadingSessionSummary.LegacySchemaVersion
            or > SightReadingSessionSummary.CurrentSchemaVersion)
        {
            throw new ArgumentException(
                $"Session {session.SessionId} has unsupported schema version {session.SchemaVersion}.",
                nameof(session));
        }

        if (session.SessionId == Guid.Empty)
        {
            throw new ArgumentException("A session needs an id.", nameof(session));
        }
    }
}
