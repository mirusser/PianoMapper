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
        Assert.Equal(3, audio.BeatsPerGroup);
        Assert.Equal([0, 3], audio.GroupStartBeatIndices);
        Assert.Equal(1, audio.Volume.GetValueOrDefault(), 6);
        Assert.Equal(MetronomeTimbre.Sine, audio.Timbre);
    }

    [Fact]
    public async Task StartAsync_SimpleMeter_AccentsEveryBeatGroupOfOne()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);

        await metronome.StartAsync(new TimeSignature(4, new NoteValue(4)), new Tempo(120));

        Assert.Equal(4, audio.BeatsPerMeasure);
        Assert.Equal(1, audio.BeatsPerGroup);
        Assert.Equal([0], audio.GroupStartBeatIndices);
    }

    [Fact]
    public async Task StartAsync_ExplicitAnchor_UsesItVerbatimWithoutTheSchedulingLead()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);
        var timeSignature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(60);
        var anchor = TimeSpan.FromSeconds(10.05);

        await metronome.StartAsync(timeSignature, tempo, anchor);

        Assert.True(metronome.IsRunning);
        Assert.Equal(anchor, metronome.Grid?.Anchor);
        Assert.Equal(anchor, audio.Anchor);
        Assert.Equal(TimeSpan.FromSeconds(1), audio.BeatDuration);
        Assert.Equal(4, audio.BeatsPerMeasure);
        Assert.Equal(1, audio.BeatsPerGroup);
    }

    [Fact]
    public async Task StartAsync_CustomManualOptions_PassesResolvedGroupingAndSoundOptionsToAudio()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);
        var options = new MetronomeOptions
        {
            GroupLengths = [2, 3],
            Volume = 0.6,
            Timbre = MetronomeTimbre.Triangle,
        };

        await metronome.StartAsync(
            new TimeSignature(5, new NoteValue(8)),
            new Tempo(120),
            options: options);

        Assert.Equal([0, 2], audio.GroupStartBeatIndices);
        Assert.Equal(0.6, audio.Volume.GetValueOrDefault(), 6);
        Assert.Equal(MetronomeTimbre.Triangle, audio.Timbre);
    }

    [Fact]
    public async Task SetSoundAsync_RunningMetronome_UpdatesSoundWithoutRestartingTheGrid()
    {
        var audio = new FakeMetronomeAudio(TimeSpan.FromSeconds(10));
        var metronome = new BrowserMetronome(audio);
        await metronome.StartAsync(new TimeSignature(4, new NoteValue(4)), new Tempo(120));
        var grid = metronome.Grid;

        await metronome.SetSoundAsync(0.3, MetronomeTimbre.Triangle);

        Assert.Same(grid, metronome.Grid);
        Assert.Equal(1, audio.StartCount);
        Assert.Equal(1, audio.SetSoundCount);
        Assert.Equal(0.3, audio.Volume.GetValueOrDefault(), 6);
        Assert.Equal(MetronomeTimbre.Triangle, audio.Timbre);
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

        internal int? BeatsPerGroup { get; private set; }

        internal IReadOnlyList<int>? GroupStartBeatIndices { get; private set; }

        internal double? Volume { get; private set; }

        internal MetronomeTimbre? Timbre { get; private set; }

        internal int StartCount { get; private set; }

        internal int StopCount { get; private set; }

        internal int SetSoundCount { get; private set; }

        public ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CurrentTime);

        public ValueTask StartMetronomeAsync(
            TimeSpan anchor,
            TimeSpan beatDuration,
            int beatsPerMeasure,
            IReadOnlyList<int> groupStartBeatIndices,
            double volume,
            MetronomeTimbre timbre,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            Anchor = anchor;
            BeatDuration = beatDuration;
            BeatsPerMeasure = beatsPerMeasure;
            GroupStartBeatIndices = groupStartBeatIndices;
            BeatsPerGroup = groupStartBeatIndices.Count == 2
                ? groupStartBeatIndices[1]
                : 1;
            Volume = volume;
            Timbre = timbre;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask SetMetronomeSoundAsync(
            double volume,
            MetronomeTimbre timbre,
            CancellationToken cancellationToken = default)
        {
            SetSoundCount++;
            Volume = volume;
            Timbre = timbre;
            return ValueTask.CompletedTask;
        }
    }
}
