namespace PianoMapper.Audio;

/// <summary>
/// Hand-rolled iterative radix-2 Cooley-Tukey FFT for spectrum analysis, consistent
/// with the existing hand-rolled DSP style in <see cref="PCM"/> (no external DSP
/// dependency).
/// </summary>
public static class Fft
{
    /// <summary>
    /// Computes magnitude spectrum bins for a window of 16-bit PCM samples.
    /// <paramref name="samples"/>'s length must be a non-zero power of two.
    /// </summary>
    public static double[] ComputeMagnitudes(IReadOnlyList<short> samples)
    {
        int n = samples.Count;
        if (n == 0 || (n & (n - 1)) != 0)
        {
            throw new ArgumentException("Sample window length must be a non-zero power of two.", nameof(samples));
        }

        var real = new double[n];
        var imag = new double[n];
        for (int i = 0; i < n; i++)
        {
            // Hann window to reduce spectral leakage from the finite sample window.
            double hann = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            real[i] = samples[i] * hann;
        }

        Transform(real, imag);

        var magnitudes = new double[n / 2];
        for (int i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
        }

        return magnitudes;
    }

    /// <summary>
    /// Returns the frequency (Hz) a given magnitude bin index represents.
    /// </summary>
    public static double BinToFrequency(int binIndex, int windowSize, int sampleRate) =>
        (double)binIndex * sampleRate / windowSize;

    private static void Transform(double[] real, double[] imag)
    {
        int n = real.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }
            j ^= bit;

            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            double wLenReal = Math.Cos(angle);
            double wLenImag = Math.Sin(angle);
            int half = len / 2;

            for (int start = 0; start < n; start += len)
            {
                double wReal = 1, wImag = 0;

                for (int k = 0; k < half; k++)
                {
                    int evenIndex = start + k;
                    int oddIndex = start + k + half;

                    double oddReal = real[oddIndex] * wReal - imag[oddIndex] * wImag;
                    double oddImag = real[oddIndex] * wImag + imag[oddIndex] * wReal;

                    real[oddIndex] = real[evenIndex] - oddReal;
                    imag[oddIndex] = imag[evenIndex] - oddImag;
                    real[evenIndex] += oddReal;
                    imag[evenIndex] += oddImag;

                    double nextWReal = wReal * wLenReal - wImag * wLenImag;
                    double nextWImag = wReal * wLenImag + wImag * wLenReal;
                    wReal = nextWReal;
                    wImag = nextWImag;
                }
            }
        }
    }
}
