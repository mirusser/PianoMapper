namespace PianoMapper;

using PianoMapper.Music;

/// <summary>
/// Tracks note start/duration/pitch metadata for rendering, independent of the
/// audio thread's own active-source bookkeeping in <see cref="AudioDispatcher"/>.
/// </summary>
internal sealed class NoteTimeline
{
    // Keeps finished notes around long enough for a piano-roll's rolling window
    // (Task 2.2 uses an 8s window) to fully scroll them off before they're pruned.
    // Pinned against PianoRollLayout.RollingWindowSeconds by RetentionSeconds_ExceedsPianoRollRollingWindow.
    internal const double RetentionSeconds = 15.0;

    private readonly TimeProvider timeProvider;
    private readonly long startTimestamp;
    private readonly List<PerformedNote> notes = [];
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

    public PerformedNote Start(Pitch pitch)
    {
        var note = new PerformedNote
        {
            Pitch = pitch,
            StartTime = Now,
        };

        lock (notesLock)
        {
            notes.Add(note);
            PruneExpiredLocked();
        }

        return note;
    }

    public void Complete(PerformedNote note, TimeSpan releaseTime)
    {
        lock (notesLock)
        {
            if (notes.Any(candidate => ReferenceEquals(candidate, note)))
            {
                note.ReleaseTime = releaseTime;
            }
        }
    }

    public IReadOnlyList<PerformedNote> Snapshot()
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
    public void Remove(IReadOnlyCollection<PerformedNote> notesToRemove)
    {
        if (notesToRemove.Count == 0)
        {
            return;
        }

        lock (notesLock)
        {
            var identities = new HashSet<PerformedNote>(notesToRemove, ReferenceEqualityComparer.Instance);
            notes.RemoveAll(identities.Contains);
        }
    }

    private void PruneExpiredLocked()
    {
        var now = Now;
        notes.RemoveAll(note =>
            note.ReleaseTime.HasValue &&
            now.TotalSeconds - note.ReleaseTime.Value.TotalSeconds > RetentionSeconds);
    }
}
