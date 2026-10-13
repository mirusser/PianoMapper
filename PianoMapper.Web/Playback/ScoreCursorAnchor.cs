namespace PianoMapper.Web.Playback;

/// <summary>
/// The audio-clock anchor a client-side (JS) cursor animation needs to compute the current
/// playback beat position on its own, without polling back into C# every frame. <see cref="PausedBeats"/> is set
/// while the score is paused: the cursor then stands at that beat instead of following the audio clock.
/// </summary>
internal readonly record struct ScoreCursorAnchor(
    double AnchorSeconds,
    double BeatsPerMinute,
    double CompletionSeconds,
    double? PausedBeats = null);
