using System.Diagnostics.CodeAnalysis;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace PianoMapper;

internal sealed class OpenNoteTracker
{
    private readonly Dictionary<Keys, PerformedNote> notesByKey = [];

    internal IReadOnlyList<Keys> ActiveKeys => notesByKey.Keys.ToArray();

    internal void Track(Keys key, PerformedNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        notesByKey[key] = note;
    }

    internal bool TryRelease(Keys key, [NotNullWhen(true)] out PerformedNote? note) =>
        notesByKey.Remove(key, out note);

    internal void Clear() => notesByKey.Clear();
}
