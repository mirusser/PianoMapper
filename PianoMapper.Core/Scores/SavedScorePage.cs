namespace PianoMapper.Scores;

public sealed record SavedScorePage(IReadOnlyList<SavedScoreSummary> Scores, int TotalCount)
{
    public const int DefaultPageSize = 10;

    public const int MaximumPageSize = 100;
}
