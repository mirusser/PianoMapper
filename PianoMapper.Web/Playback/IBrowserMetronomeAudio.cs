using PianoMapper.Web.Audio;

namespace PianoMapper.Web.Playback;

internal interface IBrowserMetronomeAudio
{
    ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default);

    ValueTask StartMetronomeAsync(
        TimeSpan anchor,
        TimeSpan beatDuration,
        int beatsPerMeasure,
        IReadOnlyList<int> groupStartBeatIndices,
        double volume,
        MetronomeTimbre timbre,
        CancellationToken cancellationToken = default);

    ValueTask SetMetronomeSoundAsync(
        double volume,
        MetronomeTimbre timbre,
        CancellationToken cancellationToken = default);

    ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default);
}
