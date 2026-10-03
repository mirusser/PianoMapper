using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>What the exercise history says about the learner, ready to show; built from history alone.</summary>
/// <param name="WeakSpots">The notes (by pitch and staff) most in need of work, worst first.</param>
/// <param name="Habits">The commonest ways the learner plays the wrong key, most frequent first.</param>
/// <param name="Speed">Null until at least one self-paced session has response times.</param>
/// <param name="Trend">Recent sessions, oldest first.</param>
public sealed record SightReadingInsightReport(
    IReadOnlyList<NoteMastery> WeakSpots,
    IReadOnlyList<ConfusionHabit> Habits,
    SpeedInsight? Speed,
    IReadOnlyList<TrendPoint> Trend,
    TrendDirection PitchTrend)
{
    /// <summary>False when there is nothing to report yet, so the panel keeps its "not enough attempts" copy.</summary>
    public bool HasEnoughData => WeakSpots.Count > 0 || Habits.Count > 0 || Speed is not null || Trend.Count > 0;
}
