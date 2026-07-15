namespace PianoMapper.Web.Input;

internal readonly record struct BrowserKeyAction(BrowserKeyActionKind Kind, int Value = 0);
