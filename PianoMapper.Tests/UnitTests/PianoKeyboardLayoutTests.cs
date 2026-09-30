using PianoMapper.Input;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PianoKeyboardLayoutTests
{
    [Theory]
    [InlineData(0, (int)NoteLetter.C, 0, 4)]
    [InlineData(1, (int)NoteLetter.C, 1, 4)]
    [InlineData(4, (int)NoteLetter.E, 0, 4)]
    [InlineData(6, (int)NoteLetter.F, 1, 4)]
    [InlineData(11, (int)NoteLetter.B, 0, 4)]
    [InlineData(12, (int)NoteLetter.C, 0, 5)]
    public void GetPitch_ChromaticOffset_ReturnsExpectedPitch(int offset, int letterValue, int alter, int octave)
    {
        var pitch = PianoKeyboardLayout.GetPitch(4, offset);

        Assert.Equal(new Pitch((NoteLetter)letterValue, alter, octave), pitch);
    }
}
