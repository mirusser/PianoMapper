namespace PianoMapper.Practice;

/// <summary>
/// What a finished time-driven run means in the exercise's own terms: one result per prompt (the same type the
/// pitch-gated session produces), plus the notes that matched nothing and the per-verdict counts, which belong to the
/// run as a whole rather than to any prompt.
/// </summary>
public sealed record PlayAlongOutcome(
    IReadOnlyList<NoteReadingPromptResult> PromptResults,
    int ExtraNoteCount,
    IReadOnlyDictionary<Verdict, int> VerdictCounts);
