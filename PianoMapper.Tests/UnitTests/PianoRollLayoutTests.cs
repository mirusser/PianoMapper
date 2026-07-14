using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class PianoRollLayoutTests
{
    private static PerformedNote CreateNote(Pitch pitch, double startSeconds, float duration) =>
        new()
        {
            Pitch = pitch,
            StartTime = TimeSpan.FromSeconds(startSeconds),
            ReleaseTime = TimeSpan.FromSeconds(startSeconds + duration),
        };

    [Fact]
    public void GetBarRect_NoteEndedBeforeVisibleWindow_ReturnsNull()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 0, duration: 1f);
        var now = TimeSpan.FromSeconds(1 + PianoRollLayout.RollingWindowSeconds + 1);

        var rect = PianoRollLayout.GetBarRect(note, now);

        Assert.Null(rect);
    }

    [Fact]
    public void GetBarRect_NoteJustStarted_RightEdgeAtNewestTimeColumn()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 5, duration: 1f);
        var now = TimeSpan.FromSeconds(5);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        Assert.Equal(1f, rect.X1, 3);
        Assert.Equal(rect.X0, rect.X1, 3);
    }

    [Fact]
    public void GetBarRect_NoteStillPlaying_WidthGrowsWithElapsedTime()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 0, duration: 4f);

        var halfway = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(2))!.Value;
        var laterButStillPlaying = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(3))!.Value;

        var halfwayWidth = halfway.X1 - halfway.X0;
        var laterWidth = laterButStillPlaying.X1 - laterButStillPlaying.X0;

        Assert.True(laterWidth > halfwayWidth, $"expected {laterWidth} > {halfwayWidth}");
    }

    [Fact]
    public void GetBarRect_OpenNote_WidthContinuesGrowingToNow()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.A, 0, 4),
            StartTime = TimeSpan.Zero,
        };

        var earlier = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(2))!.Value;
        var later = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(3))!.Value;

        Assert.True(later.X1 - later.X0 > earlier.X1 - earlier.X0);
    }

    [Fact]
    public void GetBarRect_NoteFinished_WidthStopsGrowingPastDuration()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 0, duration: 1f);

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
        var low = CreateNote(new Pitch(NoteLetter.A, 0, 3), startSeconds: 0, duration: 1f);
        var high = CreateNote(new Pitch(NoteLetter.A, 0, 5), startSeconds: 0, duration: 1f);

        var lowRect = PianoRollLayout.GetBarRect(low, now)!.Value;
        var highRect = PianoRollLayout.GetBarRect(high, now)!.Value;

        Assert.True(highRect.Y0 > lowRect.Y0);
    }

    [Fact]
    public void GetBarRect_ReferenceFrequency_CentersOnMiddleOfPianoRollBand()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 0, duration: 1f);
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
        var low = CreateNote(new Pitch(NoteLetter.A, 0, 0), startSeconds: 0, duration: 1f);
        var high = CreateNote(new Pitch(NoteLetter.C, 0, 8), startSeconds: 0, duration: 1f);

        var lowRect = PianoRollLayout.GetBarRect(low, now)!.Value;
        var highRect = PianoRollLayout.GetBarRect(high, now)!.Value;

        Assert.InRange((lowRect.Y0 + lowRect.Y1) / 2f, PianoRollLayout.BandY0, PianoRollLayout.BandY1);
        Assert.InRange((highRect.Y0 + highRect.Y1) / 2f, PianoRollLayout.BandY0, PianoRollLayout.BandY1);
    }

    [Theory]
    [InlineData((int)NoteLetter.C, 0, 1, 32.70)]
    [InlineData((int)NoteLetter.F, 1, 3, 184.98)]
    [InlineData((int)NoteLetter.A, 0, 4, 440.00)]
    [InlineData((int)NoteLetter.C, 0, 5, 523.20)]
    [InlineData((int)NoteLetter.B, 0, 7, 3950.18)]
    public void GetBarRect_MappedPitch_MatchesLegacyFrequencyPosition(
        int letterValue,
        int alter,
        int octave,
        double legacyFrequency)
    {
        var letter = (NoteLetter)letterValue;
        var note = CreateNote(new Pitch(letter, alter, octave), startSeconds: 0, duration: 1f);

        var rect = PianoRollLayout.GetBarRect(note, TimeSpan.FromSeconds(1))!.Value;

        double semitoneOffset = 12.0 * Math.Log2(legacyFrequency / 440.0);
        double normalized = Math.Clamp(semitoneOffset / 48.0, -1.0, 1.0);
        double bandMid = (PianoRollLayout.BandY0 + PianoRollLayout.BandY1) / 2.0;
        double bandHalfSpan = (PianoRollLayout.BandY1 - PianoRollLayout.BandY0) / 2.0;
        double expectedCenter = bandMid + (normalized * bandHalfSpan);
        double actualCenter = (rect.Y0 + rect.Y1) / 2.0;
        Assert.InRange(Math.Abs(actualCenter - expectedCenter), 0.0, 0.001);
    }

    [Fact]
    public void GetBarRect_NoteStartedBeforeVisibleWindow_ClampsLeftEdge()
    {
        var note = CreateNote(new Pitch(NoteLetter.A, 0, 4), startSeconds: 0, duration: 100f);
        var now = TimeSpan.FromSeconds(PianoRollLayout.RollingWindowSeconds + 50);

        var rect = PianoRollLayout.GetBarRect(note, now)!.Value;

        Assert.Equal(-1f, rect.X0, 3);
    }
}
