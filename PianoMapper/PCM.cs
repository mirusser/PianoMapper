namespace PianoMapper;

public static class PCM
{
    /*
    Step-by-Step Explanation of GenerateSineWave:

    1. Calculating the Total Number of Samples:
       ------------------------------------------------
       int sampleCount = (int)(sampleRate * durationSeconds);
       short[] buffer = new short[sampleCount];

       What it does:
         - The total number of samples needed is calculated by multiplying the sample rate
           (e.g., 44100 samples per second) by the duration (in seconds) of the tone.
           This determines how many discrete data points will represent our sound wave.

       In Physics/Music Theory:
         - The sample rate represents how finely we capture the continuous sound wave. According to the Nyquist theorem,
           to accurately reproduce a tone, you must sample at least twice as fast as its highest frequency.
         - A standard sample rate like 44100 Hz is chosen because it captures the full range of audible frequencies
           (roughly 20 Hz to 20 kHz).

    2. Determining the Angular Increment per Sample:
       ------------------------------------------------
       double increment = 2 * Math.PI * frequency / sampleRate;
       double angle = 0;

       What it does:
         - A sine wave is periodic with a period of 2π radians. For a tone of a specific frequency, one full cycle
           (2π radians) occurs in 1/frequency seconds.
         - Dividing 2π × frequency by the sample rate gives the angular increment for each sample.
         - This increment is added to an angle variable for every sample, so the sine function advances in phase correctly.

       In Physics:
         - The formula x(t) = A * sin(2π f t) models simple harmonic motion.
         - Here, 2π f / sampleRate tells us how much the phase (in radians) should change per sample.

       In Music Theory:
         - The frequency (in Hertz) determines the pitch of the note (e.g., A4 = 440 Hz). Setting the frequency
           defines the note’s pitch.

    3. Filling the Buffer with Sine Wave Samples:
       ------------------------------------------------
       for (int i = 0; i < sampleCount; i++)
       {
           buffer[i] = (short)(amplitude * Math.Sin(angle));
           angle += increment;
       }

       What it does:
         - The loop iterates over every sample index, calculates the instantaneous amplitude of the sine wave
           using Math.Sin(angle), and scales it by the maximum amplitude to fit the 16-bit range.
         - The angle is incremented for each sample, effectively "moving" along the sine curve over time.

       In Physics:
         - The sine function, sin(θ), represents the oscillatory nature of waves and is a direct mathematical model
           of a simple harmonic oscillator (like a vibrating string or a mass on a spring).

       In Music Theory:
         - A pure sine wave produces a “pure tone” with no additional harmonics (overtones).
           While most real instruments produce richer sounds by combining multiple harmonics,
           the sine wave is the basic building block of sound synthesis.

    4. Returning the PCM Data:
       ------------------------------------------------
       return buffer;

       What it does:
         - After the loop, the 'buffer' array contains all the PCM (pulse-code modulation)
           samples for the generated sine wave.
         - These samples represent the waveform’s instantaneous pressure variations and can be sent to an audio API
           (e.g., OpenAL) for playback.

       In Practical Terms:
         - When played back at the correct sample rate, these discrete samples will reconstruct the continuous sine wave,
           resulting in the perception of a pure tone at the desired pitch.
    */

    /// <summary>
    /// Generates a 16-bit PCM sine wave.
    /// </summary>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <returns>Array of short values containing PCM data.</returns>
    public static short[] GenerateSineWave(float frequency, float durationSeconds)
    {
        var sampleCount = (int)(Consts.SampleRate * durationSeconds);
        var buffer = new short[sampleCount];

        var increment = 2 * Math.PI * frequency / Consts.SampleRate;
        double angle = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            buffer[i] = (short)(Consts.Amplitude * Math.Sin(angle));
            angle += increment;
        }

        return buffer;
    }

    /// <summary>
    /// Generates a piano-like waveform using the provided formula and applies an attack envelope.
    /// </summary>
    /// <param name="frequency">The fundamental frequency in Hz.</param>
    /// <param name="durationSeconds">Total duration of the note in seconds.</param>
    /// <returns>Array of PCM samples (16-bit).</returns>
    public static short[] GeneratePianoWave(float frequency, float durationSeconds)
    {
        var sampleCount = (int)(Consts.SampleRate * durationSeconds);
        var buffer = new short[sampleCount];

        // Define an attack duration (in seconds) for a gentler onset.
        double attackDuration = durationSeconds * 3; // Adjust this value as needed

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / Consts.SampleRate;
            // Compute the basic exponential decay envelope
            double envelope = Math.Exp(-0.0004 * 2 * Math.PI * frequency * t);

            // Generate the fundamental tone and overtones:
            double Y = Math.Sin(2 * Math.PI * frequency * t) * envelope;
            for (int harmonic = 2; harmonic <= 6; harmonic++)
            {
                Y += Math.Sin(harmonic * 2 * Math.PI * frequency * t) * envelope / Math.Pow(2, harmonic - 1);
            }

            // Apply cubic saturation to enrich the timbre
            Y += Math.Pow(Y, 3);

            // Apply the time-dependent multiplier (if needed)
            Y *= 1 + 16 * t * Math.Exp(-6 * t);

            // Apply an attack envelope: ramp from 0 to 1 over the attackDuration.
            // Using a sine ramp gives a smooth curve.
            double attackFactor = t < attackDuration ? Math.Sin((t / attackDuration) * (Math.PI / 2)) : 1.0;
            Y *= attackFactor;

            // Scale to 16-bit PCM and assign to buffer
            buffer[i] = (short)(Consts.Amplitude * Y);
        }

        return buffer;
    }
    
    // /// <summary>
    // /// Returns a recommended note duration (in seconds) based on its frequency.
    // /// Lower notes sustain longer, higher notes decay faster.
    // /// </summary>
    // /// <remarks>
    // /// For example, A4 (440 Hz) is set to about 3 seconds, A1 (~55 Hz) to about 5 seconds, 
    // /// and A8 (~3520 Hz) to around 1.5 seconds.
    // /// </remarks>
    // /// <param name="frequency">The frequency of the note in Hz.</param>
    // /// <returns>Duration in seconds for a natural decay.</returns>
    // public static float GetNoteDuration(double frequency)
    // {
    //     // Calculate how many octaves away from A4 (440 Hz) the note is.
    //     // For frequencies below 440, octaveDifference will be negative; above, positive.
    //     var octaveDifference = Math.Log(frequency / 440.0, 2);
    //
    //     // For A4 (octaveDifference = 0), we want about 3 seconds.
    //     // Using a linear model in octaves: duration = 3 - k * octaveDifference.
    //     // For A1 (~55 Hz), octaveDifference = log2(55/440) = -3.
    //     // To target a duration of about 5 seconds for A1: 3 - k * (-3) = 3 + 3k ≈ 5 => k ≈ 0.67.
    //     var duration = (3.0 - 0.67 * octaveDifference);
    //
    //     // For A8 (~3520 Hz), octaveDifference = log2(3520/440) = 3,
    //     // which gives duration = 3 - 0.67*3 = 3 - 2.01 ≈ 0.99 seconds.
    //     // Since that might be too short, we clamp the duration.
    //     duration = Math.Max(1.5, Math.Min(5.0, duration));
    //     
    //     return (float)duration;
    // }
    
    // }
    
    /// <summary>
    /// Returns a recommended note duration (in seconds) based on its frequency,
    /// using an exponential scaling model so that duration falls off more naturally
    /// with higher pitches.
    /// 
    /// Formulation:
    ///     duration = D₀ * (frequency / 440.0)ᵏ
    /// where D₀ is the reference duration at A4 (e.g. 3 s) and k is a negative
    /// exponent (e.g. –0.4) that controls how sharply sustain decreases with pitch.
    /// Lower notes (frequency lower than 440 Hz) thus sustain longer, and higher notes
    /// (frequency higher than 440 Hz) decay faster, with optional soft clamping to
    /// avoid extremes.
    /// </summary>
    /// <param name="frequency">The frequency of the note in Hz.</param>
    /// <returns>Duration in seconds for a natural decay.</returns>
    public static float GetNoteDuration(double frequency)
    {
        const double referenceFreq = 440.0;    // A4
        const double referenceDur = 3.0;       // seconds at A4
        const double exponent = -0.4;          // controls falloff rate

        // Exponential model: duration scales as (freq/440)^exponent
        double duration = referenceDur * Math.Pow(frequency / referenceFreq, exponent);

        // Optional soft clamps to prevent extreme outliers:
        const double minDur = 1.2;  // lower bound
        const double maxDur = 6.0;  // upper bound
        duration = Math.Max(minDur, Math.Min(maxDur, duration));

        return (float)duration;
    }

    /// <summary>
    /// Returns the playback length (in seconds) for a note of a given frequency,
    /// constrained by its natural decay and by its rhythmic duration.
    /// </summary>
    /// <param name="frequency">
    ///   Frequency of the note in Hz (e.g. 440.0 for A4).
    /// </param>
    /// <param name="beatsPerMeasure">
    ///   Time signature numerator: number of beats in each measure (e.g., 3 for 3/4).
    /// </param>
    /// <param name="beatNoteValue">
    ///   Time signature denominator: which note value equals one beat  
    ///   (e.g., 4 for quarter‑note beats in 3/4; 8 for eighth‑note beats in 6/8)
    /// </param>
    /// <param name="measureDuration">
    ///   Total duration of one measure in seconds (e.g., from BPM × beatsPerMeasure)
    /// </param>
    /// <param name="noteDenominator">
    ///   Denominator of the target note’s value  
    ///   (e.g., 4=quarter, 8=eighth, 2=half, 1=whole)
    /// </param>
    /// <returns>
    ///   Playback length in seconds: min(naturalDecay, rhythmicDuration).
    /// </returns>
    public static float GetTimedNoteDuration(
        double frequency,
        int beatsPerMeasure,
        int beatNoteValue,
        double measureDuration,
        int noteDenominator = 1)
    {
        // 1. Natural decay (exponential model)
        const double referenceFreq = 440.0;  // A4
        const double referenceDur = 3.0;     // seconds at A4
        const double exponent     = -0.4;    // decay falloff rate
        double naturalDecay =
            referenceDur *
            Math.Pow(frequency / referenceFreq, exponent);

        // 2. Rhythmic duration
        // 2.1 Duration of one beat
        double beatDuration = measureDuration / beatsPerMeasure;            

        // 2.2 Beats spanned by the target note value
        double noteBeats = (double)beatNoteValue / noteDenominator;        

        // 2.3 Total rhythmic duration
        double rhythmicDuration = beatDuration * noteBeats;              

        // 3. Return the shorter of natural decay and rhythmic length
        return (float)Math.Min(naturalDecay, rhythmicDuration);
    }

    /// <summary>
    /// Returns the playback length (in seconds) for a note of a given frequency,
    /// constrained by its natural exponential decay and by its rhythmic duration
    /// based on BPM and time signature.
    /// </summary>
    /// <param name="frequency">
    ///   Frequency of the note in Hz (e.g. 440.0 for A4).
    /// </param>
    /// <param name="bpm">
    ///   Tempo in beats per minute (e.g. 120 for allegro).
    /// </param>
    /// <param name="beatNoteValue">
    ///   Time signature denominator: which note value equals one beat  
    ///   (e.g., 4 for quarter‐note beats in 4/4; 8 for eighth‐note beats in 6/8).
    /// </param>
    /// <param name="noteDenominator">
    ///   Denominator of the target note’s value  
    ///   (e.g., 4=quarter, 8=eighth, 2=half, 1=whole).
    /// </param>
    /// <returns>
    ///   Playback length in seconds: minimum of naturalDecay and rhythmicDuration.
    /// </returns>
    public static float GetTimedNoteDuration(
        double frequency,
        double bpm,
        int beatNoteValue,
        int noteDenominator)
    {
        // 1. Natural decay (exponential model)
        const double referenceFreq = 440.0;  // A4
        const double referenceDur = 3.0;     // seconds at A4
        const double exponent     = -0.4;    // decay falloff rate
        double naturalDecay =
            referenceDur *
            Math.Pow(frequency / referenceFreq, exponent);

        // 2. Rhythmic duration
        double beatDuration      = 60.0 / bpm;                      // seconds per beat
        double noteBeats         = (double)beatNoteValue / noteDenominator;
        double rhythmicDuration  = beatDuration * noteBeats;

        // 3. Return the shorter of natural decay and rhythmic length
        return (float)Math.Min(naturalDecay, rhythmicDuration);
    }

}