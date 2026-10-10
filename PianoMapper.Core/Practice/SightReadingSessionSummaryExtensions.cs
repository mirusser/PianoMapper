namespace PianoMapper.Practice;

/// <summary>
/// What a stored <see cref="SightReadingSessionSummary"/> can honestly say. Entries written by older versions lack
/// members or fused outcomes that later versions keep apart, so every reader (mastery, the guided ladder, the trend,
/// the history list) asks here instead of re-deriving what it may trust from the schema version, the mode and the
/// nullable members. Each answer is null or false when the entry cannot say.
/// </summary>
public static class SightReadingSessionSummaryExtensions
{
    /// <summary>Whether the entry was recorded before schema version 2, so it lacks the layout, rhythm and separated outcomes.</summary>
    public static bool IsLegacy(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.SchemaVersion < SightReadingSessionSummary.DetailedSchemaVersion;
    }

    /// <summary>
    /// Whether the per-pitch counts can be read as pitch accuracy. A legacy entry recorded in a timed mode fused
    /// wrong-key and early/late/short/long outcomes into one first-try flag, so its counts cannot be trusted. A
    /// pitch-only legacy entry and every current-schema entry are fine.
    /// </summary>
    public static bool HasReliablePitchCounts(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return !summary.IsLegacy() || summary.Mode == NoteReadingMode.PitchAndOrder;
    }

    /// <summary>
    /// Whether the entry was recorded with the details needed to tell ladder levels apart: schema version 2 or later
    /// (layout and rhythm known, pitch outcome separated) and at least one prompt. Older entries cannot be matched
    /// safely (a grand-staff session stored an arbitrary staff), so they are ignored rather than guessed at.
    /// </summary>
    public static bool CanBeMatchedToLevel(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary is { PromptCount: > 0, IsGrandStaff: not null, RhythmPreset: not null, PitchFirstTryCorrectCount: not null } &&
            !summary.IsLegacy();
    }

    /// <summary>
    /// The share of prompts whose pitches were found without a wrong key, or null when the entry has no prompts or
    /// cannot say (a legacy timed entry's first-try flag mixed pitch with timing).
    /// </summary>
    public static double? PitchFirstTryPercent(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        int? correct = summary.PitchFirstTryCorrectCount ??
            (summary.Mode == NoteReadingMode.PitchAndOrder ? summary.FirstTryCorrectCount : null);
        return summary.PromptCount > 0 && correct is { } count ? 100.0 * count / summary.PromptCount : null;
    }

    /// <summary>
    /// The share of prompts whose timing was fine, or null when the mode does not grade timing, the entry has no
    /// prompts, or it never recorded the timing mistakes. Rhythm only has no pitch to get wrong, so there every
    /// prompt that was not clean counts, including notes never played (which are not "timing mistakes" in the stored
    /// counts).
    /// </summary>
    public static double? TimingCleanPercent(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.PromptCount == 0 || !summary.Mode.IsTimingGraded())
        {
            return null;
        }

        int? clean = summary.Mode == NoteReadingMode.RhythmOnly
            ? summary.FirstTryCorrectCount
            : summary.TimingMistakeCount is { } timingMistakes ? summary.PromptCount - timingMistakes : null;
        return clean is { } count ? 100.0 * count / summary.PromptCount : null;
    }

    /// <summary>
    /// The pitch-versus-timing split for a timed session, or null when the mode does not grade timing or the entry
    /// predates the split (schema version 1 fused the two).
    /// </summary>
    public static SessionTimingBreakdown? GetTimingBreakdown(this SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.Mode.IsTimingGraded() &&
            summary is { PitchFirstTryCorrectCount: { } pitchFirstTryCorrectCount, TimingMistakeCount: { } timingMistakeCount }
                ? new SessionTimingBreakdown(pitchFirstTryCorrectCount, timingMistakeCount)
                : null;
    }
}
