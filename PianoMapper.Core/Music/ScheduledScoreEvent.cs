namespace PianoMapper.Music;

public sealed record ScheduledScoreEvent(
    ScoreEvent Event,
    TimeSpan DueTime,
    TimeSpan Duration);
