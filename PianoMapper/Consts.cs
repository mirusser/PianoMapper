namespace PianoMapper;

public static class Consts
{
    public const int SampleRate = 44100; // Samples per second
    public const short Amplitude = short.MaxValue; // 16-bit max amplitude
    
    public static Dictionary<ConsoleKey, Note> GenerateKeyToFrequencyMapping(int startingOctave)
    {
        // Mapping of ConsoleKey to semitone offset relative to C of the starting octave.
        var keyOffsets = new Dictionary<ConsoleKey, int>
        {
            { ConsoleKey.A, 0 },   // C
            { ConsoleKey.W, 1 },   // C#
            { ConsoleKey.S, 2 },   // D
            { ConsoleKey.E, 3 },   // D#
            { ConsoleKey.D, 4 },   // E
            { ConsoleKey.F, 5 },   // F
            { ConsoleKey.T, 6 },   // F#
            { ConsoleKey.J, 7 },   // G
            { ConsoleKey.U, 8 },   // G#
            { ConsoleKey.K, 9 },   // A
            { ConsoleKey.I, 10 },  // A#
            { ConsoleKey.L, 11 },  // B
            { ConsoleKey.None, 12 }  // C (next octave) - should be Oem1 but my keyboard doesn't register it for some reason
        };

        var mapping = new Dictionary<ConsoleKey, Note>();

        // Calculate the frequency for C in the starting octave.
        // For example, C4 is approximately 261.63 Hz.
        // One way to compute this is to use C0 = 16.35 Hz and then:
        // frequency = 16.35 * 2^(octave)
        double baseC = 16.35 * Math.Pow(2, startingOctave);

        // Note names for the 12 semitones.
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        foreach (var kvp in keyOffsets)
        {
            ConsoleKey key = kvp.Key;
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