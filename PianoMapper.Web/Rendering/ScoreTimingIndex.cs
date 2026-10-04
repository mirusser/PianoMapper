using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Immutable score timing lookups shared by all geometry in one cached score window.
/// </summary>
internal sealed class ScoreTimingIndex
{
    private readonly double[] measureStartBeats;

    private ScoreTimingIndex(double[] measureStartBeats, int[] keyFifthsByMeasure)
    {
        this.measureStartBeats = measureStartBeats;
        KeyFifthsByMeasure = keyFifthsByMeasure;
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

        return new ScoreTimingIndex(measureStarts, ScoreKeys.GetKeyFifthsByMeasure(score));
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
}
