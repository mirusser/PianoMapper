using PianoMapper.Audio;

namespace PianoMapper.Tests.UnitTests;

public class FftTests
{
    private const int WindowSize = 2048;

    [Fact]
    public void ComputeMagnitudes_PureSineWave_PeaksAtBinMatchingFrequency()
    {
        const float frequency = 440f;
        var buffer = PCM.GenerateSineWave(frequency, durationSeconds: 1f);
        var window = buffer.Take(WindowSize).ToArray();

        var magnitudes = Fft.ComputeMagnitudes(window);

        var peakBin = Array.IndexOf(magnitudes, magnitudes.Max());
        var peakFrequency = Fft.BinToFrequency(peakBin, WindowSize, Consts.SampleRate);

        var binWidth = (double)Consts.SampleRate / WindowSize;
        Assert.True(
            Math.Abs(peakFrequency - frequency) <= binWidth * 1.5,
            $"expected peak near {frequency}Hz, got {peakFrequency}Hz (bin {peakBin})");
    }

    [Fact]
    public void ComputeMagnitudes_ReturnsHalfTheWindowSizeBins()
    {
        var buffer = PCM.GenerateSineWave(440f, durationSeconds: 1f);
        var window = buffer.Take(WindowSize).ToArray();

        var magnitudes = Fft.ComputeMagnitudes(window);

        Assert.Equal(WindowSize / 2, magnitudes.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1023)]
    public void ComputeMagnitudes_WindowSizeNotAPowerOfTwo_Throws(int size)
    {
        var window = new short[size];

        Assert.Throws<ArgumentException>(() => Fft.ComputeMagnitudes(window));
    }

    [Fact]
    public void BinToFrequency_FirstNonDcBin_ReturnsSampleRateOverWindowSize()
    {
        var frequency = Fft.BinToFrequency(1, WindowSize, Consts.SampleRate);

        Assert.Equal((double)Consts.SampleRate / WindowSize, frequency, 6);
    }
}
