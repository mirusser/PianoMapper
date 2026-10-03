namespace PianoMapper.Practice;

/// <summary>What to practise next: a level and, for a timed level, a tempo.</summary>
public sealed record LevelRecommendation(ExerciseLevel Level, int? TempoPulsesPerMinute);
