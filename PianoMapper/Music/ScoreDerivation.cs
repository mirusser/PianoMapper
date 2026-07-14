namespace PianoMapper.Music;

internal static class ScoreDerivation
{
    private const double BeatComparisonTolerance = 1e-9;

    public static IReadOnlyList<ScoreEvent> Flatten(Score score)
    {
        var notes = score.Measures
            .SelectMany(measure => measure.Notes)
            .OrderBy(note => GetOnsetBeats(note, score.TimeSignature))
            .ThenBy(note => note.Pitch.MidiNumber)
            .ToArray();
        var consumed = new bool[notes.Length];
        var events = new List<ScoreEvent>(notes.Length);

        for (int index = 0; index < notes.Length; index++)
        {
            if (consumed[index])
            {
                continue;
            }

            var note = notes[index];
            double onsetBeats = GetOnsetBeats(note, score.TimeSignature);
            double durationBeats = MusicalTime.GetBeats(note.NoteValue, score.TimeSignature);
            var tiedNote = note;
            var sourceNotes = new List<ScoreNote> { note };

            while (tiedNote.TiesToNext)
            {
                int continuationIndex = FindTieContinuation(notes, consumed, tiedNote, onsetBeats + durationBeats, score.TimeSignature);
                if (continuationIndex < 0)
                {
                    break;
                }

                consumed[continuationIndex] = true;
                tiedNote = notes[continuationIndex];
                sourceNotes.Add(tiedNote);
                durationBeats += MusicalTime.GetBeats(tiedNote.NoteValue, score.TimeSignature);
            }

            events.Add(new ScoreEvent(note.Pitch, onsetBeats, durationBeats, note.Staff, sourceNotes.ToArray()));
        }

        return events;
    }

    private static int FindTieContinuation(
        IReadOnlyList<ScoreNote> notes,
        IReadOnlyList<bool> consumed,
        ScoreNote tiedNote,
        double expectedOnset,
        TimeSignature timeSignature)
    {
        for (int index = 0; index < notes.Count; index++)
        {
            var candidate = notes[index];
            if (!consumed[index] &&
                candidate.Pitch == tiedNote.Pitch &&
                candidate.Staff == tiedNote.Staff &&
                Math.Abs(GetOnsetBeats(candidate, timeSignature) - expectedOnset) <= BeatComparisonTolerance)
            {
                return index;
            }
        }

        return -1;
    }

    public static double GetOnsetBeats(ScoreNote note, TimeSignature timeSignature) =>
        (note.MeasureIndex * timeSignature.Numerator) + note.BeatOffset;
}
