namespace PianoMapper.Music;

internal sealed record ScoreRest(
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff);
