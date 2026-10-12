namespace PianoMapper.Web.Rendering;

/// <summary>
/// The part of a score's grand-staff scene that stays the same for a given
/// (score, visible measure window, verdicts) combination — everything except the playback
/// cursor line. See <see cref="GrandStaffSceneBuilder.BuildStaticScoreParts"/> and
/// <see cref="GrandStaffSceneBuilder.ComposeScore"/>. Every Y in it is already placed by
/// <paramref name="VerticalFit"/>; whatever is drawn over it (the cursor, held notes) maps its own Y through the same fit.
/// </summary>
internal readonly record struct GrandStaffStaticScoreParts(
    IReadOnlyList<GrandStaffLine> Lines,
    IReadOnlyList<GrandStaffGlyph> Glyphs,
    IReadOnlyList<GrandStaffNote> Notes,
    IReadOnlyList<GrandStaffBeam> Beams,
    IReadOnlyList<GrandStaffBand> Bands,
    double? TrebleAnnotationLabelY,
    double? BassAnnotationLabelY,
    IReadOnlyList<GrandStaffSlur> Slurs,
    IReadOnlyList<GrandStaffArpeggioMark> ArpeggioMarks,
    IReadOnlyList<GrandStaffTie> Ties,
    ScoreTimingIndex Timing,
    IReadOnlyDictionary<int, ScoreNotationMeasureLayout> NotationMeasureLayouts,
    GrandStaffVerticalFit VerticalFit);
