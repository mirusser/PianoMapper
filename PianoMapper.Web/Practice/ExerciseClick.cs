using PianoMapper.Music;
using PianoMapper.Web.Audio;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The metronome click a timed run starts for itself (a play-along count-in, or a wait-for-me run's click anchored on
/// the learner's first key, and optionally the steady click while the run is played). It shares the page's <see cref="BrowserMetronome"/> with the manual metronome, so it remembers which beat
/// grid it started: stopping it never silences a metronome the learner started themselves, and a manual restart takes
/// ownership of the click away from the run.
/// </summary>
internal sealed class ExerciseClick(BrowserMetronome metronome)
{
    private MetronomeGrid? startedGrid;

    internal bool IsRunning => startedGrid is not null && ReferenceEquals(metronome.Grid, startedGrid);

    /// <summary>Whether this click, and not a manual metronome, is the one running on that audio-clock anchor.</summary>
    internal bool IsRunningOn(TimeSpan anchor) => IsRunning && startedGrid!.Anchor == anchor;

    internal async ValueTask StartAsync(Score score, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(score);
        await metronome.StartAsync(score.TimeSignature, score.Tempo, cancellationToken);
        startedGrid = metronome.Grid;
    }

    /// <summary>Starts the click on an explicit audio-clock anchor, so it lines up with a run scheduled elsewhere.</summary>
    internal async ValueTask StartAsync(Score score, TimeSpan anchor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(score);
        await metronome.StartAsync(score.TimeSignature, score.Tempo, anchor, cancellationToken);
        startedGrid = metronome.Grid;
    }

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        bool ownsRunningClick = IsRunning;
        startedGrid = null;
        if (ownsRunningClick)
        {
            await metronome.StopAsync(cancellationToken);
        }
    }
}
