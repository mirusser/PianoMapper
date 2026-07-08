using OpenTK.Windowing.GraphicsLibraryFramework;

namespace PianoMapper;

public static class Consts
{
    public const int SampleRate = 44100; // Samples per second
    public const short Amplitude = short.MaxValue; // 16-bit max amplitude

    // Shared by the oscilloscope (sample window it displays) and the FFT (its input
    // size). Must be a power of two for the radix-2 FFT.
    public const int ScopeWindowSize = 1024;

    public static Dictionary<Keys, Note> GenerateKeyToFrequencyMapping(int startingOctave)
    {
        // Mapping of Keys to semitone offset relative to C of the starting octave.
        var keyOffsets = new Dictionary<Keys, int>
        {
            { Keys.A, 0 },   // C
            { Keys.W, 1 },   // C#
            { Keys.S, 2 },   // D
            { Keys.E, 3 },   // D#
            { Keys.D, 4 },   // E
            { Keys.F, 5 },   // F
            { Keys.R, 6 },   // F#
            { Keys.J, 7 },   // G
            { Keys.U, 8 },   // G#
            { Keys.K, 9 },   // A
            { Keys.I, 10 },  // A#
            { Keys.L, 11 },  // B
            { Keys.Semicolon, 12 }  // C (next octave)
        };

        var mapping = new Dictionary<Keys, Note>();

        // Calculate the frequency for C in the starting octave.
        // For example, C4 is approximately 261.63 Hz.
        // One way to compute this is to use C0 = 16.35 Hz and then:
        // frequency = 16.35 * 2^(octave)
        double baseC = 16.35 * Math.Pow(2, startingOctave);

        // Note names for the 12 semitones.
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        foreach (var kvp in keyOffsets)
        {
            Keys key = kvp.Key;
            int semitoneOffset = kvp.Value;
            // Calculate frequency: multiply base C by 2^(semitoneOffset/12)
            double frequency = baseC * Math.Pow(2, semitoneOffset / 12.0);
            // Determine the note name and octave:
            int noteOctave = startingOctave + (semitoneOffset / 12);
            string noteName = noteNames[semitoneOffset % 12] + noteOctave.ToString();

            mapping[key] = new Note
            {
                Name = noteName,
                Frequency = (float)frequency
            };
        }

        return mapping;
    }
}

public class Note
{
    public required string Name { get; init; }
    public float Frequency { get; init; }
}