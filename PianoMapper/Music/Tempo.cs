namespace PianoMapper.Music;

internal readonly record struct Tempo
{
    public Tempo(double beatsPerMinute)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatsPerMinute);
        BeatsPerMinute = beatsPerMinute;
    }

    public double BeatsPerMinute { get; }
}
