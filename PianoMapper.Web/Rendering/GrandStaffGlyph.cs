using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffGlyph(
    string Text,
    double X,
    double Y,
    GrandStaffGlyphKind Kind,
    double? Height = null,
    bool IsActive = false,
    Verdict? Verdict = null);
