namespace PianoMapper.Web.Rendering;

/// <summary>
/// The part of a score's grand-staff scene that stays the same for a given
/// (score, visible measure window, verdicts) combination — everything except the playback
/// cursor line. See <see cref="GrandStaffSceneBuilder.BuildStaticScoreParts"/> and
/// <see cref="GrandStaffSceneBuilder.ComposeScore"/>.
/// </summary>
internal readonly record struct GrandStaffStaticScoreParts(
    IReadOnlyList<GrandStaffLine> Lines,
    IReadOnlyList<GrandStaffGlyph> Glyphs,
    IReadOnlyList<GrandStaffNote> Notes);
