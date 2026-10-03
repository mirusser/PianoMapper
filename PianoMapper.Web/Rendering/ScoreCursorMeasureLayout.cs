namespace PianoMapper.Web.Rendering;

/// <summary>
/// The browser-facing notation geometry for one visible measure. It lets the client-side playback
/// cursor follow the same piecewise spacing as the cached C# score scene.
/// </summary>
public sealed record ScoreCursorMeasureLayout(
    int MeasureIndex,
    double StartBeat,
    double EndBeat,
    double NoteAreaStartX,
    double NoteAreaEndX,
    IReadOnlyList<double> SpacingAnchorBeats,
    IReadOnlyList<double> SpacingAnchorFractions);
