namespace PianoMapper.Music;

internal sealed record Score(
    string Title,
    TimeSignature TimeSignature,
    Tempo Tempo,
    int KeyFifths,
    IReadOnlyList<ScoreMeasure> Measures);
