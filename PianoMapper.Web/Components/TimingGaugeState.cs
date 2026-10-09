using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Web.Components;

/// <summary>
/// What the timing gauge over the grand staff shows: how far off the beat the learner's latest notes were (positive
/// is late, oldest first) against the exercise's on-time window. The gauge is a scale with the beat in the middle, so
/// the latest note's distance from the middle is its error at a glance.
/// </summary>
/// <param name="RecentDeviations">The latest notes' signed offsets from the beat, oldest first.</param>
/// <param name="OnTimeTolerance">How far off still counts as on time (the exercise's own window).</param>
/// <param name="IsClickRunning">Whether the exercise click is sounding, so the gauge shows its pulse and beat count.</param>
public sealed record TimingGaugeState(
    IReadOnlyList<TimeSpan> RecentDeviations,
    TimeSpan OnTimeTolerance,
    bool IsClickRunning = false)
{
    /// <summary>How many of the learner's latest notes the gauge shows.</summary>
    public const int RecentNoteCount = 6;

    /// <summary>How far from the beat each end of the scale is. A note further off sits on the edge.</summary>
    public static readonly TimeSpan Range = TimeSpan.FromMilliseconds(400);

    public TimeSpan? Latest => RecentDeviations.Count == 0 ? null : RecentDeviations[^1];

    /// <summary>Half the width of the on-time band as a fraction of the whole scale, never past an edge.</summary>
    public double OnTimeHalfWidth => Math.Min(1.0, OnTimeTolerance / Range) / 2.0;

    /// <summary>Where a note sits on the scale: 0 is the early edge, 0.5 the beat and 1 the late edge.</summary>
    public static double GetPosition(TimeSpan deviation) => 0.5 + (Math.Clamp(deviation / Range, -1.0, 1.0) / 2.0);

    public Verdict Classify(TimeSpan deviation) => deviation < -OnTimeTolerance
        ? Verdict.Early
        : deviation > OnTimeTolerance
            ? Verdict.Late
            : Verdict.Correct;

    public string Describe(TimeSpan deviation) => Classify(deviation) switch
    {
        Verdict.Early => $"{SightReadingLabels.SignedMilliseconds(deviation)} early",
        Verdict.Late => $"{SightReadingLabels.SignedMilliseconds(deviation)} late",
        _ => "On time",
    };
}
