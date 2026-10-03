namespace PianoMapper.Music;

public static class ScoreDerivation
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

    /// <summary>
    /// Groups events into prompts: events whose onsets lie within a billionth of a beat of the group's first onset
    /// are one chord (the same tolerance wherever a prompt is formed, so every engine agrees on what a prompt is).
    /// Groups are in onset order.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<ScoreEvent>> GroupByOnset(IReadOnlyList<ScoreEvent> scoreEvents)
    {
        ArgumentNullException.ThrowIfNull(scoreEvents);
        var groups = new List<IReadOnlyList<ScoreEvent>>();
        List<ScoreEvent>? currentGroup = null;
        double currentGroupOnsetBeats = 0;
        foreach (ScoreEvent scoreEvent in scoreEvents.OrderBy(candidate => candidate.OnsetBeats))
        {
            if (currentGroup is null || scoreEvent.OnsetBeats - currentGroupOnsetBeats > BeatComparisonTolerance)
            {
                currentGroupOnsetBeats = scoreEvent.OnsetBeats;
                currentGroup = [];
                groups.Add(currentGroup);
            }

            currentGroup.Add(scoreEvent);
        }

        return groups;
    }

    /// <summary>
    /// The note that continues <paramref name="tiedNote"/>'s tie: same staff and pitch, starting where it ends (in the
    /// same measure or the next one), or null when the score has none. The same rule <see cref="Flatten"/> merges by.
    /// </summary>
    public static ScoreNote? FindTieContinuation(Score score, ScoreNote tiedNote)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(tiedNote);
        double endBeats = GetOnsetBeats(tiedNote, score.TimeSignature) +
            MusicalTime.GetBeats(tiedNote.NoteValue, score.TimeSignature);
        int measureIndex = (int)Math.Floor((endBeats + BeatComparisonTolerance) / score.TimeSignature.Numerator);
        if (measureIndex < 0 || measureIndex >= score.Measures.Count)
        {
            return null;
        }

        return score.Measures[measureIndex].Notes.FirstOrDefault(candidate =>
            candidate.Pitch == tiedNote.Pitch &&
            candidate.Staff == tiedNote.Staff &&
            Math.Abs(GetOnsetBeats(candidate, score.TimeSignature) - endBeats) <= BeatComparisonTolerance);
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
