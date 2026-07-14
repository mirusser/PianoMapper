namespace PianoMapper.Tests.UnitTests;

public sealed class PcmDurationTests
{
    [Theory]
    [InlineData(440.0, 3.0)]
    [InlineData(27.5, 9.094)]
    [InlineData(7040.0, 0.990)]
    public void NaturalDecaySeconds_Frequency_UsesUnclampedDecay(double frequency, double expectedSeconds)
    {
        double duration = PCM.NaturalDecaySeconds(frequency);

        Assert.Equal(expectedSeconds, duration, 3);
    }
}
