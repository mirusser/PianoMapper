namespace PianoMapper.Music;

public sealed record ScoreEvent(
    Pitch Pitch,
    double OnsetBeats,
    double DurationBeats,
    Staff Staff,
    IReadOnlyList<ScoreNote> SourceNotes);
