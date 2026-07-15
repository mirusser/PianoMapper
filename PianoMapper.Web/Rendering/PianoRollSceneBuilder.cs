using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

internal static class PianoRollSceneBuilder
{
    internal static PianoRollScene Build(IReadOnlyList<PerformedNote> notes, TimeSpan currentTime)
    {
        var bars = notes
            .Select(note => (Note: note, Rect: PianoRollLayout.GetBarRect(note, currentTime)))
            .Where(item => item.Rect.HasValue)
            .Select(item => new PianoRollBar(
                item.Rect!.Value,
                item.Note.Pitch.ToString(),
                item.Note.ReleaseTime is null))
            .ToArray();
        return new PianoRollScene(bars);
    }
}
