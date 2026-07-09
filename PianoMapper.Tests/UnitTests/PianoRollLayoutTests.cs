using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class PianoRollLayoutTests
{
    private static NoteInstance CreateNote(float frequency, double startSeconds, float duration) =>
        new()
        {
            NoteName = "X",
            Frequency = frequency,
            StartTime = TimeSpan.FromSeconds(startSeconds),
            Duration = duration,
            Samples = [],
            SourceId = 0,
            BufferId = 0,
        };

    [Fact]
    public void GetBarRect_NoteEndedBeforeVisibleWindow_ReturnsNull()
    {
        var note = CreateNote(440f, startSeconds: 0, duration: 1f);
        var now = TimeSpan.FromSeconds(1 + PianoRollLayout.RollingWindowSeconds + 1);

        var rect = PianoRollLayout.GetBarRect(note, now);

        Assert.Null(rect);
    }

    [Fact]
    public void GetBarRect_NoteJustStarted_RightEdgeAtNewestTimeColumn()
    {
        var note = CreateNote(440f, startSeconds: 5, duration: 1f);
        var now = TimeSpan.FromSeconds(5);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        Assert.Equal(1f, rect.X1, 3);
        Assert.Equal(rect.X0, rect.X1, 3);
    }

    [Fact]
    public void GetBarRect_NoteStillPlaying_WidthGrowsWithElapsedTime()
    {
        var note = CreateNote(440f, startSeconds: 0, duration: 4f);

        var halfway = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(2))!.Value;
        var laterButStillPlaying = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(3))!.Value;

        var halfwayWidth = halfway.X1 - halfway.X0;
        var laterWidth = laterButStillPlaying.X1 - laterButStillPlaying.X0;

        Assert.True(laterWidth > halfwayWidth, $"expected {laterWidth} > {halfwayWidth}");
    }

    [Fact]
    public void GetBarRect_NoteFinished_WidthStopsGrowingPastDuration()
    {
        var note = CreateNote(440f, startSeconds: 0, duration: 1f);

        var atEnd = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(1))!.Value;
        var longAfterEnd = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(3))!.Value;

        var widthAtEnd = atEnd.X1 - atEnd.X0;
        var widthAfter = longAfterEnd.X1 - longAfterEnd.X0;

        Assert.Equal(widthAtEnd, widthAfter, 3);
    }

    [Fact]
    public void GetBarRect_HigherFrequency_RendersAboveLowerFrequency()
    {
        var now = TimeSpan.FromSeconds(1);
        var low = CreateNote(220f, startSeconds: 0, duration: 1f); // A3
        var high = CreateNote(880f, startSeconds: 0, duration: 1f); // A5

        var lowRect = PianoRollLayout.GetBarRect(low, now)!.Value;
        var highRect = PianoRollLayout.GetBarRect(high, now)!.Value;

        Assert.True(highRect.Y0 > lowRect.Y0);
    }

    [Fact]
    public void GetBarRect_ReferenceFrequency_CentersOnMiddleOfPianoRollBand()
    {
        var note = CreateNote(440f, startSeconds: 0, duration: 1f); // A4
        var now = TimeSpan.FromSeconds(1);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        var centerY = (rect.Y0 + rect.Y1) / 2f;
        var expectedCenter = (PianoRollLayout.BandY0 + PianoRollLayout.BandY1) / 2f;
        Assert.Equal(expectedCenter, centerY, 3);
    }

    [Fact]
    public void GetBarRect_AnyFrequency_CenterStaysWithinPianoRollBand()
    {
        var now = TimeSpan.FromSeconds(1);
        var low = CreateNote(20f, startSeconds: 0, duration: 1f);
        var high = CreateNote(20000f, startSeconds: 0, duration: 1f);

        var lowRect = PianoRollLayout.GetBarRect(low, now)!.Value;
        var highRect = PianoRollLayout.GetBarRect(high, now)!.Value;

        Assert.InRange((lowRect.Y0 + lowRect.Y1) / 2f, PianoRollLayout.BandY0, PianoRollLayout.BandY1);
        Assert.InRange((highRect.Y0 + highRect.Y1) / 2f, PianoRollLayout.BandY0, PianoRollLayout.BandY1);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-100f)]
    public void GetBarRect_NonPositiveFrequency_DoesNotProduceNaN(float frequency)
    {
        var note = CreateNote(frequency, startSeconds: 0, duration: 1f);
        var now = TimeSpan.FromSeconds(1);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        Assert.False(float.IsNaN(rect.Y0));
        Assert.False(float.IsNaN(rect.Y1));
    }

    [Fact]
    public void GetBarRect_NoteStartedBeforeVisibleWindow_ClampsLeftEdge()
    {
        var note = CreateNote(440f, startSeconds: 0, duration: 100f);
        var now = TimeSpan.FromSeconds(PianoRollLayout.RollingWindowSeconds + 50);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        Assert.Equal(-1f, rect.X0, 3);
    }
}
