namespace PianoMapper.Web.Rendering;

/// <summary>
/// The audio-clock parameters a <c>PianoCanvas</c> hands off to the JS rendering module so it
/// can animate the score-playback cursor line itself, once per animation frame, without a
/// per-tick C# scene rebuild and JS-interop round trip.
/// </summary>
public sealed record ScoreCursorPlaybackState(
    double AnchorSeconds,
    double BeatsPerMinute,
    int BeatsPerMeasure,
    int FirstVisibleMeasure,
    double CompletionSeconds,
    double CursorY0,
    double CursorY1);
