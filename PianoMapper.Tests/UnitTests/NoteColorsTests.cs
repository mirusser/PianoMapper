using PianoMapper.Rendering;

namespace PianoMapper.Tests.UnitTests;

public class NoteColorsTests
{
    [Theory]
    [InlineData("C3", "C4")]
    [InlineData("A#2", "A#7")]
    [InlineData("B0", "B8")]
    public void GetColor_SamePitchClassDifferentOctave_ReturnsSameColor(string first, string second)
    {
        var firstColor = NoteColors.GetColor(first);
        var secondColor = NoteColors.GetColor(second);

        Assert.Equal(firstColor, secondColor);
    }

    [Fact]
    public void GetColor_AllTwelvePitchClasses_ReturnsDistinctColors()
    {
        string[] pitchClasses = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

        var colors = pitchClasses
            .Select(pitchClass => NoteColors.GetColor(pitchClass + "4"))
            .Select(color => (color[0], color[1], color[2]))
            .ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-note")]
    [InlineData("4")]
    [InlineData("H4")]
    public void GetColor_MalformedOrEmptyInput_ReturnsFallbackWithoutThrowing(string noteName)
    {
        var color = NoteColors.GetColor(noteName);

        Assert.Equal(3, color.Length);
    }

    [Fact]
    public void GetColor_MalformedInput_DoesNotCollideWithAnyRealPitchClass()
    {
        string[] pitchClasses = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

        var fallback = NoteColors.GetColor("???");
        var pitchClassColors = pitchClasses.Select(pitchClass => NoteColors.GetColor(pitchClass + "4"));

        Assert.DoesNotContain(pitchClassColors, color => color[0] == fallback[0] && color[1] == fallback[1] && color[2] == fallback[2]);
    }
}
