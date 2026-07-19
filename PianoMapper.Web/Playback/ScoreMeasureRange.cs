using PianoMapper.Music;

namespace PianoMapper.Web.Playback;

internal static class ScoreMeasureRange
{
    internal static Score Create(Score score, int firstMeasureIndex, int lastMeasureIndex)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegative(firstMeasureIndex);
        if (firstMeasureIndex >= score.Measures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(firstMeasureIndex));
        }

        if (lastMeasureIndex < firstMeasureIndex || lastMeasureIndex >= score.Measures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lastMeasureIndex));
        }

        var measures = score.Measures
            .Skip(firstMeasureIndex)
            .Take(lastMeasureIndex - firstMeasureIndex + 1)
            .Select((measure, index) => new ScoreMeasure(
                measure.Notes
                    .Select(note => note with { MeasureIndex = index })
                    .ToArray(),
                measure.Rests
                    .Select(rest => rest with { MeasureIndex = index })
                    .ToArray()))
            .ToArray();

        return score with { Measures = measures };
    }
}
