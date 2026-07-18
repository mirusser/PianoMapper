using PianoMapper.Music;

namespace PianoMapper.Rendering;

/// <summary>
/// Pure time/pitch -> screen-space math for the piano-roll, kept free of any GL
/// dependency so it can be unit tested without a live rendering context.
/// </summary>
public static class PianoRollLayout
{
    public const double RollingWindowSeconds = 8.0;

    // Piano-roll occupies the upper region of the window, leaving the lower region free
    // for the oscilloscope/spectrum bands so the three never visually collide.
    public const float BandY0 = -0.30f;
    public const float BandY1 = 0.95f;

    private const int ReferenceMidiNumber = 69; // A4
    private const double SemitoneRange = 48.0; // +/- 4 octaves mapped across the visible pitch band
    private const float BarHalfHeight = 0.02f;

    /// <summary>
    /// Returns the note's bar extent for the current frame, or null if the note has
    /// fully scrolled out of the rolling window.
    /// </summary>
    public static BarRect? GetBarRect(PerformedNote note, TimeSpan now)
    {
        double nowSeconds = now.TotalSeconds;
        double windowStart = nowSeconds - RollingWindowSeconds;
        double noteStart = note.StartTime.TotalSeconds;
        double noteEnd = (note.ReleaseTime ?? now).TotalSeconds;

        if (noteEnd < windowStart)
        {
            return null;
        }

        double visibleEnd = Math.Min(noteEnd, nowSeconds);

        float x0 = MapTimeToX(noteStart, nowSeconds);
        float x1 = MapTimeToX(visibleEnd, nowSeconds);
        float y = MapPitchToY(note.Pitch);

        return new BarRect(x0, x1, y - BarHalfHeight, y + BarHalfHeight);
    }

    public static float MapTimeToX(double time, double now)
    {
        double windowStart = now - RollingWindowSeconds;
        double t = (time - windowStart) / RollingWindowSeconds; // 0..1 across the window
        return (float)Math.Clamp(t * 2.0 - 1.0, -1.0, 1.0); // -1..1
    }

    private static float MapPitchToY(Pitch pitch)
    {
        int semitoneOffset = pitch.MidiNumber - ReferenceMidiNumber;
        double normalized = Math.Clamp(semitoneOffset / SemitoneRange, -1.0, 1.0); // -1..1

        double bandMid = (BandY0 + BandY1) / 2.0;
        double bandHalfSpan = (BandY1 - BandY0) / 2.0;
        return (float)(bandMid + normalized * bandHalfSpan);
    }
}
