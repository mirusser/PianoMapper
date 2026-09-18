using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MusicalValueTypesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(32)]
    public void NoteValueConstructor_UnsupportedDenominator_Throws(int denominator)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteValue(denominator));
    }

    [Fact]
    public void NoteValueConstructor_NegativeDots_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteValue(4, -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TempoConstructor_NonPositiveBeatsPerMinute_Throws(double beatsPerMinute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tempo(beatsPerMinute));
    }

    [Fact]
    public void TimeSignatureConstructor_NonPositiveNumerator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeSignature(0, new NoteValue(4)));
    }
}
