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
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void NoteValueConstructor_NonPositiveTupletRatio_Throws(int tupletActualNotes, int tupletNormalNotes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteValue(
            8,
            tupletActualNotes: tupletActualNotes,
            tupletNormalNotes: tupletNormalNotes));
    }

    [Fact]
    public void NoteValueConstructor_NoTupletArguments_DefaultsToIdentityRatio()
    {
        var noteValue = new NoteValue(8);

        Assert.Equal(1, noteValue.TupletActualNotes);
        Assert.Equal(1, noteValue.TupletNormalNotes);
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
