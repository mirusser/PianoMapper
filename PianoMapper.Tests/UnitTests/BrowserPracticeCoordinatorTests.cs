using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Playback;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserPracticeCoordinatorTests
{
    [Fact]
    public async Task StartAsync_Score_SchedulesCountInOnAudioClock()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var coordinator = new BrowserPracticeCoordinator(audio, new NoteTimeline());
        var score = CreateScore();

        await coordinator.StartAsync(score);

        Assert.Equal(PracticeSessionState.CountingIn, coordinator.State);
        Assert.Equal(TimeSpan.FromSeconds(12.05), coordinator.PracticeAnchor);
        Assert.Collection(
            audio.ScheduledEvents,
            scoreEvent => Assert.Equal(TimeSpan.FromSeconds(10.05), scoreEvent.StartTime),
            scoreEvent => Assert.Equal(TimeSpan.FromSeconds(10.55), scoreEvent.StartTime),
            scoreEvent => Assert.Equal(TimeSpan.FromSeconds(11.05), scoreEvent.StartTime),
            scoreEvent => Assert.Equal(TimeSpan.FromSeconds(11.55), scoreEvent.StartTime));
    }

    [Fact]
    public async Task UpdateAsync_RunningWithCorrectNote_ReturnsCorrectGrading()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var coordinator = new BrowserPracticeCoordinator(audio, timeline);
        await coordinator.StartAsync(CreateScore());
        var performed = timeline.Start(new Pitch(NoteLetter.C, 0, 4), coordinator.PracticeAnchor);
        timeline.Complete(performed, coordinator.PracticeAnchor + TimeSpan.FromSeconds(0.5));
        audio.CurrentTime = performed.ReleaseTime!.Value;

        await coordinator.UpdateAsync();

        Assert.Equal(PracticeSessionState.Running, coordinator.State);
        Assert.Equal(1, coordinator.Result?.Summary.Counts[Verdict.Correct]);
        Assert.Equal(100, coordinator.Result?.Summary.AccuracyPercent);
    }

    [Fact]
    public async Task StartAsync_CustomOnTimeTolerance_UsesOptionsForGrading()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var coordinator = new BrowserPracticeCoordinator(audio, timeline);
        var options = new GradingOptions { OnTimeTolerance = TimeSpan.FromMilliseconds(100) };
        await coordinator.StartAsync(CreateScore(), options);
        TimeSpan startTime = coordinator.PracticeAnchor + TimeSpan.FromMilliseconds(80);
        var performed = timeline.Start(new Pitch(NoteLetter.C, 0, 4), startTime);
        timeline.Complete(performed, startTime + TimeSpan.FromSeconds(0.5));
        audio.CurrentTime = performed.ReleaseTime!.Value;

        await coordinator.UpdateAsync();

        var result = Assert.IsType<GradingResult>(coordinator.Result);
        Assert.Equal(Verdict.Correct, Assert.Single(result.Events).Verdict);
    }

    [Fact]
    public async Task AbortAndStartAsync_ActiveSession_AbortsAndRetriesFromNewClockAnchor()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var coordinator = new BrowserPracticeCoordinator(audio, new NoteTimeline());
        var score = CreateScore();
        await coordinator.StartAsync(score);

        await coordinator.AbortAsync();
        audio.CurrentTime = TimeSpan.FromSeconds(20);
        await coordinator.StartAsync(score);

        Assert.Equal(PracticeSessionState.CountingIn, coordinator.State);
        Assert.Equal(TimeSpan.FromSeconds(22.05), coordinator.PracticeAnchor);
        Assert.Equal(3, audio.StopCount);
    }

    [Fact]
    public async Task GetNextPitches_BeforeStartCountInAndAfterAbort_FollowsSessionLifecycle()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var coordinator = new BrowserPracticeCoordinator(audio, new NoteTimeline());
        Assert.Empty(coordinator.GetNextPitches());

        await coordinator.StartAsync(CreateScore());
        Assert.Equal([60], coordinator.GetNextPitches().Select(pitch => pitch.MidiNumber));

        await coordinator.AbortAsync();
        Assert.Empty(coordinator.GetNextPitches());
    }

    [Fact]
    public async Task StartAsync_WithoutCountInClicks_SchedulesNoClicksButKeepsTheAnchor()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var coordinator = new BrowserPracticeCoordinator(audio, new NoteTimeline());

        await coordinator.StartAsync(
            CreateScore(),
            new GradingOptions(),
            new PracticeRunOptions { ScheduleCountInClicks = false });

        Assert.Equal(PracticeSessionState.CountingIn, coordinator.State);
        Assert.Equal(TimeSpan.FromSeconds(12.05), coordinator.PracticeAnchor);
        Assert.Empty(audio.ScheduledEvents);
    }

    [Fact]
    public async Task UpdateAsync_LongRun_GradesNotesTheTimelineHasPruned()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var coordinator = new BrowserPracticeCoordinator(audio, timeline);
        Score score = CreateLongScore(noteCount: 20);
        await coordinator.StartAsync(score);

        await PlayPerfectly(coordinator, audio, timeline, noteCount: 20);

        Assert.Equal(PracticeSessionState.Finished, coordinator.State);
        Assert.Equal(20, coordinator.Result?.Summary.Counts[Verdict.Correct]);
        Assert.Equal(0, coordinator.Result?.Summary.Counts[Verdict.Missed]);
    }

    [Fact]
    public async Task UpdateAsync_LongRunWithUnplayedNotes_StillGradesThoseNotesMissed()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var coordinator = new BrowserPracticeCoordinator(audio, timeline);
        await coordinator.StartAsync(CreateLongScore(noteCount: 20));

        // Notes 4 and 5 are a gap in the performance and note 13 is a lone missed note; all three are older than the
        // timeline's retention window by the time the run finishes, as are the notes that were played.
        await PlayPerfectly(coordinator, audio, timeline, noteCount: 20, skippedNoteIndexes: [4, 5, 13]);

        Assert.Equal(PracticeSessionState.Finished, coordinator.State);
        Assert.Equal(17, coordinator.Result?.Summary.Counts[Verdict.Correct]);
        Assert.Equal(3, coordinator.Result?.Summary.Counts[Verdict.Missed]);
        Assert.Equal(0, coordinator.Result?.Summary.Counts[Verdict.Extra]);
    }

    [Fact]
    public async Task AbortAsync_CapturedNotes_AreNotCarriedIntoTheNextRun()
    {
        var audio = new FakePracticeAudio(TimeSpan.FromSeconds(10));
        var timeline = new NoteTimeline();
        var coordinator = new BrowserPracticeCoordinator(audio, timeline);
        Score score = CreateScore();
        await coordinator.StartAsync(score);
        var oldNote = timeline.Start(new Pitch(NoteLetter.C, 0, 4), coordinator.PracticeAnchor);
        timeline.Complete(oldNote, coordinator.PracticeAnchor + TimeSpan.FromSeconds(0.5));
        audio.CurrentTime = oldNote.ReleaseTime!.Value;
        await coordinator.UpdateAsync();
        await coordinator.AbortAsync();
        timeline.Remove([oldNote]);

        audio.CurrentTime = TimeSpan.FromSeconds(40);
        await coordinator.StartAsync(score);
        audio.CurrentTime = coordinator.PracticeAnchor + TimeSpan.FromSeconds(3);
        await coordinator.UpdateAsync();

        Assert.Equal(1, coordinator.Result?.Summary.Counts[Verdict.Missed]);
        Assert.Equal(0, coordinator.Result?.Summary.Counts[Verdict.Extra]);
    }

    internal static Score CreateLongScore(int noteCount) =>
        new(
            "long practice",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            Enumerable.Range(0, (noteCount + 3) / 4)
                .Select(measureIndex => new ScoreMeasure(
                    Enumerable.Range(0, 4)
                        .Where(beat => (measureIndex * 4) + beat < noteCount)
                        .Select(beat => new ScoreNote(
                            new Pitch(NoteLetter.C, 0, 4),
                            new NoteValue(4),
                            measureIndex,
                            beat,
                            Staff.Treble))
                        .ToArray(),
                    []))
                .ToArray());

    private static async Task PlayPerfectly(
        BrowserPracticeCoordinator coordinator,
        FakePracticeAudio audio,
        NoteTimeline timeline,
        int noteCount,
        IReadOnlyCollection<int>? skippedNoteIndexes = null)
    {
        TimeSpan anchor = coordinator.PracticeAnchor;
        for (int index = 0; index < noteCount; index++)
        {
            TimeSpan start = anchor + TimeSpan.FromSeconds(index);
            audio.CurrentTime = start;
            await coordinator.UpdateAsync();
            if (skippedNoteIndexes?.Contains(index) == true)
            {
                continue;
            }

            var performed = timeline.Start(new Pitch(NoteLetter.C, 0, 4), start);
            audio.CurrentTime = start + TimeSpan.FromSeconds(0.5);
            timeline.Complete(performed, audio.CurrentTime);
            await coordinator.UpdateAsync();
        }

        audio.CurrentTime = anchor + TimeSpan.FromSeconds(noteCount + 2);
        await coordinator.UpdateAsync();
    }

    private static Score CreateScore() =>
        new(
            "practice",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)],
                    []),
            ]);

    private sealed class FakePracticeAudio(TimeSpan currentTime) : IBrowserScoreAudio
    {
        internal TimeSpan CurrentTime { get; set; } = currentTime;

        internal IReadOnlyList<BrowserScoreAudioEvent> ScheduledEvents { get; private set; } = [];

        internal int StopCount { get; private set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CurrentTime);

        public ValueTask ScheduleScoreAsync(
            IReadOnlyList<BrowserScoreAudioEvent> events,
            CancellationToken cancellationToken = default)
        {
            ScheduledEvents = events;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopScoreAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return ValueTask.CompletedTask;
        }
    }
}
