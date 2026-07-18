namespace PianoMapper.Tests.UnitTests;

public sealed class PcmWaveformTests
{
    private const int AttackSampleCount = (int)(0.02 * Consts.SampleRate);

    [Theory]
    [InlineData(65.0f)]
    [InlineData(440.0f)]
    [InlineData(1975.0f)]
    public void GeneratePianoWave_RepresentativeFrequencies_NeverWrapsOrExceedsShortRange(float frequency)
    {
        short[] buffer = PCM.GeneratePianoWave(frequency, durationSeconds: 1.0f);

        Assert.All(buffer, sample => Assert.InRange(sample, short.MinValue, short.MaxValue));

        // The historical clipping bug (commit d280f03) was a two's-complement wraparound from
        // casting an out-of-range double straight to short, instead of saturating first. That
        // shows up as an implausibly large sample-to-sample jump; a properly clamped waveform
        // never jumps anywhere near the full 16-bit range between adjacent samples at audio rates.
        for (int i = 1; i < buffer.Length; i++)
        {
            int delta = Math.Abs(buffer[i] - buffer[i - 1]);
            Assert.True(
                delta < 40000,
                $"Sample-to-sample jump of {delta} at index {i} for {frequency} Hz looks like a wraparound artifact.");
        }
    }

    [Theory]
    [InlineData(65.0f, 0.5f)]
    [InlineData(440.0f, 1.25f)]
    [InlineData(1975.0f, 2.0f)]
    public void GeneratePianoWave_DurationAndSampleRate_ProducesExpectedSampleCount(float frequency, float durationSeconds)
    {
        short[] buffer = PCM.GeneratePianoWave(frequency, durationSeconds);

        int expectedSampleCount = (int)(Consts.SampleRate * durationSeconds);
        Assert.Equal(expectedSampleCount, buffer.Length);
    }

    [Fact]
    public void GeneratePianoWave_AttackWindow_RampsFromZeroToFullAmplitude()
    {
        short[] buffer = PCM.GeneratePianoWave(frequency: 440f, durationSeconds: 0.5f);

        // At t=0 the attack factor is sin(0) = 0, so the very first sample is silent.
        Assert.Equal(0, buffer[0]);

        double earlyPeak = buffer.Take(20).Select(sample => (double)Math.Abs(sample)).Max();

        int postAttackStart = AttackSampleCount + 100;
        double postAttackPeak = buffer.Skip(postAttackStart).Take(200)
            .Select(sample => (double)Math.Abs(sample)).Max();

        Assert.True(
            postAttackPeak > earlyPeak * 5,
            $"Expected post-attack peak ({postAttackPeak}) well above early-attack peak ({earlyPeak}).");
    }

    [Theory]
    [InlineData(65.0f)]
    [InlineData(440.0f)]
    [InlineData(1975.0f)]
    public void GeneratePianoWave_EndOfNaturalDecayWindow_AmplitudeTrendsTowardSilence(float frequency)
    {
        double naturalDecaySeconds = PCM.NaturalDecaySeconds(frequency);
        short[] buffer = PCM.GeneratePianoWave(frequency, (float)naturalDecaySeconds);

        int earlyWindowStart = AttackSampleCount + 100;
        int earlyWindowLength = (int)(0.05 * Consts.SampleRate);
        double earlyRms = Rms(buffer, earlyWindowStart, earlyWindowLength);

        int lateWindowLength = (int)(0.05 * Consts.SampleRate);
        int lateWindowStart = Math.Max(earlyWindowStart, buffer.Length - lateWindowLength);
        double lateRms = Rms(buffer, lateWindowStart, buffer.Length - lateWindowStart);

        Assert.True(
            lateRms < earlyRms * 0.9,
            $"Expected late-window RMS ({lateRms:F1}) well below early-window RMS ({earlyRms:F1}) for {frequency} Hz.");
    }

    private static double Rms(short[] buffer, int start, int length)
    {
        double sumOfSquares = 0;
        for (int i = start; i < start + length; i++)
        {
            sumOfSquares += (double)buffer[i] * buffer[i];
        }

        return Math.Sqrt(sumOfSquares / length);
    }
}
