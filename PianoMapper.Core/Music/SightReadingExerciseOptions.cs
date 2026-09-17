namespace PianoMapper.Music;

public sealed record SightReadingExerciseOptions(
    Staff Staff,
    SightReadingDifficulty Difficulty,
    int MeasureCount);
