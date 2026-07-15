namespace PianoMapper.Music;

public sealed record RandomMeasureEvent(
    Pitch Pitch,
    NoteValue NoteValue,
    double Beats);
