using PianoMapper.Music;

namespace PianoMapper;

/// <summary>
/// Tracks note start, release, and pitch metadata independently of platform audio and rendering.
/// </summary>
public sealed class NoteTimeline
{
    // Keeps finished notes around long enough for a piano-roll's rolling window
    // to fully scroll them off before they're pruned.
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

    public PerformedNote Start(Pitch pitch) => Start(pitch, Now);

    public PerformedNote Start(Pitch pitch, TimeSpan startTime)
    {
        var note = new PerformedNote
        {
            Pitch = pitch,
            StartTime = startTime,
        };

        lock (notesLock)
        {
            notes.Add(note);
            PruneExpiredLocked(startTime);
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

    public IReadOnlyList<PerformedNote> Snapshot() => Snapshot(Now);

    public IReadOnlyList<PerformedNote> Snapshot(TimeSpan currentTime)
    {
        lock (notesLock)
        {
            PruneExpiredLocked(currentTime);
            return notes.ToArray();
        }
    }

    public void Prune() => Prune(Now);

    public void Prune(TimeSpan currentTime)
    {
        lock (notesLock)
        {
            PruneExpiredLocked(currentTime);
        }
    }

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

    private void PruneExpiredLocked(TimeSpan currentTime)
    {
        notes.RemoveAll(note =>
            note.ReleaseTime.HasValue &&
            currentTime.TotalSeconds - note.ReleaseTime.Value.TotalSeconds > RetentionSeconds);
    }
}
