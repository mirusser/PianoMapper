namespace PianoMapper.Music;

public sealed record ScoreMeasure(
    IReadOnlyList<ScoreNote> Notes,
    IReadOnlyList<ScoreRest> Rests);
