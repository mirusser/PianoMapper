namespace PianoMapper.Music;

internal sealed record RandomMeasureEvent(
    Pitch Pitch,
    NoteValue NoteValue,
    double Beats);
