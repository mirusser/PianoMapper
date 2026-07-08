namespace PianoMapper.Tests.UnitTests;

public class PlaybackPositionTests
{
    private static NoteInstance CreateNote(double startSeconds, float duration, int sampleCount) =>
        new()
        {
            NoteName = "X",
            Frequency = 440f,
            StartTime = TimeSpan.FromSeconds(startSeconds),
            Duration = duration,
            Samples = new short[sampleCount],
            SourceId = 0,
            BufferId = 0,
        };

    [Fact]
    public void IsNoteStillPlaying_ExactlyAtDuration_ReturnsTrue()
    {
        var note = CreateNote(startSeconds: 0, duration: 2f, sampleCount: 1);

        bool stillPlaying = PlaybackPosition.IsNoteStillPlaying(note, now: TimeSpan.FromSeconds(2));

        Assert.True(stillPlaying);
    }

    [Fact]
    public void IsNoteStillPlaying_PastDuration_ReturnsFalse()
    {
        var note = CreateNote(startSeconds: 0, duration: 2f, sampleCount: 1);

        bool stillPlaying = PlaybackPosition.IsNoteStillPlaying(note, now: TimeSpan.FromSeconds(2.01));

        Assert.False(stillPlaying);
    }

    [Fact]
    public void IsNoteStillPlaying_BeforeNoteStart_ReturnsTrue()
    {
        var note = CreateNote(startSeconds: 5, duration: 2f, sampleCount: 1);

        bool stillPlaying = PlaybackPosition.IsNoteStillPlaying(note, now: TimeSpan.FromSeconds(0));

        Assert.True(stillPlaying);
    }

    [Fact]
    public void EstimateSampleOffset_AtNoteStart_ReturnsZero()
    {
        var note = CreateNote(startSeconds: 1, duration: 2f, sampleCount: Consts.SampleRate * 2);

        var offset = PlaybackPosition.EstimateSampleOffset(note, now: TimeSpan.FromSeconds(1));

        Assert.Equal(0, offset);
    }

    [Fact]
    public void EstimateSampleOffset_PartwayThroughNote_ReturnsProportionalOffset()
    {
        var note = CreateNote(startSeconds: 0, duration: 2f, sampleCount: Consts.SampleRate * 2);

        var offset = PlaybackPosition.EstimateSampleOffset(note, now: TimeSpan.FromSeconds(1));

        Assert.Equal(Consts.SampleRate, offset);
    }

    [Fact]
    public void EstimateSampleOffset_PastBufferEnd_ClampsToLastIndex()
    {
        var note = CreateNote(startSeconds: 0, duration: 1f, sampleCount: 100);

        var offset = PlaybackPosition.EstimateSampleOffset(note, now: TimeSpan.FromSeconds(10));

        Assert.Equal(99, offset);
    }

    [Fact]
    public void EstimateSampleOffset_BeforeNoteStart_ClampsToZero()
    {
        var note = CreateNote(startSeconds: 5, duration: 1f, sampleCount: 100);

        var offset = PlaybackPosition.EstimateSampleOffset(note, now: TimeSpan.FromSeconds(0));

        Assert.Equal(0, offset);
    }

    [Fact]
    public void ExtractWindow_CenteredWithinBuffer_ReturnsExactSlice()
    {
        short[] samples = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 5, windowSize: 4);

        Assert.Equal<short>([13, 14, 15, 16], window);
    }

    [Fact]
    public void ExtractWindow_NearBufferStart_PadsWithZerosBeforeIndexZero()
    {
        short[] samples = [10, 11, 12, 13, 14];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 0, windowSize: 4);

        Assert.Equal<short>([0, 0, 10, 11], window);
    }

    [Fact]
    public void ExtractWindow_NearBufferEnd_PadsWithZerosPastLastIndex()
    {
        short[] samples = [10, 11, 12, 13, 14];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 4, windowSize: 4);

        Assert.Equal<short>([12, 13, 14, 0], window);
    }

    [Fact]
    public void ExtractWindow_AlwaysReturnsRequestedWindowSize()
    {
        short[] samples = [1, 2, 3];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 1, windowSize: 1024);

        Assert.Equal(1024, window.Length);
    }

    [Fact]
    public void ExtractWindow_CenterOffsetFarBeforeBuffer_ReturnsAllZeros()
    {
        short[] samples = [10, 11, 12, 13, 14];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: -1000, windowSize: 4);

        Assert.Equal<short>([0, 0, 0, 0], window);
    }

    [Fact]
    public void ExtractWindow_CenterOffsetFarPastBuffer_ReturnsAllZeros()
    {
        short[] samples = [10, 11, 12, 13, 14];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 1000, windowSize: 4);

        Assert.Equal<short>([0, 0, 0, 0], window);
    }

    [Fact]
    public void ExtractWindow_OddWindowSize_CentersWithOneExtraSampleAfter()
    {
        short[] samples = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19];

        var window = PlaybackPosition.ExtractWindow(samples, centerOffset: 5, windowSize: 5);

        Assert.Equal<short>([13, 14, 15, 16, 17], window);
    }
}
