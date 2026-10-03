namespace PianoMapper.Web.Rendering;

/// <summary>
/// The shared horizontal geometry of one measure in a cached score window.
/// </summary>
internal readonly record struct ScoreNotationMeasureLayout(
    double NoteAreaStartX,
    double NoteAreaEndX,
    IReadOnlyList<(double Beat, double Fraction)> SpacingAnchors);
