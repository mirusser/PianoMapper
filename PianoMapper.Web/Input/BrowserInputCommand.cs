using PianoMapper.Music;

namespace PianoMapper.Web.Input;

internal readonly record struct BrowserInputCommand(
    BrowserInputCommandKind Kind,
    bool IsHandled,
    string? NoteId = null,
    Pitch? Pitch = null,
    PerformedNote? Note = null,
    TimeSpan EventTime = default);
