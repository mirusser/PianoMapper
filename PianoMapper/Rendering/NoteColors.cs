namespace PianoMapper.Rendering;

/// <summary>
/// Pure pitch-class -> RGB color mapping for piano-roll bars. Notes are colored by a
/// fixed 12-color wheel, one hue per chromatic semitone, so the same pitch class (e.g.
/// every "C" regardless of octave) always renders the same color.
/// </summary>
public static class NoteColors
{
    private static readonly string[] PitchClasses =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>Neutral gray used for malformed or empty note names.</summary>
    private static readonly float[] FallbackColor = [0.5f, 0.5f, 0.5f];

    public static float[] GetColor(string noteName)
    {
        var pitchClass = ExtractPitchClass(noteName);
        var index = Array.IndexOf(PitchClasses, pitchClass);
        return index >= 0 ? Wheel[index] : FallbackColor;
    }

    private static string ExtractPitchClass(string noteName)
    {
        if (string.IsNullOrEmpty(noteName) || noteName[0] is < 'A' or > 'G')
        {
            return string.Empty;
        }

        return noteName.Length > 1 && noteName[1] == '#' ? noteName[..2] : noteName[..1];
    }

    private static readonly float[][] Wheel = BuildWheel();

    private static float[][] BuildWheel()
    {
        var wheel = new float[PitchClasses.Length][];
        for (var i = 0; i < PitchClasses.Length; i++)
        {
            var hue = i * 360f / PitchClasses.Length;
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
