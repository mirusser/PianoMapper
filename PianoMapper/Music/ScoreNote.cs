namespace PianoMapper.Music;

internal sealed record ScoreNote(
    Pitch Pitch,
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff,
    bool TiesToNext = false);
