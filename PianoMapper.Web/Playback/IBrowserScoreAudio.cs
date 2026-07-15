namespace PianoMapper.Web.Playback;

internal interface IBrowserScoreAudio
{
    ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default);

    ValueTask ScheduleScoreAsync(
        IReadOnlyList<BrowserScoreAudioEvent> events,
        CancellationToken cancellationToken = default);

    ValueTask StopScoreAsync(CancellationToken cancellationToken = default);
}
