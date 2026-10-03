using System.Collections.Immutable;
using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// The outcome of one exercise prompt. <see cref="IsFirstTryCorrect"/> means "everything clean" (pitch, onset and
/// duration); the separate outcome members let callers tell a pitch-reading mistake from a timing mistake.
/// </summary>
public sealed record NoteReadingPromptResult(
    int PromptIndex,
    double OnsetBeats,
    ImmutableArray<ScoreNote> ExpectedSourceNotes,
    ImmutableArray<Pitch> ExpectedPitches,
    ImmutableArray<Pitch> WrongPlayedPitches,
    int WrongAttemptCount,
    bool IsFirstTryCorrect,
    bool IsComplete,
    TimeSpan? CompletedAt)
{
    private readonly bool? isPitchFirstTryCorrect;

    /// <summary>
    /// Whether the prompt's pitches were found without a wrong key. A result that does not set this explicitly
    /// (the pre-split shape, where pitch and timing were fused) reports <see cref="IsFirstTryCorrect"/>.
    /// </summary>
    public bool IsPitchFirstTryCorrect
    {
        get => isPitchFirstTryCorrect ?? IsFirstTryCorrect;
        init => isPitchFirstTryCorrect = value;
    }

    /// <summary>
    /// The onset outcome (the worst onset among a chord's keys), or <see langword="null"/> when onset is not graded.
    /// </summary>
    public Verdict? OnsetVerdict { get; init; }

    /// <summary>
    /// The release outcome (the first duration mistake, else <see cref="Verdict.Correct"/>), or
    /// <see langword="null"/> when duration is not graded or no key of the prompt has been released yet.
    /// </summary>
    public Verdict? DurationVerdict { get; init; }

    /// <summary>
    /// The signed deviation of the worst onset from the beat it was graded against (positive is late), or
    /// <see langword="null"/> when onset is not graded.
    /// </summary>
    public TimeSpan? OnsetDeviation { get; init; }

    /// <summary>
    /// Whether the learner never played this prompt (a time-driven run moved past it). A pitch-gated run waits, so
    /// this is always false there.
    /// </summary>
    public bool WasMissed { get; init; }

    /// <summary>
    /// Time from the previous prompt's completed attack to this prompt's first correct attack. Null for the first
    /// prompt and for onset-graded modes, where the clock rather than the learner sets the pace.
    /// </summary>
    public TimeSpan? ResponseTime { get; init; }

    /// <summary>Whether any graded timing outcome (onset or duration) was not on time.</summary>
    public bool HasTimingMistake =>
        OnsetVerdict is Verdict.Early or Verdict.Late ||
        DurationVerdict is Verdict.TooShort or Verdict.TooLong;
}
