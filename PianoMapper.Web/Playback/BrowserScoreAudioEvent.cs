namespace PianoMapper.Web.Playback;

internal sealed record BrowserScoreAudioEvent(
    string NoteId,
    double Frequency,
    TimeSpan StartTime,
    TimeSpan Duration);
