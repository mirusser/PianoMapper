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

    internal async ValueTask StartAsync(Score score, CancellationToken cancellationToken = default)
    {
        await audio.StopScoreAsync(cancellationToken);
        anchor = await audio.GetCurrentTimeAsync(cancellationToken) + SchedulingLead;
        tempo = score.Tempo;
        var schedule = ScorePlayback.CreateSchedule(score, anchor.Value);
        var audioEvents = schedule.Select((scheduledEvent, index) =>
                new BrowserScoreAudioEvent(
                    $"score-{index}",
                    scheduledEvent.Event.Pitch.Frequency,
                    scheduledEvent.DueTime,
                    scheduledEvent.Duration))
            .ToArray();
        await audio.ScheduleScoreAsync(audioEvents, cancellationToken);
        var scheduledTimeline = new NoteTimeline();
        scheduledNotes = schedule.Select(scheduledEvent =>
        {
            var note = scheduledTimeline.Start(scheduledEvent.Event.Pitch, scheduledEvent.DueTime);
            scheduledTimeline.Complete(note, scheduledEvent.DueTime + scheduledEvent.Duration);
            return note;
        }).ToArray();
        IsActive = audioEvents.Length > 0;
        completionTime = schedule.Count == 0
            ? anchor.Value
            : schedule.Max(scheduledEvent => scheduledEvent.DueTime + scheduledEvent.Duration);
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
