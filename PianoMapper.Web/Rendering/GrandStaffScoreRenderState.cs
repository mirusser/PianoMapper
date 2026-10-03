namespace PianoMapper.Web.Rendering;

/// <summary>
/// A reference-stable score scene and its independently changing overlay.
/// </summary>
internal readonly record struct GrandStaffScoreRenderState(
    GrandStaffScene Scene,
    GrandStaffScoreOverlay Overlay,
    GrandStaffStaticScoreParts StaticParts);
