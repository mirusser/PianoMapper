namespace PianoMapper.Scores;

public sealed record SavedScoreSummary(
    Guid Id,
    string Title,
    int MeasureCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
