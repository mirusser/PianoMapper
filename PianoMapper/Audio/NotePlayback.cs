namespace PianoMapper.Audio;

internal sealed record NotePlayback(PerformedNote Note, Task Completion);
