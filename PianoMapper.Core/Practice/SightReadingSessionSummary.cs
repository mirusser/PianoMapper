using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// A small, serializable snapshot of one completed generated exercise, suitable for local history and mastery
/// adaptation. Deliberately excludes the full <see cref="Score"/> object graph and any mutable session state.
/// <see cref="PresetId"/> is stored as a string (the preset's enum member name), never an enum ordinal, so an
/// entry written by a future version with an unrecognized preset can still be parsed and skipped gracefully.
/// Schema version 2 adds the optional members below. They are additive and nullable: a version 1 entry parses with
/// every one of them <see langword="null"/> (meaning "recorded before this was tracked"), and later additions
/// follow the same rule without another version bump.
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
    /// <summary>The first schema, whose per-pitch counts fused pitch mistakes with timing mistakes.</summary>
    public const int LegacySchemaVersion = 1;

    public const int CurrentSchemaVersion = 2;

    /// <summary>The <see cref="SightReadingRhythmPreset"/> member name; null before schema version 2.</summary>
    public string? RhythmPreset { get; init; }

    /// <summary>Whether the exercise alternated staves (then <see cref="Staff"/> is meaningless); null before version 2.</summary>
    public bool? IsGrandStaff { get; init; }

    /// <summary>The learner-facing tempo in pulses per minute (quarter, or dotted quarter in 6/8); null when untracked.</summary>
    public int? TempoBeatsPerMinute { get; init; }

    /// <summary>The pacing style name; null means wait-for-me.</summary>
    public string? Pacing { get; init; }

    /// <summary>The <see cref="SightReadingMotion"/> member name the notes were chosen with; null when untracked.</summary>
    public string? Motion { get; init; }

    /// <summary>Prompts whose pitches were found without a wrong key, whatever their timing; null before version 2.</summary>
    public int? PitchFirstTryCorrectCount { get; init; }

    /// <summary>Prompts with at least one early, late, too-short or too-long outcome; null before version 2.</summary>
    public int? TimingMistakeCount { get; init; }

    public int? EarlyCount { get; init; }

    public int? LateCount { get; init; }

    public int? TooShortCount { get; init; }

    public int? TooLongCount { get; init; }

    /// <summary>The most confusions kept per session; the rarest are dropped to keep stored history small.</summary>
    public const int MaximumConfusions = 20;

    /// <summary>Per note and staff: attempts, pitch accuracy and response times; null before this was tracked.</summary>
    public IReadOnlyList<NoteAttemptSummary>? NoteAttempts { get; init; }

    /// <summary>The most frequent wrong-key-for-expected-note pairs of the session, most frequent first; null before this was tracked.</summary>
    public IReadOnlyList<ConfusionSummary>? Confusions { get; init; }

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
        int pitchFirstTryCorrectCount = promptResults.Count(result => result.IsPitchFirstTryCorrect);
        int wrongAttemptCount = promptResults.Sum(result => result.WrongAttemptCount);
        PitchAttemptSummary[] pitchAttempts = promptResults
            .SelectMany(result => result.ExpectedPitches.Select(pitch => new { pitch, result.IsPitchFirstTryCorrect }))
            .GroupBy(attempt => attempt.pitch)
            .Select(group => new PitchAttemptSummary(
                group.Key,
                group.Count(attempt => attempt.IsPitchFirstTryCorrect),
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
            pitchAttempts)
        {
            PitchFirstTryCorrectCount = pitchFirstTryCorrectCount,
            TimingMistakeCount = promptResults.Count(result => result.HasTimingMistake),
            EarlyCount = promptResults.Count(result => result.OnsetVerdict == Verdict.Early),
            LateCount = promptResults.Count(result => result.OnsetVerdict == Verdict.Late),
            TooShortCount = promptResults.Count(result => result.DurationVerdict == Verdict.TooShort),
            TooLongCount = promptResults.Count(result => result.DurationVerdict == Verdict.TooLong),
            NoteAttempts = BuildNoteAttempts(promptResults),
            Confusions = BuildConfusions(promptResults),
        };
    }

    private static NoteAttemptSummary[] BuildNoteAttempts(IReadOnlyList<NoteReadingPromptResult> promptResults) =>
        promptResults
            .SelectMany(result => result.ExpectedPitches.Select(pitch => new
            {
                Pitch = pitch,
                Staff = GetStaff(result, pitch),
                result.IsPitchFirstTryCorrect,
                result.ResponseTime,
            }))
            .GroupBy(attempt => (attempt.Pitch, attempt.Staff))
            .Select(group => new NoteAttemptSummary(
                group.Key.Pitch,
                group.Key.Staff,
                group.Count(),
                group.Count(attempt => attempt.IsPitchFirstTryCorrect),
                group.Count(attempt => attempt.ResponseTime is not null),
                group.Any(attempt => attempt.ResponseTime is not null)
                    ? group.Sum(attempt => (long)Math.Round(
                        attempt.ResponseTime?.TotalMilliseconds ?? 0,
                        MidpointRounding.AwayFromZero))
                    : null))
            .OrderBy(attempt => attempt.Pitch.MidiNumber)
            .ThenBy(attempt => attempt.Staff)
            .ToArray();

    private static ConfusionSummary[] BuildConfusions(IReadOnlyList<NoteReadingPromptResult> promptResults) =>
        promptResults
            .SelectMany(result => result.WrongPlayedPitches.Select(played =>
            {
                Pitch expected = result.ExpectedPitches
                    .MinBy(candidate => Math.Abs(candidate.DiatonicIndex - played.DiatonicIndex));
                return (Expected: expected, Played: played, Staff: GetStaff(result, expected));
            }))
            .GroupBy(confusion => confusion)
            .Select(group => new ConfusionSummary(group.Key.Expected, group.Key.Played, group.Key.Staff, group.Count()))
            .OrderByDescending(confusion => confusion.Count)
            .ThenBy(confusion => confusion.Expected.MidiNumber)
            .ThenBy(confusion => confusion.Played.MidiNumber)
            .ThenBy(confusion => confusion.Staff)
            .Take(MaximumConfusions)
            .ToArray();

    /// <summary>The staff the prompt's source note for this pitch is on (explicit in generated exercises).</summary>
    private static Staff GetStaff(NoteReadingPromptResult result, Pitch pitch) =>
        (result.ExpectedSourceNotes.FirstOrDefault(note => note.Pitch.MidiNumber == pitch.MidiNumber) ??
            result.ExpectedSourceNotes.FirstOrDefault())?.Staff ?? Staff.Treble;
}
