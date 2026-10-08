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

    /// <summary>
    /// Maps a note chosen in the displayed score to the address of the same note in the source score that fingering
    /// generation reads. The two can bar differently (the display may use another time signature), so the note's
    /// position in score order is what links them. Null when the address identifies no note.
    /// </summary>
    internal static ScoreFingeringNoteAddress? TryResolveSourceAddress(
        Score displayedScore,
        Score sourceScore,
        int selectedFirstMeasure,
        ScoreNoteAddress address)
    {
        ArgumentNullException.ThrowIfNull(displayedScore);
        ArgumentNullException.ThrowIfNull(sourceScore);
        int displayedMeasure = selectedFirstMeasure + address.MeasureIndex;
        if (selectedFirstMeasure < 0 ||
            address.MeasureIndex < 0 ||
            address.NoteIndex < 0 ||
            displayedMeasure >= displayedScore.Measures.Count ||
            address.NoteIndex >= displayedScore.Measures[displayedMeasure].Notes.Count)
        {
            return null;
        }

        int remaining = ResolveOrdinal(displayedScore, selectedFirstMeasure, address);
        for (int measureIndex = 0; measureIndex < sourceScore.Measures.Count; measureIndex++)
        {
            int noteCount = sourceScore.Measures[measureIndex].Notes.Count;
            if (remaining < noteCount)
            {
                return new ScoreFingeringNoteAddress(measureIndex, remaining);
            }

            remaining -= noteCount;
        }

        return null;
    }
}
