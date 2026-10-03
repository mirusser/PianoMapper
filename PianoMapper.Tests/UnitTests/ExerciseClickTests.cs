using PianoMapper.Music;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Playback;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ExerciseClickTests
{
    [Fact]
    public async Task StartAsync_Score_ClicksAtTheScoresMeterAndTempo()
    {
        var audio = new FakeMetronomeAudio();
        var metronome = new BrowserMetronome(audio);
        var click = new ExerciseClick(metronome);
        Score score = CreateScore(new TimeSignature(6, new NoteValue(8)), new Tempo(120));

        await click.StartAsync(score);

        Assert.True(click.IsRunning);
        Assert.Equal(score.TimeSignature, metronome.Grid?.TimeSignature);
        Assert.Equal(score.Tempo, metronome.Grid?.Tempo);
        Assert.Equal(6, audio.BeatsPerMeasure);
        Assert.Equal(TimeSpan.FromMilliseconds(500), audio.BeatDuration);
    }

    [Fact]
    public async Task StartAsync_ExplicitAnchor_ClicksFromThatAnchor()
    {
        var audio = new FakeMetronomeAudio();
        var metronome = new BrowserMetronome(audio);
        var click = new ExerciseClick(metronome);
        var anchor = TimeSpan.FromSeconds(42.5);

        await click.StartAsync(CreateScore(new TimeSignature(4, new NoteValue(4)), new Tempo(90)), anchor);

        Assert.True(click.IsRunning);
        Assert.Equal(anchor, metronome.Grid?.Anchor);
    }

    [Fact]
    public async Task StopAsync_ClickItStarted_StopsTheMetronome()
    {
        var audio = new FakeMetronomeAudio();
        var metronome = new BrowserMetronome(audio);
        var click = new ExerciseClick(metronome);
        await click.StartAsync(CreateScore(new TimeSignature(4, new NoteValue(4)), new Tempo(60)));

        await click.StopAsync();

        Assert.False(click.IsRunning);
        Assert.False(metronome.IsRunning);
        Assert.Equal(1, audio.StopCount);
    }

    [Fact]
    public async Task StopAsync_NothingStartedByTheExercise_LeavesAManualMetronomeRunning()
    {
        var audio = new FakeMetronomeAudio();
        var metronome = new BrowserMetronome(audio);
        var click = new ExerciseClick(metronome);
        await metronome.StartAsync(new TimeSignature(4, new NoteValue(4)), new Tempo(100));

        await click.StopAsync();

        Assert.True(metronome.IsRunning);
        Assert.Equal(0, audio.StopCount);
    }

    [Fact]
    public async Task StopAsync_MetronomeRestartedManuallyAfterTheClick_LeavesTheNewMetronomeRunning()
    {
        var audio = new FakeMetronomeAudio();
        var metronome = new BrowserMetronome(audio);
        var click = new ExerciseClick(metronome);
        await click.StartAsync(CreateScore(new TimeSignature(4, new NoteValue(4)), new Tempo(60)));
        await metronome.StopAsync();
        await metronome.StartAsync(new TimeSignature(3, new NoteValue(4)), new Tempo(100));

        await click.StopAsync();

        Assert.False(click.IsRunning);
        Assert.True(metronome.IsRunning);
    }

    [Fact]
    public async Task StopAsync_CalledTwice_StopsTheMetronomeOnlyOnce()
    {
        var audio = new FakeMetronomeAudio();
        var click = new ExerciseClick(new BrowserMetronome(audio));
        await click.StartAsync(CreateScore(new TimeSignature(4, new NoteValue(4)), new Tempo(60)));

        await click.StopAsync();
        await click.StopAsync();

        Assert.Equal(1, audio.StopCount);
    }

    private static Score CreateScore(TimeSignature timeSignature, Tempo tempo) =>
        new("test", timeSignature, tempo, 0, []);

    private sealed class FakeMetronomeAudio : IBrowserMetronomeAudio
    {
        internal TimeSpan? BeatDuration { get; private set; }

        internal int? BeatsPerMeasure { get; private set; }

        internal int StopCount { get; private set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(TimeSpan.FromSeconds(10));

        public ValueTask StartMetronomeAsync(
            TimeSpan anchor,
            TimeSpan beatDuration,
            int beatsPerMeasure,
            int beatsPerGroup,
            CancellationToken cancellationToken = default)
        {
            BeatDuration = beatDuration;
            BeatsPerMeasure = beatsPerMeasure;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return ValueTask.CompletedTask;
        }
    }
}
