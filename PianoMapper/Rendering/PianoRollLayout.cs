namespace PianoMapper.Rendering;

/// <summary>
/// Pure time/pitch -> screen-space math for the piano-roll, kept free of any GL
/// dependency so it can be unit tested without a live rendering context.
/// </summary>
public static class PianoRollLayout
{
    public const double RollingWindowSeconds = 8.0;

    private const double ReferenceFrequency = 440.0; // A4
    private const double SemitoneRange = 48.0; // +/- 4 octaves mapped across the visible pitch band
    private const float BarHalfHeight = 0.02f;

    /// <summary>
    /// Returns the note's bar extent for the current frame, or null if the note has
    /// fully scrolled out of the rolling window.
    /// </summary>
    public static BarRect? GetBarRect(NoteInstance note, TimeSpan now)
    {
        var nowSeconds = now.TotalSeconds;
        var windowStart = nowSeconds - RollingWindowSeconds;
        var noteStart = note.StartTime.TotalSeconds;
        var noteEnd = noteStart + note.Duration;

        if (noteEnd < windowStart)
        {
            return null;
        }

        var visibleEnd = Math.Min(noteEnd, nowSeconds);

        var x0 = MapTimeToX(noteStart, nowSeconds);
        var x1 = MapTimeToX(visibleEnd, nowSeconds);
        var y = MapFrequencyToY(note.Frequency);

        return new BarRect(x0, x1, y - BarHalfHeight, y + BarHalfHeight);
    }

    private static float MapTimeToX(double time, double now)
    {
        var windowStart = now - RollingWindowSeconds;
        var t = (time - windowStart) / RollingWindowSeconds; // 0..1 across the window
        return (float)Math.Clamp(t * 2.0 - 1.0, -1.0, 1.0); // -1..1
    }

    private static float MapFrequencyToY(float frequency)
    {
        if (frequency <= 0f)
        {
            return -1f;
        }

        var semitoneOffset = 12.0 * Math.Log2(frequency / ReferenceFrequency);
        var normalized = Math.Clamp(semitoneOffset / SemitoneRange, -1.0, 1.0);
        return (float)normalized;
    }
}
