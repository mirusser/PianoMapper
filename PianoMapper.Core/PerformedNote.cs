using PianoMapper.Music;

namespace PianoMapper;

public sealed record PerformedNote
{
    public required Pitch Pitch { get; init; }

    public required TimeSpan StartTime { get; init; }

    public TimeSpan? ReleaseTime { get; internal set; }

    /// <summary>
    /// A note that has already ended, such as one of a scheduled score's, made without a <see cref="NoteTimeline"/> to
    /// complete it in: the timeline checks every note it holds on each one it completes, which is slow for a long score.
    /// </summary>
    public static PerformedNote Completed(Pitch pitch, TimeSpan startTime, TimeSpan releaseTime) =>
        new()
        {
            Pitch = pitch,
            StartTime = startTime,
            ReleaseTime = releaseTime,
        };
}
