namespace PianoMapper.Music;

/// <summary>Pins one score note to a finger during a single fingering-generation request.</summary>
public sealed class ScoreFingeringLock
{
    public ScoreFingeringLock(ScoreFingeringNoteAddress address, int fingerNumber)
    {
        if (address.MeasureIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        if (address.NoteIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        if (fingerNumber is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(fingerNumber));
        }

        Address = address;
        FingerNumber = fingerNumber;
    }

    public ScoreFingeringNoteAddress Address { get; }

    public int FingerNumber { get; }
}
