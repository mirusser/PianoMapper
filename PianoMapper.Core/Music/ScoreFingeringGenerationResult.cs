namespace PianoMapper.Music;

/// <summary>The preferred fingering and up to two distinct feasible alternatives.</summary>
public sealed class ScoreFingeringGenerationResult
{
    public ScoreFingeringGenerationResult(Score bestScore, IReadOnlyList<ScoreFingeringAlternative> alternatives)
    {
        ArgumentNullException.ThrowIfNull(bestScore);
        ArgumentNullException.ThrowIfNull(alternatives);
        if (alternatives.Count == 0)
        {
            throw new ArgumentException("At least one fingering alternative is required.", nameof(alternatives));
        }

        BestScore = bestScore;
        Alternatives = alternatives;
    }

    public Score BestScore { get; }

    public IReadOnlyList<ScoreFingeringAlternative> Alternatives { get; }
}
