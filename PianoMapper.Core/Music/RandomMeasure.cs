namespace PianoMapper.Music;

public sealed record RandomMeasure(
    TimeSignature TimeSignature,
    Tempo Tempo,
    IReadOnlyList<RandomMeasureEvent> Events);
