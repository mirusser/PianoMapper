using PianoMapper.Web.Audio;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserAudioClockTests
{
    [Theory]
    [InlineData(1250, 5.25)]
    [InlineData(900, 4.9)]
    public void MapEventTimestamp_KnownAnchor_ReturnsAudioClockTime(double eventMilliseconds, double expectedSeconds)
    {
        var anchor = new AudioClockAnchor(1000, 5);

        var mappedTime = BrowserAudioClock.MapEventTimestamp(anchor, eventMilliseconds);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), mappedTime);
    }
}
