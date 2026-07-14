namespace PianoMapper.Music;

internal sealed record ScheduledScoreEvent(
    ScoreEvent Event,
    TimeSpan DueTime,
    TimeSpan Duration);
