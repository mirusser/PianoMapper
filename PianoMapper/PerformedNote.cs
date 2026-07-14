using PianoMapper.Music;

namespace PianoMapper;

internal sealed record PerformedNote
{
    public required Pitch Pitch { get; init; }

    public required TimeSpan StartTime { get; init; }

    public TimeSpan? ReleaseTime { get; internal set; }
}
