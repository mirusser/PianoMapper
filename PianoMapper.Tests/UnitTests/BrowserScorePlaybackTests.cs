using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserScorePlaybackTests
{
    [Fact]
    public async Task StartAsync_Score_SchedulesEventsAsOffsetsFromTheAnchorTheAudioEnginePicks()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateScore(beatOffset: 2);

        TimeSpan anchor = await playback.StartAsync(score);

        Assert.Equal(["stop", "schedule"], audio.Calls);
        Assert.Equal(TimeSpan.FromMilliseconds(50), audio.ScheduledLead);
        var scheduledEvent = Assert.Single(audio.ScheduledEvents);
        Assert.Equal(TimeSpan.FromSeconds(1), scheduledEvent.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(0.5), scheduledEvent.Duration);
        Assert.Equal(TimeSpan.FromSeconds(10.05), anchor);
        Assert.True(playback.IsActive);
    }

    [Fact]
    public async Task StartAsync_AudioEnginePicksALaterAnchor_CursorAndNotesFollowIt()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10))
        {
            PickedAnchor = TimeSpan.FromSeconds(10.4),
        };
        var playback = new BrowserScorePlayback(audio);

        TimeSpan anchor = await playback.StartAsync(CreateScore(beatOffset: 2));

        Assert.Equal(TimeSpan.FromSeconds(10.4), anchor);
        ScoreCursorAnchor cursorAnchor = Assert.IsType<ScoreCursorAnchor>(playback.GetCursorAnchor());
        Assert.Equal(10.4, cursorAnchor.AnchorSeconds, 6);
        Assert.Equal(11.9, cursorAnchor.CompletionSeconds, 6);
        Assert.Empty(playback.GetStartedNotes(TimeSpan.FromSeconds(11.3)));
        PerformedNote note = Assert.Single(playback.GetStartedNotes(TimeSpan.FromSeconds(11.4)));
        Assert.Equal(TimeSpan.FromSeconds(11.4), note.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(11.9), note.ReleaseTime);
    }

    [Fact]
    public async Task StartAsync_OctaveShiftedScore_SchedulesSoundingFrequency()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = new MusicXmlScoreReader().Read(Fixture("octave-shift-8va.musicxml"));
        int shiftedEventIndex = ScoreDerivation.Flatten(score)
            .Select((scoreEvent, index) => (Event: scoreEvent, Index: index))
            .First(item => item.Event.SourceNotes[0].SoundingOctavesAboveNotated == 1)
            .Index;

        await playback.StartAsync(score);

        var shiftedAudioEvent = audio.ScheduledEvents[shiftedEventIndex];
        Assert.Equal(new Pitch(NoteLetter.D, 0, 5).Frequency, shiftedAudioEvent.Frequency);
        Assert.NotEqual(new Pitch(NoteLetter.D, 0, 4).Frequency, shiftedAudioEvent.Frequency);
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
    public async Task GetCursorAnchor_AfterStart_ReturnsAudioClockAnchorTempoAndCompletion()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);

        await playback.StartAsync(CreateScore(beatOffset: 2));
        var anchor = playback.GetCursorAnchor();

        Assert.NotNull(anchor);
        Assert.Equal(10.05, anchor.Value.AnchorSeconds, 6);
        Assert.Equal(120, anchor.Value.BeatsPerMinute);
        Assert.Equal(11.55, anchor.Value.CompletionSeconds, 6);
    }

    [Fact]
    public void GetCursorAnchor_BeforeStart_ReturnsNull()
    {
        var playback = new BrowserScorePlayback(new FakeScoreAudio(TimeSpan.FromSeconds(10)));

        Assert.Null(playback.GetCursorAnchor());
    }

    [Fact]
    public async Task GetCursorAnchor_AfterStop_ReturnsNull()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        await playback.StartAsync(CreateScore(beatOffset: 0));

        await playback.StopAsync();

        Assert.Null(playback.GetCursorAnchor());
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

    [Fact]
    public async Task PauseAsync_Playing_StopsAudioAndFreezesTheCursorAtTheCurrentBeat()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        await playback.StartAsync(CreateFourNoteScore());
        audio.Calls.Clear();

        await playback.PauseAsync(TimeSpan.FromSeconds(11.05));

        Assert.Equal(["stop"], audio.Calls);
        Assert.True(playback.IsPaused);
        Assert.True(playback.IsActive);
        Assert.Equal(2, playback.GetCursorAnchor()!.Value.PausedBeats);
        Assert.Equal(2, playback.GetCursorBeats(TimeSpan.FromSeconds(50)));
        Assert.Empty(playback.GetStartedNotes(TimeSpan.FromSeconds(50)));
    }

    [Fact]
    public async Task PauseAsync_NothingPlaying_DoesNothing()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);

        await playback.PauseAsync(TimeSpan.FromSeconds(11));

        Assert.Empty(audio.Calls);
        Assert.False(playback.IsPaused);
        Assert.False(playback.IsActive);
    }

    [Fact]
    public async Task ResumeAsync_PausedMidNote_SchedulesWhatIsLeftFromThePausedPosition()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateFourNoteScore();
        await playback.StartAsync(score);
        await playback.PauseAsync(TimeSpan.FromSeconds(10.8));
        audio.PickedAnchor = TimeSpan.FromSeconds(20.05);

        TimeSpan scoreStart = await playback.ResumeAsync(score);

        // 0.75 s in: the first note is over, the second has 0.25 s left (struck again), two more follow.
        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.75)],
            audio.ScheduledEvents.Select(scheduledEvent => scheduledEvent.StartTime));
        Assert.Equal(
            [TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5)],
            audio.ScheduledEvents.Select(scheduledEvent => scheduledEvent.Duration));
        // Beat zero lies 0.75 s before the resumed audio, so the cursor carries on from where it paused.
        Assert.Equal(TimeSpan.FromSeconds(19.3), scoreStart);
        Assert.False(playback.IsPaused);
        Assert.True(playback.IsActive);
        Assert.Equal(1.5, playback.GetCursorBeats(TimeSpan.FromSeconds(20.05)));
        ScoreCursorAnchor cursorAnchor = playback.GetCursorAnchor()!.Value;
        Assert.Null(cursorAnchor.PausedBeats);
        Assert.Equal(19.3, cursorAnchor.AnchorSeconds, 6);
        Assert.Equal(21.3, cursorAnchor.CompletionSeconds, 6);
        Assert.Equal(TimeSpan.FromSeconds(20.05), Assert.Single(playback.GetStartedNotes(TimeSpan.FromSeconds(20.1))).StartTime);
    }

    [Fact]
    public async Task ResumeAsync_PausedBeforeTheFirstNote_SchedulesTheWholeScore()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateFourNoteScore();
        await playback.StartAsync(score);
        await playback.PauseAsync(TimeSpan.FromSeconds(10));

        TimeSpan scoreStart = await playback.ResumeAsync(score);

        Assert.Equal(4, audio.ScheduledEvents.Count);
        Assert.Equal(TimeSpan.FromSeconds(10.05), scoreStart);
    }

    [Fact]
    public async Task StopAsync_Paused_EndsThePlaybackSession()
    {
        var playback = new BrowserScorePlayback(new FakeScoreAudio(TimeSpan.FromSeconds(10)));
        await playback.StartAsync(CreateFourNoteScore());
        await playback.PauseAsync(TimeSpan.FromSeconds(11));

        await playback.StopAsync();

        Assert.False(playback.IsPaused);
        Assert.False(playback.IsActive);
        Assert.Null(playback.GetCursorAnchor());
    }

    [Fact]
    public async Task StartAsync_Paused_RestartsFromTheBeginning()
    {
        var audio = new FakeScoreAudio(TimeSpan.FromSeconds(10));
        var playback = new BrowserScorePlayback(audio);
        var score = CreateFourNoteScore();
        await playback.StartAsync(score);
        await playback.PauseAsync(TimeSpan.FromSeconds(11));

        await playback.StartAsync(score);

        Assert.False(playback.IsPaused);
        Assert.Equal(4, audio.ScheduledEvents.Count);
        Assert.Equal(TimeSpan.Zero, audio.ScheduledEvents[0].StartTime);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

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

    // Four quarter notes at 120 beats per minute: one every half second, each lasting half a second.
    private static Score CreateFourNoteScore() =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    Enumerable.Range(0, 4)
                        .Select(beat => new ScoreNote(
                            new Pitch(NoteLetter.C, 0, 4),
                            new NoteValue(4),
                            0,
                            beat,
                            Staff.Treble))
                        .ToArray(),
                    []),
            ]);

    private sealed class FakeScoreAudio(TimeSpan currentTime) : IBrowserScoreAudio
    {
        internal List<string> Calls { get; } = [];

        internal IReadOnlyList<BrowserScoreAudioEvent> ScheduledEvents { get; private set; } = [];

        internal TimeSpan ScheduledLead { get; private set; }

        /// <summary>The anchor the audio engine reports; by default the clock plus the requested lead.</summary>
        internal TimeSpan? PickedAnchor { get; set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(currentTime);

        public ValueTask ScheduleScoreAsync(
            IReadOnlyList<BrowserScoreAudioEvent> events,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Score playback schedules ahead of the audio clock instead.");

        public ValueTask<TimeSpan> ScheduleScoreAheadAsync(
            IReadOnlyList<BrowserScoreAudioEvent> events,
            TimeSpan lead,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("schedule");
            ScheduledEvents = events;
            ScheduledLead = lead;
            return ValueTask.FromResult(PickedAnchor ?? currentTime + lead);
        }

        public ValueTask StopScoreAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("stop");
            return ValueTask.CompletedTask;
        }
    }
}
