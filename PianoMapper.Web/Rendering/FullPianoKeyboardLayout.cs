using PianoMapper.Music;

namespace PianoMapper.Web.Rendering;

internal static class FullPianoKeyboardLayout
{
    private const int LowestMidiNumber = 21;
    private const int HighestMidiNumber = 108;
    private const int WhiteKeyCount = 52;
    private const double BlackToWhiteWidthRatio = 0.62;
    private static readonly NoteLetter[] LettersByPitchClass =
    [
        NoteLetter.C,
        NoteLetter.C,
        NoteLetter.D,
        NoteLetter.D,
        NoteLetter.E,
        NoteLetter.F,
        NoteLetter.F,
        NoteLetter.G,
        NoteLetter.G,
        NoteLetter.A,
        NoteLetter.A,
        NoteLetter.B,
    ];
    private static readonly int[] AlterationsByPitchClass = [0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0];

    internal static IReadOnlyList<PianoKeyboardKey> Build(IReadOnlySet<int> activeMidiNumbers)
    {
        ArgumentNullException.ThrowIfNull(activeMidiNumbers);

        double whiteKeyWidth = 100.0 / WhiteKeyCount;
        double blackKeyWidth = whiteKeyWidth * BlackToWhiteWidthRatio;
        int whiteKeyIndex = 0;
        var keys = new List<PianoKeyboardKey>(HighestMidiNumber - LowestMidiNumber + 1);

        for (int midiNumber = LowestMidiNumber; midiNumber <= HighestMidiNumber; midiNumber++)
        {
            Pitch pitch = CreatePitch(midiNumber);
            bool isBlack = pitch.Alter != 0;
            double left = isBlack
                ? (whiteKeyIndex * whiteKeyWidth) - (blackKeyWidth / 2)
                : whiteKeyIndex * whiteKeyWidth;
            double width = isBlack ? blackKeyWidth : whiteKeyWidth;
            keys.Add(new PianoKeyboardKey(
                pitch,
                isBlack,
                Math.Clamp(left, 0, 100 - width),
                width,
                activeMidiNumbers.Contains(midiNumber)));

            if (!isBlack)
            {
                whiteKeyIndex++;
            }
        }

        return keys;
    }

    private static Pitch CreatePitch(int midiNumber)
    {
        int pitchClass = midiNumber % 12;
        int octave = (midiNumber / 12) - 1;
        return new Pitch(
            LettersByPitchClass[pitchClass],
            AlterationsByPitchClass[pitchClass],
            octave);
    }
}
