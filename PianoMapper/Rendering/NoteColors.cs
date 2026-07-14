using PianoMapper.Music;

namespace PianoMapper.Rendering;

/// <summary>
/// Pure pitch-class -> RGB color mapping for piano-roll bars. Notes are colored by a
/// fixed 12-color wheel, one hue per chromatic semitone, so the same pitch class (e.g.
/// every "C" regardless of octave) always renders the same color.
/// </summary>
internal static class NoteColors
{
    public static float[] GetColor(Pitch pitch)
    {
        int pitchClass = ((pitch.MidiNumber % 12) + 12) % 12;
        return Wheel[pitchClass];
    }

    private static readonly float[][] Wheel = BuildWheel();

    private static float[][] BuildWheel()
    {
        const int pitchClassCount = 12;
        var wheel = new float[pitchClassCount][];
        for (int i = 0; i < pitchClassCount; i++)
        {
            float hue = i * 360f / pitchClassCount;
            wheel[i] = HsvToRgb(hue, saturation: 0.65f, value: 0.9f);
        }

        return wheel;
    }

    private static float[] HsvToRgb(float hue, float saturation, float value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60f % 2 - 1));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x),
        };

        return [r + m, g + m, b + m];
    }
}
