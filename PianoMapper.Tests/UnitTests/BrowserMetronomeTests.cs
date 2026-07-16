using PianoMapper.Music;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserMetronomeTests
{
    [Fact]
    public async Task StartAsync_Timing_AnchorsGridAndStartsAudio()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);
        var timeSignature = new TimeSignature(6, new NoteValue(8));
        var tempo = new Tempo(90);

        await metronome.StartAsync(timeSignature, tempo);

        Assert.True(metronome.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(10.05), metronome.Grid?.Anchor);
        Assert.Equal(timeSignature, metronome.Grid?.TimeSignature);
        Assert.Equal(tempo, metronome.Grid?.Tempo);
        Assert.Equal(TimeSpan.FromSeconds(10.05), audio.Anchor);
        Assert.Equal(TimeSpan.FromSeconds(2.0 / 3.0), audio.BeatDuration);
        Assert.Equal(6, audio.BeatsPerMeasure);
    }

    [Fact]
    public async Task StopAndStartAsync_RunningMetronome_ClearsAndReanchorsGrid()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);
        var timeSignature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        await metronome.StartAsync(timeSignature, tempo);

        await metronome.StopAsync();

        Assert.False(metronome.IsRunning);
        Assert.Null(metronome.Grid);
        Assert.Equal(1, audio.StopCount);

        audio.CurrentTime = TimeSpan.FromSeconds(20);
        await metronome.StartAsync(timeSignature, tempo);

        Assert.Equal(TimeSpan.FromSeconds(20.05), metronome.Grid?.Anchor);
        Assert.Equal(2, audio.StartCount);
    }

    private sealed class FakeMetronomeAudio(TimeSpan currentTime) : IBrowserMetronomeAudio
    {
        internal TimeSpan CurrentTime { get; set; } = currentTime;

        internal TimeSpan? Anchor { get; private set; }

        internal TimeSpan? BeatDuration { get; private set; }

        internal int? BeatsPerMeasure { get; private set; }

        internal int StartCount { get; private set; }

        internal int StopCount { get; private set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CurrentTime);

        public ValueTask StartMetronomeAsync(
            TimeSpan anchor,
            TimeSpan beatDuration,
            int beatsPerMeasure,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            Anchor = anchor;
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
