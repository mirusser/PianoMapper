namespace PianoMapper.Rendering;

internal sealed record StaffGlyph(
    int Width,
    int Height,
    int AnchorRow,
    IReadOnlyList<bool> Pixels);
