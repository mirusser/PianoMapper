namespace PianoMapper.Web.Audio;

/// <summary>Sound and grouping choices for a manual metronome grid.</summary>
internal sealed record MetronomeOptions
{
    internal double Volume { get; init; } = 1;

    internal MetronomeTimbre Timbre { get; init; } = MetronomeTimbre.Sine;

    /// <summary>Optional beat counts per felt group; their sum must equal the meter numerator.</summary>
    internal IReadOnlyList<int>? GroupLengths { get; init; }

    internal void Validate()
    {
        if (Volume is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Volume), "Metronome volume must be between zero and one.");
        }
    }
}

internal enum MetronomeTimbre
{
    Sine,
    Triangle,
}
