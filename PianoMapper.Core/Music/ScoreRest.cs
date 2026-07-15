namespace PianoMapper.Music;

public sealed record ScoreRest(
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff);
