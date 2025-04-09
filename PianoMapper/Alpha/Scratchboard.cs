namespace PianoMapper.Alpha;

public class Scratchboard
{
    
    public static short[] GeneratePianoWave(float frequency, float durationSeconds)
    {
        int sampleCount = (int)(Consts.SampleRate * durationSeconds);
        short[] buffer = new short[sampleCount];

        // Define amplitudes for the fundamental and several harmonics.
        // These values can be tweaked to shape the piano's timbre.
        //double[] harmonicAmplitudes = { 1.0, 0.6, 0.3, 0.1 }; // fundamental, 2nd, 3rd, 4th harmonics

        double[] harmonicAmplitudes = { 1.0, 0.8, 0.5, 0.3, 0.1 };


        // Sum the sine waves for each sample.
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / Consts.SampleRate;
            double sampleValue = 0.0;

            // Add each harmonic.
            for (int h = 0; h < harmonicAmplitudes.Length; h++)
            {
                // (h+1) gives the harmonic number.
                double harmonicFrequency = frequency * (h + 1);
                sampleValue += harmonicAmplitudes[h] * Math.Sin(2 * Math.PI * harmonicFrequency * t);
            }

            // Normalize the sample value by the sum of amplitudes.
            double normalizationFactor = harmonicAmplitudes.Sum();
            sampleValue /= normalizationFactor;

            // Apply the overall amplitude and convert to 16-bit PCM value.
            buffer[i] = (short)(Consts.Amplitude * sampleValue);
        }

        return buffer;
    }

    public static short[] GeneratePianoWave2(float frequency, float durationSeconds, int numHarmonics = 20)
    {
        int sampleCount = (int)(Consts.SampleRate * durationSeconds);
        short[] buffer = new short[sampleCount];

        // Generate harmonic amplitudes using a decay function.
        // The decay factor determines how quickly higher harmonics diminish.
        double decayFactor = 1.5; // tweak this value: higher means faster decay.
        double[] harmonicAmplitudes = new double[numHarmonics];
        double totalAmplitude = 0.0;

        for (int h = 0; h < numHarmonics; h++)
        {
            // Using a power law decay: amplitude = 1 / (h+1)^decayFactor.
            harmonicAmplitudes[h] = 1.0 / Math.Pow(h + 1, decayFactor);
            totalAmplitude += harmonicAmplitudes[h];
        }

        // Normalize the harmonic amplitudes so their sum equals 1.
        for (int h = 0; h < numHarmonics; h++)
        {
            harmonicAmplitudes[h] /= totalAmplitude;
        }

        // Synthesize the wave by summing all harmonics.
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / Consts.SampleRate;
            double sampleValue = 0.0;

            for (int h = 0; h < numHarmonics; h++)
            {
                double harmonicFrequency = frequency * (h + 1);
                sampleValue += harmonicAmplitudes[h] * Math.Sin(2 * Math.PI * harmonicFrequency * t);
            }

            buffer[i] = (short)(Consts.Amplitude * sampleValue);
        }

        return buffer;
    }


    public static short[] GeneratePianoWave3(float frequency, float durationSeconds, int numHarmonics = 20)
    {
        int sampleCount = (int)(Consts.SampleRate * durationSeconds);
        short[] buffer = new short[sampleCount];

        // ADSR envelope parameters (in seconds)
        double attackTime = 0.01; // 10ms attack
        double decayTime = 0.1; // 100ms decay
        double sustainLevel = 0.7; // Sustain amplitude (70% of full)
        double releaseTime = 0.2; // 200ms release

        // Ensure the note duration can accommodate the envelope phases.
        double sustainTime = durationSeconds - attackTime - decayTime - releaseTime;
        if (sustainTime < 0)
            sustainTime = 0;

        int attackSamples = (int)(attackTime * Consts.SampleRate);
        int decaySamples = (int)(decayTime * Consts.SampleRate);
        int sustainSamples = (int)(sustainTime * Consts.SampleRate);
        int releaseSamples = (int)(releaseTime * Consts.SampleRate);

        // Calculate harmonic amplitudes using an inverse power decay.
        double decayFactor = 1.5; // Higher values yield faster decay of higher harmonics.
        double[] harmonicAmplitudes = new double[numHarmonics];
        double totalAmplitude = 0.0;
        for (int h = 0; h < numHarmonics; h++)
        {
            // Amplitude = 1/(harmonic number^decayFactor)
            harmonicAmplitudes[h] = 1.0 / Math.Pow(h + 1, decayFactor);
            totalAmplitude += harmonicAmplitudes[h];
        }

        // Normalize the harmonic amplitudes.
        for (int h = 0; h < numHarmonics; h++)
        {
            harmonicAmplitudes[h] /= totalAmplitude;
        }

        // Synthesize the waveform sample by sample.
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / Consts.SampleRate;
            double sampleValue = 0.0;

            // Calculate the ADSR envelope value for the current sample.
            double env = 1.0;
            if (i < attackSamples)
            {
                // Attack phase: ramp up from 0 to 1.
                env = (double)i / attackSamples;
            }
            else if (i < attackSamples + decaySamples)
            {
                // Decay phase: drop from 1 to sustain level.
                int decayIndex = i - attackSamples;
                env = 1.0 - (1.0 - sustainLevel) * decayIndex / decaySamples;
            }
            else if (i < attackSamples + decaySamples + sustainSamples)
            {
                // Sustain phase: hold the sustain level.
                env = sustainLevel;
            }
            else
            {
                // Release phase: ramp down from sustain level to 0.
                int releaseIndex = i - (attackSamples + decaySamples + sustainSamples);
                env = sustainLevel * (1 - (double)releaseIndex / releaseSamples);
            }

            // Sum the harmonics with slight inharmonicity.
            for (int h = 0; h < numHarmonics; h++)
            {
                // Introduce a slight detuning to higher harmonics for realism.
                double inharmonicity = 1 + 0.0005 * h * h; // tweak this factor as needed.
                double harmonicFrequency = frequency * (h + 1) * inharmonicity;
                sampleValue += harmonicAmplitudes[h] * Math.Sin(2 * Math.PI * harmonicFrequency * t);
            }

            // Apply the envelope.
            sampleValue *= env;

            // Convert to 16-bit PCM.
            buffer[i] = (short)(Consts.Amplitude * sampleValue);
        }

        return buffer;
    }
    
    public static short[] GeneratePianoWave4(float frequency, float durationSeconds)
    {
        int sampleCount = (int)(Consts.SampleRate * durationSeconds);
        short[] buffer = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / Consts.SampleRate;
            // Exponential decay envelope for the fundamental and harmonics
            double envelope = Math.Exp(-0.0004 * 2 * Math.PI * frequency * t);

            // Fundamental tone: Y = sin(2*pi*f*t) * exp(-0.0004*2*pi*f*t)
            double Y = Math.Sin(2 * Math.PI * frequency * t) * envelope;

            // Add overtones with decreasing amplitudes:
            // 2nd harmonic: divided by 2, 3rd by 4, 4th by 8, 5th by 16, 6th by 32.
            for (int harmonic = 2; harmonic <= 6; harmonic++)
            {
                Y += Math.Sin(harmonic * 2 * Math.PI * frequency * t) * envelope / Math.Pow(2, harmonic - 1);
            }

            // Apply saturation: add cubic non-linearity
            //Y += Math.Pow(Y, 3);

            // Time-dependent multiplier for additional saturation dynamics:
            //Y *= 1 + 16 * t * Math.Exp(-6 * t);

            // Scale the result to the 16-bit PCM range
            buffer[i] = (short)(Consts.Amplitude * Y);
        }

        return buffer;
    }
}