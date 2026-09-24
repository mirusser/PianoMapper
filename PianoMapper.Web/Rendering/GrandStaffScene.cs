namespace PianoMapper.Web.Rendering;

public sealed record GrandStaffScene(
    IReadOnlyList<GrandStaffLine> Lines,
    IReadOnlyList<GrandStaffGlyph> Glyphs,
    IReadOnlyList<GrandStaffNote> Notes,
    bool ShouldClipNotesAtClefs = false) : IPianoCanvasScene
{
    public PianoCanvasSceneKind Kind => PianoCanvasSceneKind.GrandStaff;

    public IReadOnlyList<GrandStaffBeam> Beams { get; init; } = [];

    public IReadOnlyList<GrandStaffTie> Ties { get; init; } = [];

    public IReadOnlyList<GrandStaffBand> Bands { get; init; } = [];

    public IReadOnlyList<GrandStaffSlur> Slurs { get; init; } = [];

    public IReadOnlyList<GrandStaffArpeggioMark> ArpeggioMarks { get; init; } = [];
}
