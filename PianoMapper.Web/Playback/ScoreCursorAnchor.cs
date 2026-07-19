namespace PianoMapper.Web.Playback;

/// <summary>
/// The audio-clock anchor a client-side (JS) cursor animation needs to compute the current
/// playback beat position on its own, without polling back into C# every frame.
/// </summary>
internal readonly record struct ScoreCursorAnchor(
    double AnchorSeconds,
    double BeatsPerMinute,
    double CompletionSeconds);
