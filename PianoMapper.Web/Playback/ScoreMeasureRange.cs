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

        int[] keyFifthsByMeasure = ScoreKeys.GetKeyFifthsByMeasure(score);
        var measures = score.Measures
            .Skip(firstMeasureIndex)
            .Take(lastMeasureIndex - firstMeasureIndex + 1)
            .Select((measure, index) => measure with
            {
                Notes = measure.Notes
                    .Select(note => note with { MeasureIndex = index })
                    .ToArray(),
                Rests = measure.Rests
                    .Select(rest => rest with { MeasureIndex = index })
                    .ToArray(),
                // The range opens in whatever key was in effect there, so its first measure states no change.
                KeyFifths = index == 0 ? null : measure.KeyFifths,
            })
            .ToArray();

        return score with { KeyFifths = keyFifthsByMeasure[firstMeasureIndex], Measures = measures };
    }
}
