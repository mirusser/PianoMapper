namespace PianoMapper.Music;

internal sealed record ScoreEvent(
    Pitch Pitch,
    double OnsetBeats,
    double DurationBeats,
    Staff Staff,
    IReadOnlyList<ScoreNote> SourceNotes);
