namespace PianoMapper.Music;

public static class ScoreFingeringEditor
{
    public static Score SetFingering(Score score, int noteOrdinal, int? fingerNumber)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegative(noteOrdinal);
        if (fingerNumber is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(fingerNumber));
        }

        return UpdateNote(
            score,
            noteOrdinal,
            note => note with
            {
                Fingering = fingerNumber is int number
                    ? note.Fingering is { } existing
                        ? existing with { Number = number }
                        : new ScoreFingering(number)
                    : null,
            });
    }

    public static Score SetHand(Score score, int noteOrdinal, Staff hand)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegative(noteOrdinal);
        if (hand is not Staff.Treble and not Staff.Bass)
        {
            throw new ArgumentOutOfRangeException(nameof(hand));
        }

        return UpdateNote(score, noteOrdinal, note => note with { Staff = hand });
    }

    private static Score UpdateNote(
        Score score,
        int noteOrdinal,
        Func<ScoreNote, ScoreNote> update)
    {
        int remainingOrdinal = noteOrdinal;
        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            ScoreMeasure measure = score.Measures[measureIndex];
            if (remainingOrdinal >= measure.Notes.Count)
            {
                remainingOrdinal -= measure.Notes.Count;
                continue;
            }

            ScoreNote note = measure.Notes[remainingOrdinal];
            ScoreNote[] notes = measure.Notes.ToArray();
            notes[remainingOrdinal] = update(note);
            ScoreMeasure[] measures = score.Measures.ToArray();
            measures[measureIndex] = measure with { Notes = notes };
            return score with { Measures = measures };
        }

        throw new ArgumentOutOfRangeException(nameof(noteOrdinal));
    }
}
