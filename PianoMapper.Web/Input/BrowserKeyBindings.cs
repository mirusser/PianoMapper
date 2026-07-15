using System.Collections.Frozen;

namespace PianoMapper.Web.Input;

internal static class BrowserKeyBindings
{
    private static readonly FrozenDictionary<string, BrowserKeyAction> Actions =
        new Dictionary<string, BrowserKeyAction>(StringComparer.Ordinal)
        {
            ["KeyA"] = new(BrowserKeyActionKind.Note, 0),
            ["KeyW"] = new(BrowserKeyActionKind.Note, 1),
            ["KeyS"] = new(BrowserKeyActionKind.Note, 2),
            ["KeyE"] = new(BrowserKeyActionKind.Note, 3),
            ["KeyD"] = new(BrowserKeyActionKind.Note, 4),
            ["KeyF"] = new(BrowserKeyActionKind.Note, 5),
            ["KeyR"] = new(BrowserKeyActionKind.Note, 6),
            ["KeyJ"] = new(BrowserKeyActionKind.Note, 7),
            ["KeyU"] = new(BrowserKeyActionKind.Note, 8),
            ["KeyK"] = new(BrowserKeyActionKind.Note, 9),
            ["KeyI"] = new(BrowserKeyActionKind.Note, 10),
            ["KeyL"] = new(BrowserKeyActionKind.Note, 11),
            ["Semicolon"] = new(BrowserKeyActionKind.Note, 12),
            ["KeyC"] = new(BrowserKeyActionKind.Clear),
            ["KeyZ"] = new(BrowserKeyActionKind.OctaveDown),
            ["KeyX"] = new(BrowserKeyActionKind.OctaveUp),
            ["Digit1"] = new(BrowserKeyActionKind.SelectOctave, 1),
            ["Digit2"] = new(BrowserKeyActionKind.SelectOctave, 2),
            ["Digit3"] = new(BrowserKeyActionKind.SelectOctave, 3),
            ["Digit4"] = new(BrowserKeyActionKind.SelectOctave, 4),
            ["Digit5"] = new(BrowserKeyActionKind.SelectOctave, 5),
            ["Digit6"] = new(BrowserKeyActionKind.SelectOctave, 6),
            ["Digit7"] = new(BrowserKeyActionKind.SelectOctave, 7),
            ["Digit8"] = new(BrowserKeyActionKind.SelectOctave, 8),
            ["BracketLeft"] = new(BrowserKeyActionKind.PreviousMeasures),
            ["BracketRight"] = new(BrowserKeyActionKind.NextMeasures),
            ["KeyP"] = new(BrowserKeyActionKind.PlayScore),
            ["KeyT"] = new(BrowserKeyActionKind.StartPractice),
            ["KeyV"] = new(BrowserKeyActionKind.ToggleView),
            ["KeyM"] = new(BrowserKeyActionKind.PlayRandomMeasure),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static IReadOnlyList<string> HandledCodes { get; } =
        Actions.Keys.Order(StringComparer.Ordinal).ToArray();

    internal static bool TryGetAction(string code, out BrowserKeyAction action) =>
        Actions.TryGetValue(code, out action);
}
