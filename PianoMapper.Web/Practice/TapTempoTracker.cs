namespace PianoMapper.Web.Practice;

/// <summary>
/// Derives a manual tempo from recent Web Audio clock taps. It deliberately has no wall-clock dependency, so a
/// reported tempo is in the same timebase as a subsequently started metronome grid.
/// </summary>
internal sealed class TapTempoTracker
{
    internal const int MinimumTempoBeatsPerMinute = 30;
    internal const int MaximumTempoBeatsPerMinute = 300;
    private const int RequiredTapCount = 4;
    private const int MaximumTapCount = 5;
    private readonly List<TimeSpan> taps = [];

    internal int TapCount => taps.Count;

    internal void Reset() => taps.Clear();

    /// <summary>
    /// Registers one tap and returns a bounded BPM after four valid taps. An implausibly fast, slow, or non-monotonic
    /// interval starts a new sequence at this tap instead of corrupting the learner's next tempo.
    /// </summary>
    internal int? Register(TimeSpan audioClockTime)
    {
        if (taps.Count > 0)
        {
            TimeSpan interval = audioClockTime - taps[^1];
            TimeSpan shortestAllowed = TimeSpan.FromMinutes(1d / MaximumTempoBeatsPerMinute);
            TimeSpan longestAllowed = TimeSpan.FromMinutes(1d / MinimumTempoBeatsPerMinute);
            if (interval < shortestAllowed || interval > longestAllowed)
            {
                taps.Clear();
            }
        }

        taps.Add(audioClockTime);
        if (taps.Count > MaximumTapCount)
        {
            taps.RemoveAt(0);
        }

        if (taps.Count < RequiredTapCount)
        {
            return null;
        }

        double[] intervals = taps
            .Zip(taps.Skip(1), (first, second) => (second - first).TotalSeconds)
            .Order()
            .ToArray();
        int middle = intervals.Length / 2;
        double medianIntervalSeconds = intervals.Length % 2 == 0
            ? (intervals[middle - 1] + intervals[middle]) / 2
            : intervals[middle];
        double beatsPerMinute = 60 / medianIntervalSeconds;
        return Math.Clamp(
            (int)Math.Round(beatsPerMinute, MidpointRounding.AwayFromZero),
            MinimumTempoBeatsPerMinute,
            MaximumTempoBeatsPerMinute);
    }
}
