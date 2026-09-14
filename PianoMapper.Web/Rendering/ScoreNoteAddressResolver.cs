using PianoMapper.Music;

namespace PianoMapper.Web.Rendering;

internal static class ScoreNoteAddressResolver
{
    internal static int ResolveOrdinal(
        Score score,
        int selectedFirstMeasure,
        ScoreNoteAddress address)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegative(selectedFirstMeasure);
        if (address.MeasureIndex < 0 || address.NoteIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        int measureIndex = selectedFirstMeasure + address.MeasureIndex;
        if (measureIndex >= score.Measures.Count ||
            address.NoteIndex >= score.Measures[measureIndex].Notes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        return score.Measures
            .Take(measureIndex)
            .Sum(measure => measure.Notes.Count) + address.NoteIndex;
    }
}
