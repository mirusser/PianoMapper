namespace PianoMapper.Music;

internal sealed record RandomMeasure(
    TimeSignature TimeSignature,
    Tempo Tempo,
    IReadOnlyList<RandomMeasureEvent> Events);
