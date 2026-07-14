using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class NoteColorsTests
{
    [Fact]
    public void GetColor_EnharmonicPitches_ReturnsSameColor()
    {
        var firstColor = NoteColors.GetColor(new Pitch(NoteLetter.C, 1, 4));
        var secondColor = NoteColors.GetColor(new Pitch(NoteLetter.D, -1, 4));

        Assert.Equal(firstColor, secondColor);
    }

    [Fact]
    public void GetColor_AllTwelvePitchClasses_ReturnsDistinctColors()
    {
        Pitch[] pitches =
        [
            new(NoteLetter.C, 0, 4),
            new(NoteLetter.C, 1, 4),
            new(NoteLetter.D, 0, 4),
            new(NoteLetter.D, 1, 4),
            new(NoteLetter.E, 0, 4),
            new(NoteLetter.F, 0, 4),
            new(NoteLetter.F, 1, 4),
            new(NoteLetter.G, 0, 4),
            new(NoteLetter.G, 1, 4),
            new(NoteLetter.A, 0, 4),
            new(NoteLetter.A, 1, 4),
            new(NoteLetter.B, 0, 4),
        ];

        var colors = pitches
            .Select(NoteColors.GetColor)
            .Select(color => (color[0], color[1], color[2]))
            .ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

}
