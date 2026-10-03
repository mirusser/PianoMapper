using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Playback;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ExercisePlayAlongControllerTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(60);

    [Fact]
    public async Task StartAsync_PlayAlongExercise_StartsTheCountInOnTheExerciseScore()
    {
        Fixture fixture = CreateFixture();

        await fixture.Controller.StartAsync(Tolerance);

        Assert.Equal(PracticeSessionState.CountingIn, fixture.Practice.State);
        Assert.True(fixture.Controller.IsRunning);
        // 4/4 at 120 pulses: the one-measure count-in is 2 s, from the audio clock plus the 50 ms scheduling lead.
        Assert.Equal(TimeSpan.FromSeconds(12.05), fixture.Practice.PracticeAnchor);
        Assert.Equal(SightReadingExercisePhase.Active, fixture.Exercise.Phase);
    }

    [Fact]
    public async Task StartAsync_ClickSettingOff_UsesThePracticeCountInBeepsAndNoMetronome()
    {
        Fixture fixture = CreateFixture();
        fixture.Exercise.SetClickWhilePlaying(false);

        await fixture.Controller.StartAsync(Tolerance);

        Assert.Equal(4, fixture.Audio.ScheduledEvents.Count);
        Assert.False(fixture.MetronomeAudio.IsRunning);
        Assert.False(fixture.Click.IsRunning);
        Assert.Equal(PracticeSessionState.CountingIn, fixture.Practice.State);
    }

    [Fact]
    public async Task StartAsync_ClickSettingOn_OneClickTrackAnchoredAtThePracticeStart()
    {
        Fixture fixture = CreateFixture();

        await fixture.Controller.StartAsync(Tolerance);

        // One click track: the practice schedules no beeps of its own and the metronome grid starts exactly where
        // the practice started, so the beat after the one-measure count-in is the graded anchor.
        Assert.Empty(fixture.Audio.ScheduledEvents);
        Assert.True(fixture.Click.IsRunning);
        TimeSpan practiceStart = fixture.Practice.PracticeAnchor - TimeSpan.FromSeconds(2);
        Assert.Equal(practiceStart, fixture.MetronomeAudio.Anchor);
        Assert.Equal(TimeSpan.FromMilliseconds(500), fixture.MetronomeAudio.BeatDuration);
        Assert.Equal(4, fixture.MetronomeAudio.BeatsPerMeasure);
    }

    [Fact]
    public async Task StartAsync_ClickSettingOn_ClickBeatsAndTheGradedAnchorNeverDrift()
    {
        Fixture fixture = CreateFixture(tempoPulses: 90);
        await fixture.Controller.StartAsync(Tolerance);
        MetronomeGrid grid = fixture.Metronome.Grid!;

        TimeSpan anchorBeat = grid.GetBeatTime(fixture.Exercise.Score!.TimeSignature.Numerator);

        Assert.Equal(fixture.Practice.PracticeAnchor, anchorBeat);
        Assert.Equal(TimeSpan.Zero, grid.GetDeviation(fixture.Practice.PracticeAnchor));
        for (int promptBeat = 0; promptBeat < 12; promptBeat++)
        {
            TimeSpan expectedOnset = fixture.Practice.PracticeAnchor +
                MusicalTime.BeatsToDuration(promptBeat, fixture.Exercise.Score.Tempo);
            // At 90 pulses a beat is 666.67 ms, which is not a whole number of ticks: allow the rounding, no more.
            Assert.True(grid.GetDeviation(expectedOnset).Duration() < TimeSpan.FromMicroseconds(1));
        }
    }

    [Fact]
    public async Task StartAsync_ClickSettingOn_FinishingTheRunStopsTheClick()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: null, states: []);

        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        Assert.False(fixture.Click.IsRunning);
        Assert.False(fixture.MetronomeAudio.IsRunning);
    }

    [Fact]
    public async Task AbortAsync_WithTheClickRunning_StopsTheClick()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.Controller.AbortAsync();

        Assert.False(fixture.Click.IsRunning);
        Assert.False(fixture.MetronomeAudio.IsRunning);
    }

    [Fact]
    public async Task AbortAsync_ManualMetronomeStartedMeanwhile_IsLeftRunning()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        await fixture.Metronome.StopAsync();
        await fixture.Metronome.StartAsync(new TimeSignature(3, new NoteValue(4)), new Tempo(100));

        await fixture.Controller.AbortAsync();

        Assert.True(fixture.Metronome.IsRunning);
    }

    [Fact]
    public async Task UpdateAsync_FullPerfectRun_FinishesMapsResultsAndMovesTheExerciseToReview()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        var states = new List<PracticeSessionState> { fixture.Practice.State };

        await fixture.PlayAsync(skippedPromptIndex: null, states);

        Assert.Equal(
            [PracticeSessionState.CountingIn, PracticeSessionState.Running, PracticeSessionState.Finished],
            states.Distinct());
        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        Assert.Equal(fixture.Exercise.PromptCount, fixture.Exercise.PromptResults.Count);
        Assert.All(fixture.Exercise.PromptResults, result => Assert.True(result.IsFirstTryCorrect));
        Assert.False(fixture.Controller.IsRunning);
        Assert.False(fixture.Exercise.HasMissedPrompts);
        SightReadingSessionSummary? summary = fixture.Exercise.ConsumeCompletionSummary();
        Assert.Equal("playAlong", summary?.Pacing);
    }

    [Fact]
    public async Task UpdateAsync_SkippedNote_IsGradedMissedAndTheRunStillFinishes()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: 2, states: []);

        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        NoteReadingPromptResult skipped = fixture.Exercise.PromptResults[2];
        Assert.True(skipped.WasMissed);
        Assert.False(skipped.IsFirstTryCorrect);
        Assert.Equal(1, fixture.Exercise.PromptResults.Count(result => result.WasMissed));
        Assert.True(fixture.Exercise.HasMissedPrompts);
    }

    [Fact]
    public async Task UpdateAsync_LongSlowRun_KeepsEveryEarlyNote()
    {
        Fixture fixture = CreateFixture(tempoPulses: 40, promptCount: 16);
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: null, states: []);

        // 16 quarter notes at 40 pulses last 24 s, longer than the timeline retains finished notes.
        Assert.All(fixture.Exercise.PromptResults, result => Assert.True(result.IsFirstTryCorrect));
        Assert.Equal(16, fixture.Exercise.PromptResults.Count);
    }

    [Fact]
    public async Task UpdateAsync_RhythmOnlyMode_AcceptsAnyKey()
    {
        Fixture fixture = CreateFixture(mode: NoteReadingMode.RhythmOnly);
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: null, states: [], pitchOverride: new Pitch(NoteLetter.F, 1, 2));

        Assert.All(fixture.Exercise.PromptResults, result =>
        {
            Assert.True(result.IsFirstTryCorrect);
            Assert.Empty(result.WrongPlayedPitches);
        });
    }

    [Fact]
    public async Task UpdateAsync_PitchGradedModeWithWrongKeys_ReportsWrongPlayedPitches()
    {
        Fixture fixture = CreateFixture(mode: NoteReadingMode.PitchAndRhythm);
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: null, states: [], pitchOverride: new Pitch(NoteLetter.F, 1, 2));

        Assert.All(fixture.Exercise.PromptResults, result => Assert.False(result.IsPitchFirstTryCorrect));
    }

    [Fact]
    public async Task UpdateAsync_ModeChangedMidRun_GradesTheRunWithTheModeItStartedWith()
    {
        Fixture fixture = CreateFixture(mode: NoteReadingMode.RhythmOnly);
        await fixture.Controller.StartAsync(Tolerance);

        fixture.Exercise.SetMode(NoteReadingMode.PitchAndRhythm);
        await fixture.PlayAsync(skippedPromptIndex: 2, states: []);

        // In rhythm only a skipped note is an onset miss and no pitch miss; a pitch graded mode would count it as both.
        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        NoteReadingPromptResult skipped = fixture.Exercise.PromptResults[2];
        Assert.Equal(Verdict.Missed, skipped.OnsetVerdict);
        Assert.True(skipped.IsPitchFirstTryCorrect);
        Assert.Equal("RhythmOnly", fixture.Exercise.ConsumeCompletionSummary()?.Mode.ToString());
    }

    [Fact]
    public async Task UpdateAsync_PacingChangedMidRun_StillCompletesTheRunAsPlayAlong()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);

        fixture.Exercise.SetPacing(ExercisePacing.WaitForMe);
        await fixture.PlayAsync(skippedPromptIndex: 2, states: []);

        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        Assert.True(fixture.Exercise.PromptResults[2].WasMissed);
        Assert.Equal("playAlong", fixture.Exercise.ConsumeCompletionSummary()?.Pacing);
    }

    [Fact]
    public async Task BuildPlayAlongReviewVerdicts_CleanPromptsReleasedEarlyWhereHoldsAreNotGraded_AreCorrectLikeInWaitForMe()
    {
        // Pitch + rhythm does not grade how long a key is held, so a prompt played on time with the right key is clean
        // however soon it is released. The practice engine still calls an early release TooShort, which drew the clean
        // notes yellow instead of the green Wait for me gives a correct note.
        Fixture fixture = CreateFixture(mode: NoteReadingMode.PitchAndRhythm);
        await fixture.Controller.StartAsync(Tolerance);
        await fixture.PlayAsync(skippedPromptIndex: 2, states: [], holdFraction: 0.2);
        IReadOnlyDictionary<ScoreNote, Verdict> engineVerdicts = fixture.Practice.GetVisibleVerdicts();
        Assert.Contains(Verdict.TooShort, engineVerdicts.Values);

        IReadOnlyDictionary<ScoreNote, Verdict> verdicts = fixture.Exercise.BuildPlayAlongReviewVerdicts(engineVerdicts);

        for (int prompt = 0; prompt < fixture.Exercise.PromptResults.Count; prompt++)
        {
            NoteReadingPromptResult result = fixture.Exercise.PromptResults[prompt];
            Assert.All(
                result.ExpectedSourceNotes,
                note => Assert.Equal(prompt == 2 ? Verdict.Missed : Verdict.Correct, verdicts[note]));
        }
    }

    [Fact]
    public async Task BuildPlayAlongReviewVerdicts_PromptsThatWereNotClean_KeepTheEnginesColors()
    {
        // A held-too-short note in a mode that does grade holds is a mistake, so it stays the engine's TooShort color,
        // like a too-short note in Wait for me.
        Fixture fixture = CreateFixture(mode: NoteReadingMode.PitchHoldAndRhythm);
        await fixture.Controller.StartAsync(Tolerance);
        await fixture.PlayAsync(skippedPromptIndex: 2, states: [], holdFraction: 0.2);
        IReadOnlyDictionary<ScoreNote, Verdict> engineVerdicts = fixture.Practice.GetVisibleVerdicts();

        IReadOnlyDictionary<ScoreNote, Verdict> verdicts = fixture.Exercise.BuildPlayAlongReviewVerdicts(engineVerdicts);

        Assert.Equal(engineVerdicts.OrderBy(pair => pair.Key.BeatOffset).Select(pair => pair.Value), verdicts.OrderBy(pair => pair.Key.BeatOffset).Select(pair => pair.Value));
        Assert.Contains(Verdict.TooShort, verdicts.Values);
        Assert.Contains(Verdict.Missed, verdicts.Values);
        Assert.DoesNotContain(Verdict.Correct, verdicts.Values);
    }

    [Fact]
    public async Task BuildPlayAlongReviewVerdicts_PerfectRun_IsAllCorrectAndLeavesTheEnginesDictionaryAlone()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        await fixture.PlayAsync(skippedPromptIndex: null, states: []);
        IReadOnlyDictionary<ScoreNote, Verdict> engineVerdicts = fixture.Practice.GetVisibleVerdicts();
        int engineCount = engineVerdicts.Count;

        IReadOnlyDictionary<ScoreNote, Verdict> verdicts = fixture.Exercise.BuildPlayAlongReviewVerdicts(engineVerdicts);

        Assert.Equal(engineCount, verdicts.Count);
        Assert.All(verdicts.Values, verdict => Assert.Equal(Verdict.Correct, verdict));
    }

    [Fact]
    public async Task BuildPlayAlongReviewVerdicts_WhileTheRunIsStillGoing_ReturnsTheEnginesVerdictsUnchanged()
    {
        Fixture fixture = CreateFixture(mode: NoteReadingMode.PitchAndRhythm);
        await fixture.Controller.StartAsync(Tolerance);
        fixture.Audio.CurrentTime = fixture.Practice.PracticeAnchor + TimeSpan.FromSeconds(1.2);
        await fixture.Controller.UpdateAsync();
        IReadOnlyDictionary<ScoreNote, Verdict> engineVerdicts = fixture.Practice.GetVisibleVerdicts();

        Assert.Equal(SightReadingExercisePhase.Active, fixture.Exercise.Phase);
        Assert.Same(engineVerdicts, fixture.Exercise.BuildPlayAlongReviewVerdicts(engineVerdicts));
    }

    [Fact]
    public async Task VisibleCursorBeats_CountIn_HasNoCursorLine()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        fixture.Audio.CurrentTime = fixture.Practice.PracticeAnchor - TimeSpan.FromSeconds(1);
        await fixture.Controller.UpdateAsync();

        Assert.Equal(PracticeSessionState.CountingIn, fixture.Controller.State);
        Assert.Null(fixture.Controller.VisibleCursorBeats);
    }

    [Fact]
    public async Task VisibleCursorBeats_WhileTheRunIsGoing_FollowsThePracticeCursor()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        fixture.Audio.CurrentTime = fixture.Practice.PracticeAnchor + TimeSpan.FromSeconds(1.2);
        await fixture.Controller.UpdateAsync();

        Assert.Equal(PracticeSessionState.Running, fixture.Controller.State);
        Assert.Equal(fixture.Controller.CursorBeats, fixture.Controller.VisibleCursorBeats);
        Assert.True(fixture.Controller.VisibleCursorBeats > 0);
    }

    [Fact]
    public async Task VisibleCursorBeats_OnceTheRunHasFinishedAndTheExerciseIsInReview_HasNoCursorLine()
    {
        // The line used to stay at the end of the score in review.
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);

        await fixture.PlayAsync(skippedPromptIndex: null, states: []);

        Assert.Equal(PracticeSessionState.Finished, fixture.Controller.State);
        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
        Assert.Null(fixture.Controller.VisibleCursorBeats);
    }

    [Fact]
    public async Task AbortAsync_MidRun_ReturnsToIdleWithoutCompletingTheExercise()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        fixture.Audio.CurrentTime = fixture.Practice.PracticeAnchor + TimeSpan.FromSeconds(1);
        await fixture.Controller.UpdateAsync();

        await fixture.Controller.AbortAsync();

        Assert.Equal(PracticeSessionState.Idle, fixture.Practice.State);
        Assert.False(fixture.Controller.IsRunning);
        Assert.Equal(SightReadingExercisePhase.Active, fixture.Exercise.Phase);
        Assert.Empty(fixture.Exercise.PromptResults);
        Assert.Empty(fixture.Controller.GetNextPitches());
        Assert.Null(fixture.Exercise.ConsumeCompletionSummary());
    }

    [Fact]
    public async Task GetNextPitches_WhileRunning_ExposesTheUpcomingPrompt()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        ScoreEvent first = ScoreDerivation.Flatten(fixture.Exercise.Score!)[0];

        Assert.Equal([first.Pitch], fixture.Controller.GetNextPitches());

        fixture.Audio.CurrentTime = fixture.Practice.PracticeAnchor + TimeSpan.FromSeconds(0.4);
        await fixture.Controller.UpdateAsync();
        ScoreEvent second = ScoreDerivation.Flatten(fixture.Exercise.Score!)[1];

        Assert.Equal([second.Pitch], fixture.Controller.GetNextPitches());
    }

    [Fact]
    public async Task StartAsync_WithoutExerciseOrWithWaitForMePacing_Throws()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var practice = new BrowserPracticeCoordinator(audio, new NoteTimeline());
        var exercise = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var controller = new ExercisePlayAlongController(
            practice,
            exercise,
            new ExerciseClick(new BrowserMetronome(new FakeMetronomeAudio(audio))));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await controller.StartAsync(Tolerance));

        exercise.SetMode(NoteReadingMode.PitchAndOrder);
        exercise.SetPacing(ExercisePacing.PlayAlong);
        exercise.Generate(new Random(3), Tolerance);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await controller.StartAsync(Tolerance));
        Assert.Equal(PracticeSessionState.Idle, practice.State);
    }

    [Fact]
    public async Task UpdateAsync_AfterCompletion_DoesNotCompleteTheExerciseTwice()
    {
        Fixture fixture = CreateFixture();
        await fixture.Controller.StartAsync(Tolerance);
        await fixture.PlayAsync(skippedPromptIndex: null, states: []);
        fixture.Exercise.ConsumeCompletionSummary();

        bool stillRunning = await fixture.Controller.UpdateAsync();

        Assert.False(stillRunning);
        Assert.Null(fixture.Exercise.ConsumeCompletionSummary());
        Assert.Equal(SightReadingExercisePhase.Review, fixture.Exercise.Phase);
    }

    private static Fixture CreateFixture(
        NoteReadingMode mode = NoteReadingMode.PitchHoldAndRhythm,
        int tempoPulses = 120,
        int promptCount = 8)
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var practice = new BrowserPracticeCoordinator(audio, timeline);
        var metronomeAudio = new FakeMetronomeAudio(audio);
        var metronome = new BrowserMetronome(metronomeAudio);
        var click = new ExerciseClick(metronome);
        var exercise = new SightReadingExerciseCoordinator(new NoteReadingSession());
        exercise.SetMode(mode);
        exercise.SetPacing(ExercisePacing.PlayAlong);
        exercise.SetTempoPulsesPerMinute(tempoPulses);
        exercise.SetPromptCountOption(promptCount);
        exercise.Generate(new Random(5), Tolerance);
        return new Fixture(
            audio,
            timeline,
            practice,
            exercise,
            new ExercisePlayAlongController(practice, exercise, click),
            metronomeAudio,
            metronome,
            click);
    }

    private sealed record Fixture(
        FakePracticeAudio Audio,
        NoteTimeline Timeline,
        BrowserPracticeCoordinator Practice,
        SightReadingExerciseCoordinator Exercise,
        ExercisePlayAlongController Controller,
        FakeMetronomeAudio MetronomeAudio,
        BrowserMetronome Metronome,
        ExerciseClick Click)
    {
        /// <summary>Plays the exercise on time, one tick per half beat, optionally skipping a prompt.</summary>
        internal async Task PlayAsync(
            int? skippedPromptIndex,
            List<PracticeSessionState> states,
            Pitch? pitchOverride = null,
            double holdFraction = 1.0)
        {
            Score score = Exercise.Score!;
            IReadOnlyList<IReadOnlyList<ScoreEvent>> prompts =
                ScoreDerivation.GroupByOnset(ScoreDerivation.Flatten(score));
            TimeSpan anchor = Practice.PracticeAnchor;
            TimeSpan beat = MusicalTime.BeatsToDuration(1, score.Tempo);
            var pending = new List<(TimeSpan Start, TimeSpan Release, Pitch Pitch)>();
            for (int index = 0; index < prompts.Count; index++)
            {
                if (index == skippedPromptIndex)
                {
                    continue;
                }

                foreach (ScoreEvent scoreEvent in prompts[index])
                {
                    TimeSpan start = anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, score.Tempo);
                    pending.Add((start, start + (MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, score.Tempo) * holdFraction), pitchOverride ?? scoreEvent.Pitch));
                }
            }

            TimeSpan end = anchor + MusicalTime.BeatsToDuration(prompts[^1][0].OnsetBeats + 2, score.Tempo) + TimeSpan.FromSeconds(1);
            var started = new Dictionary<int, PerformedNote>();
            for (TimeSpan now = Audio.CurrentTime; now <= end; now += beat / 2)
            {
                Audio.CurrentTime = now;
                for (int index = 0; index < pending.Count; index++)
                {
                    if (!started.ContainsKey(index) && pending[index].Start <= now)
                    {
                        started[index] = Timeline.Start(pending[index].Pitch, pending[index].Start);
                    }
                    else if (started.TryGetValue(index, out PerformedNote? note) &&
                        note.ReleaseTime is null &&
                        pending[index].Release <= now)
                    {
                        Timeline.Complete(note, pending[index].Release);
                    }
                }

                bool running = await Controller.UpdateAsync();
                states.Add(Practice.State);
                if (!running)
                {
                    break;
                }
            }
        }
    }

    private sealed class FakeMetronomeAudio(FakePracticeAudio clock) : IBrowserMetronomeAudio
    {
        internal bool IsRunning { get; private set; }

        internal TimeSpan? Anchor { get; private set; }

        internal TimeSpan? BeatDuration { get; private set; }

        internal int? BeatsPerMeasure { get; private set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            clock.GetCurrentTimeAsync(cancellationToken);

        public ValueTask StartMetronomeAsync(
            TimeSpan anchor,
            TimeSpan beatDuration,
            int beatsPerMeasure,
            int beatsPerGroup,
            CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            Anchor = anchor;
            BeatDuration = beatDuration;
            BeatsPerMeasure = beatsPerMeasure;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePracticeAudio(TimeSpan currentTime) : IBrowserScoreAudio
    {
        internal TimeSpan CurrentTime { get; set; } = currentTime;

        internal IReadOnlyList<BrowserScoreAudioEvent> ScheduledEvents { get; private set; } = [];

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CurrentTime);

        public ValueTask ScheduleScoreAsync(
            IReadOnlyList<BrowserScoreAudioEvent> events,
            CancellationToken cancellationToken = default)
        {
            ScheduledEvents = events;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopScoreAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
