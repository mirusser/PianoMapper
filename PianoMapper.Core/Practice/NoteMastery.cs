using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// What the history says about one note on one staff: how often it was asked, how often the right key was found
/// first, and how long the learner typically took. <see cref="Staff"/> is null for data recorded before the staff
/// was tracked (and for legacy entries); lookups merge that staff-agnostic data into each staff.
/// </summary>
/// <param name="MedianResponseTime">
/// The median of the per-session average response times for this note, or null when it was never timed (onset-graded
/// sessions are paced by the clock, not by the learner, so they are not timed).
/// </param>
/// <param name="WeaknessScore">How much this note needs work, see <see cref="CalculateWeakness"/>.</param>
public sealed record NoteMastery(
    Pitch Pitch,
    Staff? Staff,
    int AttemptCount,
    int CorrectFirstTryCount,
    TimeSpan? MedianResponseTime,
    double WeaknessScore)
{
    /// <summary>
    /// How much each full multiple of the learner's own typical time a note takes, beyond that typical time, adds to
    /// its weakness: a note answered in twice the typical time adds this much.
    /// </summary>
    public const double SlownessWeightPerExtraMultiple = 0.25;

    /// <summary>The most slowness can add, so a very slow note ranks below a very inaccurate one.</summary>
    public const double MaximumSlownessWeight = 0.5;

    public double AccuracyPercent => AttemptCount == 0
        ? 0
        : 100.0 * CorrectFirstTryCount / AttemptCount;

    /// <summary>
    /// Weakness is the fraction of first-try misses (0 for perfect, 1 for never right) plus, when the note is slower
    /// than the learner's own typical note time, a capped slowness part. A note faster than the typical time gets no
    /// credit, so speed can only raise weakness, never hide a miss. Without response times the score is accuracy alone.
    /// </summary>
    public static double CalculateWeakness(
        double accuracyPercent,
        TimeSpan? medianResponseTime,
        TimeSpan? learnerMedianResponseTime)
    {
        double inaccuracy = (100.0 - accuracyPercent) / 100.0;
        double slowness = 0;
        if (medianResponseTime is { } median &&
            learnerMedianResponseTime is { } typical &&
            typical > TimeSpan.Zero)
        {
            double multiple = median / typical;
            slowness = Math.Clamp((multiple - 1) * SlownessWeightPerExtraMultiple, 0, MaximumSlownessWeight);
        }

        return inaccuracy + slowness;
    }
}
