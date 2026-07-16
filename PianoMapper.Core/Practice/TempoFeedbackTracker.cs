using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed class TempoFeedbackTracker
{
    private const int MaximumAlignments = 10;
    private readonly List<BeatAlignment> alignments = [];

    public BeatAlignment? LastAlignment => alignments.LastOrDefault();

    public IReadOnlyList<BeatAlignment> RecentAlignments => alignments;

    public int OnTimeCount => alignments.Count(alignment => alignment.Verdict == Verdict.Correct);

    public int TotalCount => alignments.Count;

    public TimeSpan? MedianDeviation
    {
        get
        {
            if (alignments.Count == 0)
            {
                return null;
            }

            var orderedTicks = alignments
                .Select(alignment => alignment.Deviation.Ticks)
                .Order()
                .ToArray();
            int middle = orderedTicks.Length / 2;
            long medianTicks = orderedTicks.Length % 2 == 1
                ? orderedTicks[middle]
                : orderedTicks[middle - 1] + ((orderedTicks[middle] - orderedTicks[middle - 1]) / 2);
            return TimeSpan.FromTicks(medianTicks);
        }
    }

    public BeatAlignment? Record(
        TimeSpan onset,
        MetronomeGrid? grid,
        TimeSpan onTimeTolerance)
    {
        if (grid is null)
        {
            return null;
        }

        var alignment = BeatAlignment.Classify(onset, grid, onTimeTolerance);
        if (alignments.Count == MaximumAlignments)
        {
            alignments.RemoveAt(0);
        }

        alignments.Add(alignment);
        return alignment;
    }

    public void Reset() => alignments.Clear();
}
