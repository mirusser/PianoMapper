using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserScorePlaybackTests
{
    [Fact]
    public async Task StartAsync_Score_SchedulesAllEventsAheadOfAudioClock()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateScore(beatOffset: 2);

        await playback.StartAsync(score);

        Assert.Equal(["stop", "schedule"], audio.Calls);
        var scheduledEvent = Assert.Single(audio.ScheduledEvents);
        Assert.Equal(TimeSpan.FromSeconds(11.05), scheduledEvent.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(0.5), scheduledEvent.Duration);
        Assert.True(playback.IsActive);
    }

    [Fact]
    public async Task GetCursorBeats_RunningAndCompleted_MapsAnchorAndUpdatesState()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        await playback.StartAsync(CreateScore(beatOffset: 2));

        double? runningCursor = playback.GetCursorBeats(TimeSpan.FromSeconds(10.55));
        double? completedCursor = playback.GetCursorBeats(TimeSpan.FromSeconds(11.56));

        Assert.Equal(1, runningCursor);
        Assert.NotNull(completedCursor);
        Assert.False(playback.IsActive);
    }

    [Fact]
    public async Task StartAndStopAsync_Restart_ReplacesScheduleAndClearsPlaybackState()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateScore(beatOffset: 0);
        await playback.StartAsync(score);

        await playback.StartAsync(score);
        await playback.StopAsync();

        Assert.Equal(["stop", "schedule", "stop", "schedule", "stop"], audio.Calls);
        Assert.False(playback.IsActive);
        Assert.Null(playback.GetCursorBeats(TimeSpan.FromSeconds(11)));
    }

    [Fact]
    public async Task GetStartedNotes_ScheduledScore_ReturnsOnlyNotesWhoseOnsetHasPassed()
    {
        var playback = new BrowserScorePlayback(new FakeScoreAudio(TimeSpan.FromSeconds(10)));
        await playback.StartAsync(CreateScore(beatOffset: 0));

        var beforeOnset = playback.GetStartedNotes(TimeSpan.FromSeconds(10));
        var afterOnset = playback.GetStartedNotes(TimeSpan.FromSeconds(10.1));

        Assert.Empty(beforeOnset);
        var note = Assert.Single(afterOnset);
        Assert.Equal(TimeSpan.FromSeconds(10.05), note.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(10.55), note.ReleaseTime);
    }

    private static Score CreateScore(double beatOffset) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, beatOffset, Staff.Treble)],
                    []),
            ]);

    private sealed class FakeScoreAudio(TimeSpan currentTime) : IBrowserScoreAudio
    {
        internal List<string> Calls { get; } = [];

        internal IReadOnlyList<BrowserScoreAudioEvent> ScheduledEvents { get; private set; } = [];

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(currentTime);

        public ValueTask ScheduleScoreAsync(
            IReadOnlyList<BrowserScoreAudioEvent> events,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("schedule");
            ScheduledEvents = events;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopScoreAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("stop");
            return ValueTask.CompletedTask;
        }
    }
}
