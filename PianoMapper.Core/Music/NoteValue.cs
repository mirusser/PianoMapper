namespace PianoMapper.Music;

public readonly record struct NoteValue
{
    public NoteValue(int denominator, int dots = 0)
    {
        if (denominator is not (1 or 2 or 4 or 8 or 16))
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(dots);

        Denominator = denominator;
        Dots = dots;
    }

    public int Denominator { get; }

    public int Dots { get; }
}
