namespace PianoMapper.Music;

/// <summary>One coherent feasible path and the cost trade-off that ranked it.</summary>
public sealed record ScoreFingeringAlternative(Score Score, double Cost, string Explanation);
