using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Immutable score timing lookups shared by all geometry in one cached score window.
/// </summary>
internal sealed class ScoreTimingIndex
{
    private readonly double[] measureStartBeats;
    private readonly int beatsPerMeasure;

    private ScoreTimingIndex(double[] measureStartBeats, int[] keyFifthsByMeasure, int beatsPerMeasure)
    {
        this.measureStartBeats = measureStartBeats;
        KeyFifthsByMeasure = keyFifthsByMeasure;
        this.beatsPerMeasure = beatsPerMeasure;
    }

    internal IReadOnlyList<int> KeyFifthsByMeasure { get; }

    internal static ScoreTimingIndex Create(Score score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var measureStarts = new double[score.Measures.Count + 1];
        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            measureStarts[measureIndex + 1] = measureStarts[measureIndex] +
                (score.Measures[measureIndex].LengthInBeats ?? score.TimeSignature.Numerator);
        }

        return new ScoreTimingIndex(measureStarts, ScoreKeys.GetKeyFifthsByMeasure(score), score.TimeSignature.Numerator);
    }

    internal double GetMeasureStartBeats(int measureIndex) => measureStartBeats[measureIndex];

    internal int FindMeasureIndex(double absoluteBeats)
    {
        int index = Array.BinarySearch(measureStartBeats, absoluteBeats);
        if (index >= 0)
        {
            return Math.Min(index, measureStartBeats.Length - 2);
        }

        return (~index) - 1;
    }

    internal float MapScoreOnsetToX(
        int measureIndex,
        double beatOffset,
        int firstVisibleMeasure,
        int visibleMeasureCount)
    {
        double relativeBeats = measureStartBeats[measureIndex] - measureStartBeats[firstVisibleMeasure] + beatOffset;
        return GrandStaffLayout.ScoreX0 + (float)(relativeBeats /
            (visibleMeasureCount * beatsPerMeasure) *
            (GrandStaffLayout.ScoreX1 - GrandStaffLayout.ScoreX0));
    }
}
