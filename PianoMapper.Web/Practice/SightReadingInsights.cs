using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Reads the exercise history and says what is worth knowing: weak notes by staff, habitual wrong keys, how fast
/// the learner is when self-paced, and a short trend. Pure and history-only: nothing new is stored for it.
/// </summary>
public static class SightReadingInsights
{
    /// <summary>A note is a weak spot from this weakness score up (one first-try miss in five).</summary>
    public const double WeakNoteThreshold = 0.2;

    public const int MaximumWeakSpots = 5;

    /// <summary>A wrong-key pattern must have happened this many times before it is called a habit.</summary>
    public const int MinimumHabitCount = 3;

    public const int MaximumHabits = 3;

    public const int TrendSessionCount = 10;

    /// <summary>The newer half of the trend must beat the older half by this many percentage points to count as a change.</summary>
    private const double TrendChangePoints = 5;

    private const int MinimumTrendPoints = 4;

    public static SightReadingInsightReport Build(SightReadingHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        TrendPoint[] trend = BuildTrend(history);
        return new SightReadingInsightReport(
            GetWeakSpots(history),
            BuildHabits(history),
            BuildSpeed(history),
            trend,
            GetDirection(trend));
    }

    /// <summary>The weak notes the range can drill: weakness at least <see cref="WeakNoteThreshold"/>, worst first.</summary>
    public static IReadOnlyList<NoteMastery> GetWeakNotes(SightReadingHistory history) =>
        history.ComputeNoteMasteryWeakestFirst()
            .Where(note => note.WeaknessScore >= WeakNoteThreshold)
            .ToArray();

    private static IReadOnlyList<NoteMastery> GetWeakSpots(SightReadingHistory history) =>
        GetWeakNotes(history).Take(MaximumWeakSpots).ToArray();

    private static IReadOnlyList<ConfusionHabit> BuildHabits(SightReadingHistory history) =>
        history.Entries
            .SelectMany(entry => entry.Confusions ?? [])
            .Select(confusion => (confusion.Staff, Difference: Normalize(PitchDistance.Measure(confusion.Expected, confusion.Played)), confusion.Count))
            .GroupBy(item => (item.Staff, item.Difference))
            .Select(group => new ConfusionHabit(group.Key.Staff, group.Key.Difference, group.Sum(item => item.Count)))
            .Where(habit => habit.Count >= MinimumHabitCount)
            .OrderByDescending(habit => habit.Count)
            .ThenBy(habit => habit.Staff)
            .ThenBy(habit => habit.Difference.DiatonicSteps)
            .Take(MaximumHabits)
            .ToArray();

    /// <summary>Drops the accidental difference of an interval so "a step higher" groups across notes with and without accidentals.</summary>
    private static PitchDifference Normalize(PitchDifference difference) =>
        difference.Kind is PitchDifferenceKind.Interval or PitchDifferenceKind.Octaves
            ? difference with { AlterDelta = 0 }
            : difference;

    private static SpeedInsight? BuildSpeed(SightReadingHistory history)
    {
        var sessionAverages = new List<double>();
        long totalMilliseconds = 0;
        long totalSamples = 0;
        foreach (SightReadingSessionSummary entry in history.Entries)
        {
            NoteAttemptSummary[] timed = (entry.NoteAttempts ?? [])
                .Where(attempt => attempt is { ResponseSampleCount: > 0, TotalResponseMilliseconds: not null })
                .ToArray();
            int samples = timed.Sum(attempt => attempt.ResponseSampleCount);
            if (samples == 0)
            {
                continue;
            }

            long milliseconds = timed.Sum(attempt => attempt.TotalResponseMilliseconds!.Value);
            sessionAverages.Add(milliseconds / (double)samples);
            totalMilliseconds += milliseconds;
            totalSamples += samples;
        }

        if (sessionAverages.Count == 0 || totalMilliseconds <= 0)
        {
            return null;
        }

        double[] ordered = [.. sessionAverages.Order()];
        int middle = ordered.Length / 2;
        double median = ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2;
        return new SpeedInsight(
            TimeSpan.FromMilliseconds(median),
            totalSamples * 60_000.0 / totalMilliseconds,
            sessionAverages.Count);
    }

    private static TrendPoint[] BuildTrend(SightReadingHistory history) =>
        history.Entries
            .Select(ToTrendPoint)
            .OfType<TrendPoint>()
            .Take(TrendSessionCount)
            .OrderBy(point => point.CompletedAt)
            .ToArray();

    private static TrendPoint? ToTrendPoint(SightReadingSessionSummary entry)
    {
        if (entry.PromptCount == 0)
        {
            return null;
        }

        // Legacy entries recorded in a timed mode fused pitch and timing into one number, so they say nothing here.
        int? pitchCorrect = entry.PitchFirstTryCorrectCount ??
            (entry.Mode == NoteReadingMode.PitchAndOrder ? entry.FirstTryCorrectCount : null);
        if (pitchCorrect is not { } correct)
        {
            return null;
        }

        double? timingClean = entry.TimingMistakeCount is { } timingMistakes && SightReadingLabels.IsTimingGraded(entry.Mode)
            ? 100.0 * (entry.PromptCount - timingMistakes) / entry.PromptCount
            : null;
        return new TrendPoint(entry.CompletedAt, 100.0 * correct / entry.PromptCount, timingClean);
    }

    private static TrendDirection GetDirection(IReadOnlyList<TrendPoint> trend)
    {
        if (trend.Count < MinimumTrendPoints)
        {
            return TrendDirection.NotEnoughData;
        }

        int half = trend.Count / 2;
        double older = trend.Take(half).Average(point => point.PitchAccuracyPercent);
        double newer = trend.Skip(trend.Count - half).Average(point => point.PitchAccuracyPercent);
        return newer - older >= TrendChangePoints
            ? TrendDirection.Improving
            : older - newer >= TrendChangePoints
                ? TrendDirection.Declining
                : TrendDirection.Steady;
    }
}
