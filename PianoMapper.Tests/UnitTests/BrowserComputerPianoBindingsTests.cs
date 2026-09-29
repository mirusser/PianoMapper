using PianoMapper.Music;
using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserComputerPianoBindingsTests
{
    [Theory]
    [InlineData("KeyZ", NoteLetter.C, 0, 4)]
    [InlineData("KeyS", NoteLetter.C, 1, 4)]
    [InlineData("KeyX", NoteLetter.D, 0, 4)]
    [InlineData("KeyM", NoteLetter.B, 0, 4)]
    [InlineData("Comma", NoteLetter.C, 0, 5)]
    public void TryGetPitch_MappedCode_ReturnsPitchRelativeToBaseOctave(
        string code,
        NoteLetter expectedLetter,
        int expectedAlter,
        int expectedOctave)
    {
        bool found = BrowserComputerPianoBindings.TryGetPitch(code, baseOctave: 4, out Pitch pitch);

        Assert.True(found);
        Assert.Equal(new Pitch(expectedLetter, expectedAlter, expectedOctave), pitch);
    }

    [Fact]
    public void TryGetPitch_UnmappedCode_ReturnsFalse()
    {
        bool found = BrowserComputerPianoBindings.TryGetPitch("KeyQ", baseOctave: 4, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetPitch_DifferentBaseOctave_ShiftsResultingPitch()
    {
        BrowserComputerPianoBindings.TryGetPitch("KeyZ", baseOctave: 3, out Pitch lowerPitch);
        BrowserComputerPianoBindings.TryGetPitch("KeyZ", baseOctave: 5, out Pitch higherPitch);

        Assert.Equal(new Pitch(NoteLetter.C, 0, 3), lowerPitch);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 5), higherPitch);
    }

    [Fact]
    public void HandledCodes_AreAllDistinct()
    {
        var distinctCodes = BrowserComputerPianoBindings.HandledCodes.ToHashSet();

        Assert.Equal(BrowserComputerPianoBindings.HandledCodes.Count, distinctCodes.Count);
    }
}
