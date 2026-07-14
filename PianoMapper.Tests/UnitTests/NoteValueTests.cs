using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class NoteValueTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(32)]
    public void Constructor_UnsupportedDenominator_Throws(int denominator)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteValue(denominator));
    }

    [Fact]
    public void Constructor_NegativeDots_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoteValue(4, -1));
    }
}
