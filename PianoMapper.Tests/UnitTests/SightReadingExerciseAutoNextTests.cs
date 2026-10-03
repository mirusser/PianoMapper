using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingExerciseAutoNextTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(60);

    [Fact]
    public void AutoNext_Initially_IsOn()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.True(coordinator.AutoNext);
    }

    [Fact]
    public void AutoNextRemaining_WithTheDefaultSetting_StartsWhenTheExerciseIsFinished()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), new FakeTimeProvider());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        FinishWaitForMe(coordinator);

        coordinator.ConsumeCompletionSummary();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAutoNext_UpdatesTheProperty(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.SetAutoNext(value);

        Assert.Equal(value, coordinator.AutoNext);
    }

    [Fact]
    public void SetAutoNext_PersistsAcrossGenerateRetryRetryMissedAndEnd()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        FinishWaitForMe(coordinator);

        Assert.True(coordinator.AutoNext);
        Assert.True(coordinator.RetryMissed(Tolerance));
        Assert.True(coordinator.AutoNext);
        coordinator.Retry(Tolerance);
        Assert.True(coordinator.AutoNext);
        coordinator.Generate(new Random(12), Tolerance);
        Assert.True(coordinator.AutoNext);
        coordinator.End();
        Assert.True(coordinator.AutoNext);
    }

    [Fact]
    public void AutoNextDelay_IsTenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), SightReadingExerciseCoordinator.AutoNextDelay);
    }

    [Fact]
    public void AutoNextRemaining_WhileTheExerciseIsActive_IsNull()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());

        Assert.Null(coordinator.ConsumeCompletionSummary());
        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_NoExercise_IsNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetAutoNext(true);

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_AutoNextOff_IsNullAfterCompletion()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());
        coordinator.SetAutoNext(false);
        FinishWaitForMe(coordinator);

        coordinator.ConsumeCompletionSummary();

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_AfterTheCompletionIsConsumed_CountsDownFromTheDelayAndStopsAtZero()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);

        timeProvider.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(TimeSpan.FromSeconds(6), coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);

        timeProvider.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(TimeSpan.Zero, coordinator.AutoNextRemaining);
        Assert.True(coordinator.IsAutoNextDue);

        timeProvider.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.Zero, coordinator.AutoNextRemaining);
        Assert.True(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_TurnedOnWhileTheExerciseIsActive_StartsWhenItIsFinished()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.SetAutoNext(false);
        coordinator.Generate(new Random(11), Tolerance);

        coordinator.SetAutoNext(true);
        timeProvider.Advance(TimeSpan.FromSeconds(60));
        Assert.Null(coordinator.AutoNextRemaining);

        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
    }

    [Fact]
    public void SetAutoNext_TurnedOnInReview_StartsTheCountdownFromThen()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.SetAutoNext(false);
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        coordinator.SetAutoNext(true);

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void SetAutoNext_TurnedOffInReview_ClearsTheCountdown()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        Assert.NotNull(coordinator.AutoNextRemaining);

        coordinator.SetAutoNext(false);

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void CancelAutoNext_StopsTheCountdownButKeepsTheSettingAndArmsAgainForTheNextRun()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();

        coordinator.CancelAutoNext();
        timeProvider.Advance(TimeSpan.FromSeconds(60));

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
        Assert.True(coordinator.AutoNext);

        coordinator.Retry(Tolerance);
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
    }

    [Fact]
    public void RestartAutoNext_WhileCounting_StartsTheDelayAgain()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        timeProvider.Advance(TimeSpan.FromSeconds(7));

        coordinator.RestartAutoNext();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
    }

    [Fact]
    public void RestartAutoNext_AfterCancel_DoesNotStartTheCountdown()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        coordinator.CancelAutoNext();

        coordinator.RestartAutoNext();

        Assert.Null(coordinator.AutoNextRemaining);
    }

    [Fact]
    public void AutoNextRemaining_AfterGenerate_IsNull()
    {
        SightReadingExerciseCoordinator coordinator = CreateArmedCoordinator();

        coordinator.Generate(new Random(12), Tolerance);

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_AfterRetry_IsNull()
    {
        SightReadingExerciseCoordinator coordinator = CreateArmedCoordinator();

        coordinator.Retry(Tolerance);

        Assert.Null(coordinator.AutoNextRemaining);
    }

    [Fact]
    public void AutoNextRemaining_AfterRetryMissed_IsNull()
    {
        var timeProvider = new FakeTimeProvider();
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        Assert.NotNull(coordinator.AutoNextRemaining);

        Assert.True(coordinator.RetryMissed(Tolerance));

        Assert.Null(coordinator.AutoNextRemaining);
    }

    [Fact]
    public void AutoNextRemaining_AfterEnd_IsNull()
    {
        SightReadingExerciseCoordinator coordinator = CreateArmedCoordinator();

        coordinator.End();

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void AutoNextRemaining_PlayAlongRunWithSomethingPlayed_IsArmed()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, playedPromptCount: 1), TimeSpan.FromSeconds(9));

        coordinator.ConsumeCompletionSummary();

        Assert.Equal(SightReadingExerciseCoordinator.AutoNextDelay, coordinator.AutoNextRemaining);
    }

    [Fact]
    public void AutoNextRemaining_PlayAlongRunWithNothingPlayed_StaysOffSoAnUnattendedRunCannotChain()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, playedPromptCount: 0), TimeSpan.FromSeconds(9));

        coordinator.ConsumeCompletionSummary();

        Assert.Null(coordinator.AutoNextRemaining);
        Assert.False(coordinator.IsAutoNextDue);
    }

    [Fact]
    public void SetAutoNext_TurnedOnAfterAPlayAlongRunWithNothingPlayed_DoesNotStartTheCountdown()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.SetAutoNext(false);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, playedPromptCount: 0), TimeSpan.FromSeconds(9));
        coordinator.ConsumeCompletionSummary();

        coordinator.SetAutoNext(true);

        Assert.Null(coordinator.AutoNextRemaining);
    }

    private static SightReadingExerciseCoordinator CreateCoordinator(TimeProvider timeProvider)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.SetPromptCountOption(4);
        coordinator.SetAutoNext(true);
        coordinator.Generate(new Random(11), Tolerance);
        return coordinator;
    }

    private static SightReadingExerciseCoordinator CreateArmedCoordinator()
    {
        SightReadingExerciseCoordinator coordinator = CreateCoordinator(new FakeTimeProvider());
        FinishWaitForMe(coordinator);
        coordinator.ConsumeCompletionSummary();
        Assert.NotNull(coordinator.AutoNextRemaining);
        return coordinator;
    }

    private static void FinishWaitForMe(SightReadingExerciseCoordinator coordinator)
    {
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
    }

    private static SightReadingExerciseCoordinator CreatePlayAlongCoordinator()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), new FakeTimeProvider());
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetPromptCountOption(8);
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        coordinator.SetAutoNext(true);
        coordinator.Generate(new Random(21), Tolerance);
        return coordinator;
    }

    /// <summary>Plays the first prompts of the exercise perfectly in time and never plays the rest.</summary>
    private static PlayAlongOutcome MapPlayAlong(SightReadingExerciseCoordinator coordinator, int playedPromptCount)
    {
        Score score = coordinator.Score!;
        TimeSpan anchor = TimeSpan.FromSeconds(10);
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(score);
        ScoreEvent[] played =
        [
            .. ScoreDerivation.GroupByOnset(events).Take(playedPromptCount).SelectMany(prompt => prompt),
        ];
        PerformedNote[] performed = played
            .Select(scoreEvent =>
            {
                TimeSpan start = anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, score.Tempo);
                return new PerformedNote
                {
                    Pitch = scoreEvent.Pitch,
                    StartTime = start,
                    ReleaseTime = start + MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, score.Tempo),
                };
            })
            .ToArray();
        var options = new GradingOptions();
        GradingResult result = Grader.Grade(events, score.Tempo, performed, anchor, anchor + TimeSpan.FromMinutes(5), options);
        return PlayAlongResultMapper.Map(result, coordinator.Mode, score.Tempo, anchor, options);
    }
}
