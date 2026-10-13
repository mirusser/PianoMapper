using PianoMapper.Music;

namespace PianoMapper.Web.Playback;

internal sealed class BrowserScorePlayback(IBrowserScoreAudio audio)
{
    private static readonly TimeSpan SchedulingLead = TimeSpan.FromMilliseconds(50);
    private TimeSpan? anchor;
    private TimeSpan completionTime;
    private Tempo? tempo;
    private IReadOnlyList<PerformedNote> scheduledNotes = [];

    internal bool IsActive { get; private set; }

    /// <summary>
    /// Schedules the score from an anchor the audio engine picks when it takes the notes over, and returns it so
    /// another audio track (the click) can share its grid. Picking it here instead would leave the first notes past
    /// by the time they arrive, because building and passing a long score's events takes longer than the lead.
    /// </summary>
    internal async ValueTask<TimeSpan> StartAsync(Score score, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(score);
        await audio.StopScoreAsync(cancellationToken);
        var schedule = ScorePlayback.CreateSchedule(score, TimeSpan.Zero);
        var audioEvents = schedule.Select((scheduledEvent, index) =>
                new BrowserScoreAudioEvent(
                    $"score-{index}",
                    scheduledEvent.Event.Pitch.Frequency,
                    scheduledEvent.DueTime,
                    scheduledEvent.Duration))
            .ToArray();
        TimeSpan audioClockAnchor = await audio.ScheduleScoreAheadAsync(audioEvents, SchedulingLead, cancellationToken);
        anchor = audioClockAnchor;
        tempo = score.Tempo;
        scheduledNotes = schedule.Select(scheduledEvent => PerformedNote.Completed(
            scheduledEvent.Event.Pitch,
            audioClockAnchor + scheduledEvent.DueTime,
            audioClockAnchor + scheduledEvent.DueTime + scheduledEvent.Duration)).ToArray();
        IsActive = audioEvents.Length > 0;
        completionTime = schedule.Count == 0
            ? audioClockAnchor
            : audioClockAnchor + schedule.Max(scheduledEvent => scheduledEvent.DueTime + scheduledEvent.Duration);
        return audioClockAnchor;
    }

    internal double? GetCursorBeats(TimeSpan currentAudioTime)
    {
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
            ? new ScoreCursorAnchor(anchor.Value.TotalSeconds, tempo.Value.BeatsPerMinute, completionTime.TotalSeconds)
            : null;

    internal async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await audio.StopScoreAsync(cancellationToken);
        anchor = null;
        tempo = null;
        completionTime = default;
        scheduledNotes = [];
        IsActive = false;
    }
}
