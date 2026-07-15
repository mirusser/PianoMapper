using PianoMapper.Music;
using PianoMapper.Rendering;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoRollSceneBuilderTests
{
    [Fact]
    public void Build_ActiveNote_ReturnsCoreLayoutBar()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
        };
        TimeSpan currentTime = TimeSpan.FromSeconds(2);

        var scene = PianoRollSceneBuilder.Build([note], currentTime);

        var bar = Assert.Single(scene.Bars);
        Assert.Equal(PianoRollLayout.GetBarRect(note, currentTime), bar.Rect);
        Assert.True(bar.IsActive);
    }

    [Fact]
    public void Build_ReleasedNote_ReturnsClosedBar()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.E, 0, 4),
            StartTime = TimeSpan.FromSeconds(1),
            ReleaseTime = TimeSpan.FromSeconds(1.5),
        };

        var bar = Assert.Single(PianoRollSceneBuilder.Build([note], TimeSpan.FromSeconds(2)).Bars);

        Assert.False(bar.IsActive);
    }

    [Fact]
    public void Build_ExpiredNote_ReturnsNoBar()
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.E, 0, 4),
            StartTime = TimeSpan.Zero,
            ReleaseTime = TimeSpan.FromSeconds(1),
        };

        var scene = PianoRollSceneBuilder.Build([note], TimeSpan.FromSeconds(10));

        Assert.Empty(scene.Bars);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Build_ExtremePitch_ClampsBarToVisiblePitchBand(int octave)
    {
        var note = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, octave),
            StartTime = TimeSpan.FromSeconds(1),
        };

        var bar = Assert.Single(PianoRollSceneBuilder.Build([note], TimeSpan.FromSeconds(2)).Bars);

        Assert.InRange(bar.Rect.Y0, PianoRollLayout.BandY0 - 0.02f, PianoRollLayout.BandY1);
        Assert.InRange(bar.Rect.Y1, PianoRollLayout.BandY0, PianoRollLayout.BandY1 + 0.02f);
    }
}
