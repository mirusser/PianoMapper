namespace PianoMapper;

/// <summary>
/// Tracks note start/duration/pitch metadata for rendering, independent of the
/// audio thread's own active-source bookkeeping in <see cref="AudioDispatcher"/>.
/// </summary>
public sealed class NoteTimeline
{
    // Keeps finished notes around long enough for a piano-roll's rolling window
    // (Task 2.2 uses an 8s window) to fully scroll them off before they're pruned.
    // Pinned against PianoRollLayout.RollingWindowSeconds by RetentionSeconds_ExceedsPianoRollRollingWindow.
    internal const double RetentionSeconds = 15.0;

    private readonly TimeProvider timeProvider;
    private readonly long startTimestamp;
    private readonly List<NoteInstance> notes = [];
    private readonly Lock notesLock = new();

    public NoteTimeline() : this(TimeProvider.System)
    {
    }

    public NoteTimeline(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        startTimestamp = timeProvider.GetTimestamp();
    }

    public TimeSpan Now => timeProvider.GetElapsedTime(startTimestamp);

    public NoteInstance Add(string noteName, float frequency, float durationSeconds, short[] samples, int sourceId, int bufferId)
    {
        var note = new NoteInstance
        {
            NoteName = noteName,
            Frequency = frequency,
            StartTime = Now,
            Duration = durationSeconds,
            Samples = samples,
            SourceId = sourceId,
            BufferId = bufferId,
        };

        lock (notesLock)
        {
            notes.Add(note);
            PruneExpiredLocked();
        }

        return note;
    }

    public IReadOnlyList<NoteInstance> Snapshot()
    {
        lock (notesLock)
        {
            PruneExpiredLocked();
            return notes.ToArray();
        }
    }

    /// <summary>
    /// Removes specific notes from the timeline immediately, e.g. when they are
    /// stopped early (Spacebar clear) instead of finishing their own duration.
    /// </summary>
    public void Remove(IReadOnlyCollection<NoteInstance> notesToRemove)
    {
        if (notesToRemove.Count == 0)
        {
            return;
        }

        lock (notesLock)
        {
            notes.RemoveAll(notesToRemove.Contains);
        }
    }

    private void PruneExpiredLocked()
    {
        var now = Now;
        notes.RemoveAll(note => now.TotalSeconds - note.StartTime.TotalSeconds > note.Duration + RetentionSeconds);
    }
}
