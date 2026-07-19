namespace PianoMapper.Music;

public sealed record ScoreNote(
    Pitch Pitch,
    NoteValue NoteValue,
    int MeasureIndex,
    double BeatOffset,
    Staff Staff,
    bool TiesToNext = false,
    BeamState BeamState = BeamState.None);
