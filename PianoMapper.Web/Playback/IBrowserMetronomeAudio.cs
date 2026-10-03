namespace PianoMapper.Web.Playback;

internal interface IBrowserMetronomeAudio
{
    ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default);

    ValueTask StartMetronomeAsync(
        TimeSpan anchor,
        TimeSpan beatDuration,
        int beatsPerMeasure,
        int beatsPerGroup,
        CancellationToken cancellationToken = default);

    ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default);
}
