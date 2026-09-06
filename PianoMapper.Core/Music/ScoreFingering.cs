namespace PianoMapper.Music;

public sealed record ScoreFingering(
    int Number,
    ScoreFingeringPlacement? Placement = null);
