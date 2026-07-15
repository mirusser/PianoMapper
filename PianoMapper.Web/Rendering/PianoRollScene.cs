namespace PianoMapper.Web.Rendering;

public sealed record PianoRollScene(IReadOnlyList<PianoRollBar> Bars) : IPianoCanvasScene
{
    public PianoCanvasSceneKind Kind => PianoCanvasSceneKind.PianoRoll;
}
