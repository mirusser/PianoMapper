namespace PianoMapper.Music;

internal sealed record ScoreMeasure(
    IReadOnlyList<ScoreNote> Notes,
    IReadOnlyList<ScoreRest> Rests);
