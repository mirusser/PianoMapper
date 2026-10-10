namespace PianoMapper.Server.Persistence;

internal static class SchemaSteps
{
    internal static IReadOnlyList<SchemaStep> All { get; } =
    [
        new SchemaStep(1, "scores", """
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
            """),
        new SchemaStep(2, "progress_sessions", """
            CREATE TABLE IF NOT EXISTS progress_sessions (
                session_id uuid PRIMARY KEY,
                learner_id uuid NOT NULL,
                completed_at timestamptz NOT NULL,
                document_version integer NOT NULL CONSTRAINT progress_sessions_document_version_ck CHECK (document_version > 0),
                session_document jsonb NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS progress_sessions_learner_completed_idx
                ON progress_sessions (learner_id, completed_at DESC);
            """),
    ];
}
