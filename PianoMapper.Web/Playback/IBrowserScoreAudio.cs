namespace PianoMapper.Web.Playback;

internal interface IBrowserScoreAudio
{
    ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default);

    ValueTask ScheduleScoreAsync(
        IReadOnlyList<BrowserScoreAudioEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules events whose start times are offsets from an anchor the audio engine picks <paramref name="lead"/>
    /// after its clock at the moment it takes the notes over, and returns that anchor on the audio clock. Use it when
    /// building the events takes long enough that an anchor chosen before it would already be past. The audio engine
    /// names the notes by their position, so the events' ids are not used.
    /// </summary>
    ValueTask<TimeSpan> ScheduleScoreAheadAsync(
        IReadOnlyList<BrowserScoreAudioEvent> events,
        TimeSpan lead,
        CancellationToken cancellationToken = default);

    ValueTask StopScoreAsync(CancellationToken cancellationToken = default);
}
