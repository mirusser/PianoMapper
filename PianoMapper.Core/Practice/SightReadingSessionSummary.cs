using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// A small, serializable snapshot of one completed generated exercise, suitable for local history and mastery
/// adaptation. Deliberately excludes the full <see cref="Score"/> object graph and any mutable session state.
/// <see cref="PresetId"/> is stored as a string (the preset's enum member name), never an enum ordinal, so an
/// entry written by a future version with an unrecognized preset can still be parsed and skipped gracefully.
/// </summary>
public sealed record SightReadingSessionSummary(
    int SchemaVersion,
    DateTimeOffset CompletedAt,
    string PresetId,
    Staff Staff,
    NoteReadingMode Mode,
    int PromptCount,
    TimeSpan ElapsedTime,
    int FirstTryCorrectCount,
    int WrongAttemptCount,
    IReadOnlyList<PitchAttemptSummary> PitchAttempts)
{
    public const int CurrentSchemaVersion = 1;

    public static SightReadingSessionSummary Create(
        DateTimeOffset completedAt,
        string presetId,
        Staff staff,
        NoteReadingMode mode,
        TimeSpan elapsedTime,
        IReadOnlyList<NoteReadingPromptResult> promptResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
        ArgumentNullException.ThrowIfNull(promptResults);

        int firstTryCorrectCount = promptResults.Count(result => result.IsFirstTryCorrect);
        int wrongAttemptCount = promptResults.Sum(result => result.WrongAttemptCount);
        PitchAttemptSummary[] pitchAttempts = promptResults
            .SelectMany(result => result.ExpectedPitches.Select(pitch => new { pitch, result.IsFirstTryCorrect }))
            .GroupBy(attempt => attempt.pitch)
            .Select(group => new PitchAttemptSummary(
                group.Key,
                group.Count(attempt => attempt.IsFirstTryCorrect),
                group.Count()))
            .ToArray();

        return new SightReadingSessionSummary(
            CurrentSchemaVersion,
            completedAt,
            presetId,
            staff,
            mode,
            promptResults.Count,
            elapsedTime,
            firstTryCorrectCount,
            wrongAttemptCount,
            pitchAttempts);
    }
}
