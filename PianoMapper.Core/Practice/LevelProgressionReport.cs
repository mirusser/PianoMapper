namespace PianoMapper.Practice;

/// <summary>The ladder as the history sees it: every level's standing and the one recommendation.</summary>
/// <param name="AllLevelsPassed">True once every level is passed; the recommendation then stays on the last level.</param>
public sealed record LevelProgressionReport(
    IReadOnlyList<LevelProgress> Levels,
    LevelRecommendation Recommended,
    bool AllLevelsPassed);
