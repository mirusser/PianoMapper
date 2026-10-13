using PianoMapper.Music;

namespace PianoMapper.Web.Playback;

internal sealed class BrowserScorePlayback(IBrowserScoreAudio audio)
{
    private static readonly TimeSpan SchedulingLead = TimeSpan.FromMilliseconds(50);
    private TimeSpan? anchor;
    private TimeSpan? pausedPosition;
    private TimeSpan completionTime;
    private Tempo? tempo;
    private IReadOnlyList<PerformedNote> scheduledNotes = [];

    /// <summary>True from a score's start until it ends or is stopped, including while it is paused.</summary>
    internal bool IsActive { get; private set; }

    internal bool IsPaused => pausedPosition.HasValue;

    /// <summary>
    /// Schedules the score from an anchor the audio engine picks when it takes the notes over, and returns it so
    /// another audio track (the click) can share its grid. Picking it here instead would leave the first notes past
    /// by the time they arrive, because building and passing a long score's events takes longer than the lead.
    /// </summary>
    internal ValueTask<TimeSpan> StartAsync(Score score, CancellationToken cancellationToken = default) =>
        PlayFromAsync(score, TimeSpan.Zero, cancellationToken);

    /// <summary>
    /// Plays a paused score on from where it paused, and returns the audio-clock time of its first beat, which lies
    /// before the clock by the score time already played. A note that was sounding at the pause is struck again for
    /// what is left of it.
    /// </summary>
    internal ValueTask<TimeSpan> ResumeAsync(Score score, CancellationToken cancellationToken = default) =>
        PlayFromAsync(score, pausedPosition ?? TimeSpan.Zero, cancellationToken);

    /// <summary>Stops the sound and holds the score at <paramref name="currentAudioTime"/> until it is resumed.</summary>
    internal async ValueTask PauseAsync(TimeSpan currentAudioTime, CancellationToken cancellationToken = default)
    {
        if (!IsActive || IsPaused || anchor is not { } scoreStart)
        {
            return;
        }

        await audio.StopScoreAsync(cancellationToken);
        TimeSpan playedUntil = currentAudioTime < completionTime ? currentAudioTime : completionTime;
        pausedPosition = playedUntil > scoreStart ? playedUntil - scoreStart : TimeSpan.Zero;
        scheduledNotes = [];
    }

    private async ValueTask<TimeSpan> PlayFromAsync(
        Score score,
        TimeSpan position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(score);
        await audio.StopScoreAsync(cancellationToken);
        var schedule = ScorePlayback.CreateSchedule(score, TimeSpan.Zero);
        var remainingEvents = schedule
            .Where(scheduledEvent => scheduledEvent.DueTime >= position ||
                scheduledEvent.DueTime + scheduledEvent.Duration > position)
            .ToArray();
        var audioEvents = remainingEvents.Select((scheduledEvent, index) =>
            {
                TimeSpan start = scheduledEvent.DueTime > position ? scheduledEvent.DueTime : position;
                return new BrowserScoreAudioEvent(
                    $"score-{index}",
                    scheduledEvent.Event.Pitch.Frequency,
                    start - position,
                    scheduledEvent.DueTime + scheduledEvent.Duration - start);
            })
            .ToArray();
        TimeSpan firstEventTime = await audio.ScheduleScoreAheadAsync(audioEvents, SchedulingLead, cancellationToken);
        TimeSpan scoreStart = firstEventTime - position;
        anchor = scoreStart;
        pausedPosition = null;
        tempo = score.Tempo;
        scheduledNotes = remainingEvents.Select((scheduledEvent, index) => PerformedNote.Completed(
            scheduledEvent.Event.Pitch,
            firstEventTime + audioEvents[index].StartTime,
            firstEventTime + audioEvents[index].StartTime + audioEvents[index].Duration)).ToArray();
        IsActive = audioEvents.Length > 0;
        completionTime = schedule.Count == 0
            ? scoreStart
            : scoreStart + schedule.Max(scheduledEvent => scheduledEvent.DueTime + scheduledEvent.Duration);
        return scoreStart;
    }

    internal double? GetCursorBeats(TimeSpan currentAudioTime)
    {
        if (GetPausedBeats() is { } pausedBeats)
        {
            return pausedBeats;
        }

        if (!anchor.HasValue || !tempo.HasValue)
        {
            return null;
        }

        if (currentAudioTime >= completionTime)
        {
            IsActive = false;
        }

        return Math.Max(0, ScorePlayback.GetCursorBeats(currentAudioTime, anchor.Value, tempo.Value));
    }

    internal IReadOnlyList<PerformedNote> GetStartedNotes(TimeSpan currentAudioTime) =>
        scheduledNotes.Where(note => note.StartTime <= currentAudioTime).ToArray();

    /// <summary>
    /// Exposes the audio-clock anchor, tempo, and completion time needed to animate the
    /// playback cursor entirely client-side (JS), instead of recomputing its position in C#
    /// on every tick.
    /// </summary>
    internal ScoreCursorAnchor? GetCursorAnchor() =>
        anchor.HasValue && tempo.HasValue
            ? new ScoreCursorAnchor(
                anchor.Value.TotalSeconds,
                tempo.Value.BeatsPerMinute,
                completionTime.TotalSeconds,
                GetPausedBeats())
            : null;

    private double? GetPausedBeats() =>
        pausedPosition is { } position && tempo is { } scoreTempo
            ? ScorePlayback.GetCursorBeats(position, TimeSpan.Zero, scoreTempo)
            : null;

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await audio.StopScoreAsync(cancellationToken);
        anchor = null;
        tempo = null;
        completionTime = default;
        scheduledNotes = [];
        pausedPosition = null;
        IsActive = false;
    }
}
