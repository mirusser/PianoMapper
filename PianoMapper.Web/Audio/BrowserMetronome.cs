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
        await StartAsync(timeSignature, tempo, anchor, cancellationToken);
    }

    /// <summary>
    /// Starts the click on a caller-chosen audio-clock anchor, used verbatim: for a click that has to line up with
    /// something already scheduled (a play-along run's start) instead of starting "a moment from now".
    /// </summary>
    internal async ValueTask StartAsync(
        TimeSignature timeSignature,
        Tempo tempo,
        TimeSpan anchor,
        CancellationToken cancellationToken = default)
    {
        var grid = new MetronomeGrid(anchor, tempo, timeSignature);
        await audio.StartMetronomeAsync(
            grid.Anchor,
            grid.BeatDuration,
            grid.TimeSignature.Numerator,
            grid.BeatsPerGroup,
            cancellationToken);
        Grid = grid;
    }

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await audio.StopMetronomeAsync(cancellationToken);
        Grid = null;
    }
}
