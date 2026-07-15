using PianoMapper.Music;

namespace PianoMapper.Input;

public static class PianoKeyboardLayout
{
    public static Pitch GetPitch(int startingOctave, int semitoneOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(semitoneOffset);

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
            _ => throw new InvalidOperationException(),
        };
    }
}
