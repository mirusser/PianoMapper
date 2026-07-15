using PianoMapper.Input;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoKeyboardLayoutTests
{
    [Fact]
    public void GetPitch_ZeroOffset_ReturnsStartingOctaveC()
    {
        var pitch = PianoKeyboardLayout.GetPitch(4, 0);

        Assert.Equal(new Pitch(NoteLetter.C, 0, 4), pitch);
    }

    [Theory]
    [InlineData(1, (int)NoteLetter.C, 1)]
    [InlineData(4, (int)NoteLetter.E, 0)]
    [InlineData(6, (int)NoteLetter.F, 1)]
    [InlineData(11, (int)NoteLetter.B, 0)]
    public void GetPitch_ChromaticOffset_ReturnsExpectedPitch(int offset, int letterValue, int alter)
    {
        var pitch = PianoKeyboardLayout.GetPitch(4, offset);

        Assert.Equal(new Pitch((NoteLetter)letterValue, alter, 4), pitch);
    }

    [Fact]
    public void GetPitch_TwelveSemitones_ReturnsNextOctaveC()
    {
        var pitch = PianoKeyboardLayout.GetPitch(4, 12);

        Assert.Equal(new Pitch(NoteLetter.C, 0, 5), pitch);
    }
}
