namespace PianoMapper.Music;

public readonly record struct TimeSignature
{
    public TimeSignature(int numerator, NoteValue beatNoteValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numerator);
        Numerator = numerator;
        BeatNoteValue = beatNoteValue;
    }

    public int Numerator { get; }

    public NoteValue BeatNoteValue { get; }
}
