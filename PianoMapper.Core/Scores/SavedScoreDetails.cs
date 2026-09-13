using PianoMapper.Music;

namespace PianoMapper.Scores;

public sealed record SavedScoreDetails(
    Guid Id,
    Score Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
