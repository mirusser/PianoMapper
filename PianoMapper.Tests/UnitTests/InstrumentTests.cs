using Microsoft.Extensions.Time.Testing;
using PianoMapper.Audio;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class InstrumentTests
{
    [Theory]
    [InlineData(1, 1.0f)]
    [InlineData(4, 0.5f)]
    public void CalculateGain_Polyphony_ReturnsInverseSquareRoot(int polyphony, float expected)
    {
        float gain = Instrument.CalculateGain(polyphony);

        Assert.Equal(expected, gain, 5);
    }

    [Fact]
    public void CalculateGain_IncreasingPolyphony_IsMonotoneNonIncreasing()
    {
        var gains = Enumerable.Range(1, 32).Select(Instrument.CalculateGain).ToArray();

        Assert.All(gains.Zip(gains.Skip(1)), pair => Assert.True(pair.First >= pair.Second));
    }

    [Fact]
    public void GetNaturalDuration_A4_ReturnsThreeSecondSustainCap()
    {
        var pitch = new Pitch(NoteLetter.A, 0, 4);

        var duration = Instrument.GetNaturalDuration(pitch);

        Assert.Equal(TimeSpan.FromSeconds(3), duration);
    }

    [Fact]
    public async Task NoteOn_NaturalDurationElapsed_CompletesAtSustainCap()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        using var audio = new InlineAudioDispatcher();
        using var instrument = new Instrument(timeline, time, audio);

        var playback = instrument.NoteOn(new Pitch(NoteLetter.A, 0, 4));
        time.Advance(TimeSpan.FromSeconds(3));
        await playback.Completion;
        instrument.NoteOff(playback.Note);

        Assert.Equal(TimeSpan.FromSeconds(3), playback.Note.ReleaseTime);
    }

    [Fact]
    public void NoteOn_SamePitchTwice_StartsDistinctNotes()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        using var audio = new InlineAudioDispatcher();
        using var instrument = new Instrument(timeline, time, audio);
        var pitch = new Pitch(NoteLetter.C, 0, 4);

        var first = instrument.NoteOn(pitch);
        var second = instrument.NoteOn(pitch);

        Assert.NotSame(first.Note, second.Note);
        Assert.Equal(2, timeline.Snapshot().Count);
    }

    [Fact]
    public async Task NoteOff_FakeTimeElapsed_RecordsMeasuredRelease()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        using var audio = new InlineAudioDispatcher();
        using var instrument = new Instrument(timeline, time, audio);
        var playback = instrument.NoteOn(new Pitch(NoteLetter.C, 0, 4));
        time.Advance(TimeSpan.FromMilliseconds(250));

        instrument.NoteOff(playback.Note);
        await playback.Completion;

        Assert.Equal(TimeSpan.FromMilliseconds(250), playback.Note.ReleaseTime);
    }

    [Fact]
    public async Task ClearAll_ThenRelease_RemainsANoOp()
    {
        var time = new FakeTimeProvider();
        var timeline = new NoteTimeline(time);
        using var audio = new InlineAudioDispatcher();
        using var instrument = new Instrument(timeline, time, audio);
        var playback = instrument.NoteOn(new Pitch(NoteLetter.C, 0, 4));

        instrument.ClearAll();
        await playback.Completion;
        instrument.NoteOff(playback.Note);
        time.Advance(Instrument.GetNaturalDuration(playback.Note.Pitch));

        Assert.Empty(timeline.Snapshot());
    }

    private sealed class InlineAudioDispatcher : IAudioDispatcher
    {
        public void Enqueue(Action action) => action();

        public void StartAudio(PerformedNote note, short[] samples, float gain)
        {
        }

        public void StopAudio(PerformedNote note)
        {
        }

        public void ClearActiveNotes(IReadOnlyCollection<PerformedNote> activeNotes)
        {
        }

        public bool TryGetSamples(PerformedNote note, out short[] samples)
        {
            samples = [];
            return false;
        }

        public void RequestSampleOffsetRefresh(PerformedNote note)
        {
        }

        public bool TryGetSampleOffset(PerformedNote note, out int sampleOffset)
        {
            sampleOffset = 0;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
