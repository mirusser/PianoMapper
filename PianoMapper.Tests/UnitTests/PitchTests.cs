using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PitchTests
{
    [Fact]
    public void DerivedProperties_MiddleC_ReturnsKnownMidiAndFrequency()
    {
        var pitch = new Pitch(NoteLetter.C, 0, 4);

        Assert.Equal(60, pitch.MidiNumber);
        Assert.Equal(261.626, pitch.Frequency, 3);
    }

    [Fact]
    public void DerivedProperties_EnharmonicPitches_ShareSoundButKeepSpelling()
    {
        var cSharp = new Pitch(NoteLetter.C, 1, 4);
        var dFlat = new Pitch(NoteLetter.D, -1, 4);

        Assert.Equal(cSharp.MidiNumber, dFlat.MidiNumber);
        Assert.NotEqual(cSharp, dFlat);
        Assert.NotEqual(cSharp.DiatonicIndex, dFlat.DiatonicIndex);
    }

    [Theory]
    [InlineData((int)NoteLetter.C, 0, 4, "C4")]
    [InlineData((int)NoteLetter.F, 1, 3, "F#3")]
    [InlineData((int)NoteLetter.B, -1, 5, "Bb5")]
    [InlineData((int)NoteLetter.C, 2, 4, "Cx4")]
    [InlineData((int)NoteLetter.D, -2, 2, "Dbb2")]
    public void ToString_SpelledPitch_ReturnsScientificName(int letterValue, int alter, int octave, string expected)
    {
        var letter = (NoteLetter)letterValue;
        var pitch = new Pitch(letter, alter, octave);

        Assert.Equal(expected, pitch.ToString());
    }

    [Fact]
    public void TryParse_AllSingleAccidentalSpellings_RoundTrips()
    {
        foreach (var letter in Enum.GetValues<NoteLetter>())
        {
            for (int alter = -1; alter <= 1; alter++)
            {
                for (int octave = 0; octave <= 8; octave++)
                {
                    var expected = new Pitch(letter, alter, octave);

                    bool parsed = Pitch.TryParse(expected.ToString(), out var actual);

                    Assert.True(parsed);
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C")]
    [InlineData("H4")]
    [InlineData("4")]
    [InlineData("C#")]
    [InlineData("C###4")]
    [InlineData("C4x")]
    public void TryParse_MalformedInput_ReturnsFalse(string? value)
    {
        bool parsed = Pitch.TryParse(value, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(3)]
    public void Constructor_UnsupportedAlter_Throws(int alter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pitch(NoteLetter.C, alter, 4));
    }
}
