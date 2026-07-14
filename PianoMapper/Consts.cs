using System.Collections.Frozen;
using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Music;

namespace PianoMapper;

public static class Consts
{
    private static readonly FrozenDictionary<Keys, int> KeyOffsets = new Dictionary<Keys, int>
    {
        { Keys.A, 0 },
        { Keys.W, 1 },
        { Keys.S, 2 },
        { Keys.E, 3 },
        { Keys.D, 4 },
        { Keys.F, 5 },
        { Keys.R, 6 },
        { Keys.J, 7 },
        { Keys.U, 8 },
        { Keys.K, 9 },
        { Keys.I, 10 },
        { Keys.L, 11 },
        { Keys.Semicolon, 12 },
    }.ToFrozenDictionary();

    public const int SampleRate = 44100; // Samples per second
    public const short Amplitude = short.MaxValue; // 16-bit max amplitude

    // Shared by the oscilloscope (sample window it displays) and the FFT (its input
    // size). Must be a power of two for the radix-2 FFT.
    public const int ScopeWindowSize = 1024;

    internal static IReadOnlyDictionary<Keys, Pitch> GenerateKeyToPitchMapping(int startingOctave)
    {
        var mapping = new Dictionary<Keys, Pitch>();

        foreach (var (key, semitoneOffset) in KeyOffsets)
        {
            mapping[key] = GetPitch(startingOctave, semitoneOffset);
        }

        return mapping.ToFrozenDictionary();
    }

    private static Pitch GetPitch(int startingOctave, int semitoneOffset)
    {
        int octave = startingOctave + (semitoneOffset / 12);
        return (semitoneOffset % 12) switch
        {
            0 => new Pitch(NoteLetter.C, 0, octave),
            1 => new Pitch(NoteLetter.C, 1, octave),
            2 => new Pitch(NoteLetter.D, 0, octave),
            3 => new Pitch(NoteLetter.D, 1, octave),
            4 => new Pitch(NoteLetter.E, 0, octave),
            5 => new Pitch(NoteLetter.F, 0, octave),
            6 => new Pitch(NoteLetter.F, 1, octave),
            7 => new Pitch(NoteLetter.G, 0, octave),
            8 => new Pitch(NoteLetter.G, 1, octave),
            9 => new Pitch(NoteLetter.A, 0, octave),
            10 => new Pitch(NoteLetter.A, 1, octave),
            11 => new Pitch(NoteLetter.B, 0, octave),
            _ => throw new ArgumentOutOfRangeException(nameof(semitoneOffset)),
        };
    }
}
