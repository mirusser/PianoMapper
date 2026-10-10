namespace PianoMapper.Server.Persistence;

/// <summary>
/// One ordered, idempotent change to the database schema. <see cref="Version"/> is recorded in
/// <c>schema_migrations</c> once the step has run, and a step is never edited after it has shipped: later changes
/// are new steps.
/// </summary>
internal sealed record SchemaStep(int Version, string Name, string Sql);
