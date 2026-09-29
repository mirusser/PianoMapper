using PianoMapper.Music;

namespace PianoMapper.Web.Input;

/// <summary>
/// Maps a MIDI note number to a <see cref="Pitch"/> using sharps for every black key (never flats) — the same
/// mapping both <see cref="BrowserMidiInputState"/> (from an actual MIDI device) and
/// <see cref="BrowserComputerPianoBindings"/> (from the opt-in computer-key piano) need, so it lives in one place.
/// </summary>
internal static class MidiPitchMapping
{
    internal static Pitch CreatePitch(int midiNumber)
    {
        int octave = (midiNumber / 12) - 1;
        return (midiNumber % 12) switch
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
