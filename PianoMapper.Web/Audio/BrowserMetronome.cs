using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Web.Audio;

internal sealed class BrowserMetronome(IBrowserMetronomeAudio audio)
{
    private static readonly TimeSpan SchedulingLead = TimeSpan.FromMilliseconds(50);

    internal MetronomeGrid? Grid { get; private set; }

    internal bool IsRunning => Grid is not null;

    internal async ValueTask StartAsync(
        TimeSignature timeSignature,
        Tempo tempo,
        CancellationToken cancellationToken = default)
    {
        TimeSpan anchor = await audio.GetCurrentTimeAsync(cancellationToken) + SchedulingLead;
        var grid = new MetronomeGrid(anchor, tempo, timeSignature);
        await audio.StartMetronomeAsync(
            grid.Anchor,
            grid.BeatDuration,
            grid.TimeSignature.Numerator,
            cancellationToken);
        Grid = grid;
    }

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await audio.StopMetronomeAsync(cancellationToken);
        Grid = null;
    }
}
