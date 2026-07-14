using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class TempoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveBeatsPerMinute_Throws(double beatsPerMinute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tempo(beatsPerMinute));
    }
}
