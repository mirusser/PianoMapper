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
