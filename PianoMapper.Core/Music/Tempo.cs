using System.Text.Json.Serialization;

namespace PianoMapper.Music;

public readonly record struct Tempo
{
    [JsonConstructor]
    public Tempo(double beatsPerMinute)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beatsPerMinute);
        BeatsPerMinute = beatsPerMinute;
    }

    public double BeatsPerMinute { get; }
}
