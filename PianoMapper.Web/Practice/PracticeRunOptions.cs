namespace PianoMapper.Web.Practice;

/// <summary>Optional browser-audio behavior for a practice run.</summary>
internal sealed record PracticeRunOptions
{
    /// <summary>
    /// Whether the run plays its own one-measure count-in clicks. Off when the metronome already clicks the count-in,
    /// so there is one click track.
    /// </summary>
    public bool ScheduleCountInClicks { get; init; } = true;
}
