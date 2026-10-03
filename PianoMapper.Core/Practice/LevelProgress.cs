namespace PianoMapper.Practice;

/// <summary>One level's standing, from the latest sessions that match it.</summary>
/// <param name="WindowSessionCount">How many matching sessions are in the latest-sessions window (at most the required number).</param>
/// <param name="AveragePitchPercent">Average pitch first-try rate over the window; null when there are no matching sessions.</param>
/// <param name="AverageTimingCleanPercent">Average timing-clean rate over the window; null for untimed levels or no sessions.</param>
/// <param name="RecommendedTempoPulsesPerMinute">The tempo to practise at next; null for untimed levels.</param>
public sealed record LevelProgress(
    ExerciseLevel Level,
    LevelStatus Status,
    int WindowSessionCount,
    double? AveragePitchPercent,
    double? AverageTimingCleanPercent,
    int? RecommendedTempoPulsesPerMinute);
