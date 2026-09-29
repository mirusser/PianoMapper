using System.Collections.Frozen;
using PianoMapper.Music;

namespace PianoMapper.Web.Input;

/// <summary>
/// The opt-in "desktop-style" typing-piano layout: a bottom row of keys spanning roughly one octave plus a couple
/// of extra notes, in the same order most DAWs use (Z row = white keys, S/D/G/H/J row = the black keys between
/// them). A separate mapping from <see cref="BrowserKeyBindings"/> (control shortcuts) — several codes
/// intentionally overlap (e.g. <c>KeyC</c>, <c>KeyV</c>, <c>KeyM</c> are both shortcuts and piano notes here); when
/// the computer-piano toggle is on, piano notes take priority for those codes.
/// </summary>
internal static class BrowserComputerPianoBindings
{
    private static readonly FrozenDictionary<string, int> SemitoneOffsetsByCode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KeyZ"] = 0, // C
            ["KeyS"] = 1, // C#
            ["KeyX"] = 2, // D
            ["KeyD"] = 3, // D#
            ["KeyC"] = 4, // E
            ["KeyV"] = 5, // F
            ["KeyG"] = 6, // F#
            ["KeyB"] = 7, // G
            ["KeyH"] = 8, // G#
            ["KeyN"] = 9, // A
            ["KeyJ"] = 10, // A#
            ["KeyM"] = 11, // B
            ["Comma"] = 12, // C (next octave)
            ["KeyL"] = 13, // C# (next octave)
            ["Period"] = 14, // D (next octave)
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static IReadOnlyList<string> HandledCodes { get; } =
        SemitoneOffsetsByCode.Keys.Order(StringComparer.Ordinal).ToArray();

    internal static bool TryGetPitch(string code, int baseOctave, out Pitch pitch)
    {
        if (!SemitoneOffsetsByCode.TryGetValue(code, out int semitoneOffset))
        {
            pitch = default;
            return false;
        }

        int midiNumber = ((baseOctave + 1) * 12) + semitoneOffset;
        pitch = MidiPitchMapping.CreatePitch(midiNumber);
        return true;
    }
}
