using System.Collections.Frozen;

namespace PianoMapper.Web.Input;

internal static class BrowserKeyBindings
{
    private static readonly FrozenDictionary<string, BrowserKeyAction> Actions =
        new Dictionary<string, BrowserKeyAction>(StringComparer.Ordinal)
        {
            ["KeyC"] = new(BrowserKeyActionKind.Clear),
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
