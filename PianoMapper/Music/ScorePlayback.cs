namespace PianoMapper.Music;

internal static class ScorePlayback
{
    public static IReadOnlyList<ScheduledScoreEvent> CreateSchedule(Score score, TimeSpan anchor) =>
        ScoreDerivation.Flatten(score)
            .Select(scoreEvent => new ScheduledScoreEvent(
                scoreEvent,
                anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, score.Tempo),
                MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, score.Tempo)))
            .ToArray();

    public static IReadOnlyList<ScheduledScoreEvent> GetDueEvents(
        IReadOnlyList<ScheduledScoreEvent> schedule,
        TimeSpan now,
        int startIndex) =>
        schedule.Skip(startIndex).TakeWhile(item => item.DueTime <= now).ToArray();

    public static double GetCursorBeats(TimeSpan now, TimeSpan anchor, Tempo tempo) =>
        MusicalTime.DurationToBeats(now - anchor, tempo);
}
