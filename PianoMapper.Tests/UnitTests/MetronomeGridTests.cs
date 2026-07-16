using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MetronomeGridTests
{
    [Theory]
    [InlineData(9.6, -1, 0.1)]
    [InlineData(10.0, 0, 0.0)]
    [InlineData(10.4, 1, -0.1)]
    public void GetNearestBeat_TimeAroundAnchor_ReturnsSignedDeviation(
        double timeSeconds,
        long expectedBeatIndex,
        double expectedDeviationSeconds)
    {
        var grid = new MetronomeGrid(
            TimeSpan.FromSeconds(10),
            new Tempo(120),
            new TimeSignature(4, new NoteValue(4)));

        long beatIndex = grid.GetNearestBeatIndex(TimeSpan.FromSeconds(timeSeconds));
        TimeSpan deviation = grid.GetDeviation(TimeSpan.FromSeconds(timeSeconds));

        Assert.Equal(expectedBeatIndex, beatIndex);
        Assert.Equal(expectedDeviationSeconds, deviation.TotalSeconds, 6);
    }

    [Fact]
    public void BeatDuration_SixEightAtNinetyBpm_UsesEighthNoteBeat()
    {
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(90),
            new TimeSignature(6, new NoteValue(8)));

        Assert.Equal(2.0 / 3.0, grid.BeatDuration.TotalSeconds, 6);
        Assert.Equal(1.5, grid.GetBeatsElapsed(TimeSpan.FromSeconds(1)), 6);
    }
}
