using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Runs a play-along exercise on the time-driven practice engine and hands its result to the exercise as if the
/// pitch-gated engine had produced it. It owns no timers and touches no UI: the page ticks
/// <see cref="UpdateAsync"/>, so the whole run (count-in, running, finished, aborted) is testable with fake audio.
/// </summary>
internal sealed class ExercisePlayAlongController(
    BrowserPracticeCoordinator practice,
    SightReadingExerciseCoordinator exercise,
    ExerciseClick click)
{
    private GradingOptions? gradingOptions;
    private bool hasCompletedRun;

    internal PracticeSessionState State => practice.State;

    /// <summary>True from the start of the count-in until the run finishes or is aborted.</summary>
    internal bool IsRunning => practice.IsActive;

    internal double CursorBeats => practice.CursorBeats;

    /// <summary>
    /// Where the page draws the red cursor line: the practice cursor while the run is going, and nothing in the count-in
    /// (as before) or once the run has finished, when the exercise is in review and the line would only sit at the end of
    /// the score.
    /// </summary>
    internal double? VisibleCursorBeats =>
        practice.State == PracticeSessionState.Running ? CursorBeats : null;

    /// <summary>The pitches to play next (for the opt-in amber keys): the upcoming prompt while a run is going.</summary>
    internal IReadOnlyList<Pitch> GetNextPitches() => practice.GetNextPitches();

    /// <summary>
    /// Starts the run for the current exercise. Rhythm-only runs ignore which key was played. With the exercise's
    /// click on, the metronome is the one click track: it starts exactly at the run's start (so its beat after the
    /// one-measure count-in is the graded anchor) and the practice engine adds no beeps of its own; with it off the
    /// practice engine beeps its count-in as it always has.
    /// </summary>
    internal async ValueTask StartAsync(
        TimeSpan onTimeTolerance,
        CancellationToken cancellationToken = default)
    {
        if (exercise.Score is not { } score ||
            exercise.RunPacing != ExercisePacing.PlayAlong ||
            exercise.Phase != SightReadingExercisePhase.Active)
        {
            throw new InvalidOperationException("A play-along run needs a generated exercise that is play-along paced.");
        }

        gradingOptions = new GradingOptions
        {
            OnTimeTolerance = onTimeTolerance,
            IgnorePitch = exercise.RunMode == NoteReadingMode.RhythmOnly,
        };
        hasCompletedRun = false;
        bool metronomeOwnsTheClick = exercise.ClickWhilePlaying;
        await practice.StartAsync(
            score,
            gradingOptions,
            new PracticeRunOptions { ScheduleCountInClicks = !metronomeOwnsTheClick },
            cancellationToken);
        if (metronomeOwnsTheClick)
        {
            TimeSpan runStart = practice.PracticeAnchor -
                MusicalTime.BeatsToDuration(score.TimeSignature.Numerator, score.Tempo);
            await click.StartAsync(score, runStart, cancellationToken);
        }
    }

    /// <summary>
    /// Advances the run. When it finishes, grades it, maps the result into prompt results and completes the exercise
    /// (once). Returns whether the run is still going, which is what the page's ticker wants.
    /// </summary>
    internal async ValueTask<bool> UpdateAsync(CancellationToken cancellationToken = default)
    {
        await practice.UpdateAsync(cancellationToken);
        if (practice.State == PracticeSessionState.Finished &&
            !hasCompletedRun &&
            practice.Result is { } result &&
            exercise.Score is { } score &&
            gradingOptions is not null)
        {
            hasCompletedRun = true;
            await click.StopAsync(cancellationToken);
            PlayAlongOutcome outcome = PlayAlongResultMapper.Map(
                result,
                exercise.RunMode,
                score.Tempo,
                practice.PracticeAnchor,
                gradingOptions);
            exercise.CompletePlayAlong(outcome, GetRunDuration(score));
        }

        return practice.IsActive;
    }

    /// <summary>Stops the run without completing the exercise, which stays active so it can be retried.</summary>
    internal async ValueTask AbortAsync(CancellationToken cancellationToken = default)
    {
        await click.StopAsync(cancellationToken);
        await practice.AbortAsync(cancellationToken);
    }

    private static TimeSpan GetRunDuration(Score score)
    {
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(score);
        double totalBeats = events.Count == 0 ? 0 : events.Max(scoreEvent => scoreEvent.OnsetBeats + scoreEvent.DurationBeats);
        return MusicalTime.BeatsToDuration(totalBeats, score.Tempo);
    }
}
