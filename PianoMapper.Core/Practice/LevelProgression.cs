using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// The guided path as a pure function of the history and the static catalog: which levels are passed, which is next,
/// and at what tempo. It only recommends (it never locks a level) and has no UI or storage dependency.
/// </summary>
public static class LevelProgression
{
    private const double PercentTolerance = 1e-9;

    public static LevelProgressionReport Evaluate(SightReadingHistory history, LevelProgressionRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        rules ??= LevelProgressionRules.Default;
        IReadOnlyList<ExerciseLevel> catalog = ExerciseLevelCatalog.Levels;
        var evaluations = catalog.Select(level => Evaluate(history, level, rules)).ToArray();

        int firstUnpassed = Array.FindIndex(evaluations, evaluation => !evaluation.IsPassed);
        bool allPassed = firstUnpassed < 0;
        int recommendedIndex = allPassed ? catalog.Count - 1 : firstUnpassed;

        var levels = new LevelProgress[catalog.Count];
        for (int index = 0; index < catalog.Count; index++)
        {
            LevelEvaluation evaluation = evaluations[index];
            LevelStatus status = evaluation.IsPassed
                ? LevelStatus.Passed
                : index == recommendedIndex
                    ? LevelStatus.Recommended
                    : evaluation.MatchingSessionCount > 0 ? LevelStatus.InProgress : LevelStatus.NotTried;
            levels[index] = new LevelProgress(
                catalog[index],
                status,
                evaluation.Window.Length,
                evaluation.AveragePitchPercent,
                evaluation.AverageTimingCleanPercent,
                evaluation.RecommendedTempo);
        }

        return new LevelProgressionReport(
            levels,
            new LevelRecommendation(catalog[recommendedIndex], evaluations[recommendedIndex].RecommendedTempo),
            allPassed);
    }

    private static LevelEvaluation Evaluate(SightReadingHistory history, ExerciseLevel level, LevelProgressionRules rules)
    {
        SightReadingSessionSummary[] matching = history.Entries.Where(entry => Matches(entry, level)).ToArray();
        SightReadingSessionSummary[] window = matching.Take(rules.RequiredSessions).ToArray();
        bool isPassed = window.Length == rules.RequiredSessions && window.All(entry => Passes(entry, level, rules));
        return new LevelEvaluation(
            matching.Length,
            window,
            isPassed,
            window.Length == 0 ? null : window.Average(PitchPercent),
            window.Length == 0 || !level.IsTimed ? null : window.Average(entry => TimingCleanPercent(entry, level)),
            level.IsTimed ? RecommendTempo(matching, level, rules) : null);
    }

    /// <summary>
    /// A session counts toward a level when it was recorded with the details needed to tell levels apart (schema
    /// version 2: layout and rhythm known, pitch outcome separated) and its range, staff or grand staff, mode and
    /// rhythm all equal the level's. Older entries cannot be matched safely (a grand-staff session stored an arbitrary
    /// staff), so they are ignored rather than guessed at.
    /// </summary>
    private static bool Matches(SightReadingSessionSummary entry, ExerciseLevel level) =>
        entry is { PromptCount: > 0, IsGrandStaff: { } isGrandStaff, RhythmPreset: { } rhythmPreset, PitchFirstTryCorrectCount: not null } &&
        entry.SchemaVersion >= SightReadingSessionSummary.CurrentSchemaVersion &&
        entry.PresetId == level.PresetId.ToString() &&
        isGrandStaff == level.IsGrandStaff &&
        (isGrandStaff || entry.Staff == level.Staff) &&
        entry.Mode == level.Mode &&
        rhythmPreset == level.RhythmPreset.ToString();

    private static bool Passes(SightReadingSessionSummary entry, ExerciseLevel level, LevelProgressionRules rules) =>
        PitchPercent(entry) >= rules.MinimumPitchFirstTryPercent - PercentTolerance &&
        (!level.IsTimed || TimingCleanPercent(entry, level) >= rules.MinimumTimingCleanPercent - PercentTolerance);

    private static double PitchPercent(SightReadingSessionSummary entry) =>
        100.0 * (entry.PitchFirstTryCorrectCount ?? 0) / entry.PromptCount;

    /// <summary>
    /// Share of prompts whose timing was fine. Rhythm only has no pitch to get wrong, so there every prompt that was
    /// not clean counts, including notes never played (which are not "timing mistakes" in the stored counts).
    /// </summary>
    private static double TimingCleanPercent(SightReadingSessionSummary entry, ExerciseLevel level) =>
        level.Mode == NoteReadingMode.RhythmOnly
            ? 100.0 * entry.FirstTryCorrectCount / entry.PromptCount
            : 100.0 * (entry.PromptCount - (entry.TimingMistakeCount ?? entry.PromptCount)) / entry.PromptCount;

    private static double CleanPercent(SightReadingSessionSummary entry) =>
        100.0 * entry.FirstTryCorrectCount / entry.PromptCount;

    /// <summary>
    /// Steps the tempo down from the latest session after <see cref="LevelProgressionRules.StruggleSessionCount"/>
    /// struggling sessions in a row; otherwise one step above the tempo of the latest passing session; with no pass
    /// yet it stays where the learner last played (or at the level's start tempo with no sessions).
    /// </summary>
    private static int RecommendTempo(
        SightReadingSessionSummary[] matchingNewestFirst,
        ExerciseLevel level,
        LevelProgressionRules rules)
    {
        int maximum = level.MaximumTempoPulsesPerMinute!.Value;
        SightReadingSessionSummary[] withTempo = matchingNewestFirst
            .Where(entry => entry.TempoBeatsPerMinute is not null)
            .ToArray();
        if (withTempo.Length == 0)
        {
            return level.StartTempoPulsesPerMinute!.Value;
        }

        int latestTempo = withTempo[0].TempoBeatsPerMinute!.Value;
        SightReadingSessionSummary[] recent = withTempo.Take(rules.StruggleSessionCount).ToArray();
        if (recent.Length == rules.StruggleSessionCount &&
            recent.All(entry => CleanPercent(entry) < rules.StruggleBelowPercent))
        {
            return Math.Max(SightReadingExerciseOptions.MinimumTempoPulsesPerMinute, latestTempo - rules.TempoStepPulses);
        }

        SightReadingSessionSummary? lastPassing = withTempo.FirstOrDefault(entry => Passes(entry, level, rules));
        return lastPassing is not null
            ? Math.Min(maximum, lastPassing.TempoBeatsPerMinute!.Value + rules.TempoStepPulses)
            : Math.Clamp(latestTempo, SightReadingExerciseOptions.MinimumTempoPulsesPerMinute, maximum);
    }

    private sealed record LevelEvaluation(
        int MatchingSessionCount,
        SightReadingSessionSummary[] Window,
        bool IsPassed,
        double? AveragePitchPercent,
        double? AverageTimingCleanPercent,
        int? RecommendedTempo);
}
