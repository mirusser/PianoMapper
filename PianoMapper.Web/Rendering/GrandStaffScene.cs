namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffScene(
    IReadOnlyList<GrandStaffLine> Lines,
    IReadOnlyList<GrandStaffGlyph> Glyphs,
    IReadOnlyList<GrandStaffNote> Notes) : IPianoCanvasScene
{
    public PianoCanvasSceneKind Kind => PianoCanvasSceneKind.GrandStaff;
}
