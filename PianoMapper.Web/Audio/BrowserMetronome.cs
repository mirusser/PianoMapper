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
        CancellationToken cancellationToken = default,
        MetronomeOptions? options = null)
    {
        TimeSpan anchor = await audio.GetCurrentTimeAsync(cancellationToken) + SchedulingLead;
        await StartAsync(timeSignature, tempo, anchor, cancellationToken, options);
    }

    /// <summary>
    /// Starts the click on a caller-chosen audio-clock anchor, used verbatim: for a click that has to line up with
    /// something already scheduled (a play-along run's start) instead of starting "a moment from now".
    /// </summary>
    internal async ValueTask StartAsync(
        TimeSignature timeSignature,
        Tempo tempo,
        TimeSpan anchor,
        CancellationToken cancellationToken = default,
        MetronomeOptions? options = null)
    {
        options ??= new MetronomeOptions();
        options.Validate();
        var grid = new MetronomeGrid(anchor, tempo, timeSignature);
        MetronomeAccentPattern accentPattern = MetronomeAccentPattern.Create(grid, options.GroupLengths);
        await audio.StartMetronomeAsync(
            grid.Anchor,
            grid.BeatDuration,
            grid.TimeSignature.Numerator,
            accentPattern.GroupStartBeatIndices,
            options.Volume,
            options.Timbre,
            cancellationToken);
        Grid = grid;
    }

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await audio.StopMetronomeAsync(cancellationToken);
        Grid = null;
    }

    /// <summary>Changes the sound of an active grid without moving its beat positions.</summary>
    internal ValueTask SetSoundAsync(
        double volume,
        MetronomeTimbre timbre,
        CancellationToken cancellationToken = default)
    {
        new MetronomeOptions { Volume = volume, Timbre = timbre }.Validate();
        return IsRunning
            ? audio.SetMetronomeSoundAsync(volume, timbre, cancellationToken)
            : ValueTask.CompletedTask;
    }
}
