using PianoMapper.Music;

namespace PianoMapper.Audio;

internal sealed class Instrument : IDisposable
{
    private readonly NoteTimeline noteTimeline;
    private readonly TimeProvider timeProvider;
    private readonly IAudioDispatcher audioDispatcher;
    private readonly List<PerformedNote> activeNotes = [];
    private readonly Dictionary<PerformedNote, TaskCompletionSource> completions = new(ReferenceEqualityComparer.Instance);
    private readonly object activeNotesLock = new();
    private readonly object primaryNoteLock = new();
    private readonly CancellationTokenSource cleanupCancellation = new();

    private PerformedNote? primaryNote;
    private bool disposed;

    public Instrument(NoteTimeline noteTimeline)
        : this(noteTimeline, TimeProvider.System, new AudioDispatcher())
    {
    }

    internal Instrument(NoteTimeline noteTimeline, TimeProvider timeProvider, IAudioDispatcher audioDispatcher)
    {
        this.noteTimeline = noteTimeline;
        this.timeProvider = timeProvider;
        this.audioDispatcher = audioDispatcher;
    }

    public NotePlayback Play(Pitch pitch, TimeSpan duration) =>
        BeginPlayback(pitch, duration, hasScheduledRelease: true);

    public NotePlayback NoteOn(Pitch pitch) =>
        BeginPlayback(pitch, GetNaturalDuration(pitch), hasScheduledRelease: false);

    public void NoteOff(PerformedNote note)
    {
        TaskCompletionSource? completion;
        lock (activeNotesLock)
        {
            completions.TryGetValue(note, out completion);
        }

        if (completion is null)
        {
            return;
        }

        var releaseTime = noteTimeline.Now;
        noteTimeline.Complete(note, releaseTime);
        audioDispatcher.Enqueue(() => CleanupPlayback(note, completion));
    }

    internal static TimeSpan GetNaturalDuration(Pitch pitch) =>
        TimeSpan.FromSeconds(PCM.NaturalDecaySeconds(pitch.Frequency));

    private NotePlayback BeginPlayback(Pitch pitch, TimeSpan duration, bool hasScheduledRelease)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var note = noteTimeline.Start(pitch);
        if (hasScheduledRelease)
        {
            noteTimeline.Complete(note, note.StartTime + duration);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int polyphony;

        lock (activeNotesLock)
        {
            activeNotes.Add(note);
            completions.Add(note, completion);
            polyphony = activeNotes.Count;
        }

        lock (primaryNoteLock)
        {
            primaryNote = note;
        }

        audioDispatcher.Enqueue(() => StartPlayback(note, duration, polyphony, completion));
        return new NotePlayback(note, completion.Task);
    }

    public void ClearAll()
    {
        PerformedNote[] notesToClear;
        lock (activeNotesLock)
        {
            notesToClear = activeNotes.ToArray();
            activeNotes.Clear();
            foreach (var note in notesToClear)
            {
                if (completions.Remove(note, out var completion))
                {
                    completion.TrySetResult();
                }
            }
        }

        lock (primaryNoteLock)
        {
            if (primaryNote is not null && notesToClear.Any(note => ReferenceEquals(note, primaryNote)))
            {
                primaryNote = null;
            }
        }

        noteTimeline.Remove(notesToClear);
        audioDispatcher.ClearActiveNotes(notesToClear);
    }

    public bool TryGetPrimarySampleWindow(TimeSpan now, int windowSize, out short[] sampleWindow)
    {
        PerformedNote? note;
        lock (primaryNoteLock)
        {
            note = primaryNote;
        }

        if (note is null || !audioDispatcher.TryGetSamples(note, out var samples))
        {
            sampleWindow = [];
            return false;
        }

        if (PlaybackPosition.IsNoteStillPlaying(note, now))
        {
            audioDispatcher.RequestSampleOffsetRefresh(note);
        }

        int offset = audioDispatcher.TryGetSampleOffset(note, out int liveOffset)
            ? liveOffset
            : PlaybackPosition.EstimateSampleOffset(note, now, samples.Length);
        sampleWindow = PlaybackPosition.ExtractWindow(samples, offset, windowSize);
        return true;
    }

    internal static float CalculateGain(int polyphony)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(polyphony);
        return (float)Math.Min(1.0, 1.0 / Math.Sqrt(polyphony));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cleanupCancellation.Cancel();
        ClearAll();
        audioDispatcher.Dispose();
        cleanupCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StartPlayback(PerformedNote note, TimeSpan duration, int polyphony, TaskCompletionSource completion)
    {
        float frequency = (float)note.Pitch.Frequency;
        var samples = PCM.GeneratePianoWave(frequency, (float)duration.TotalSeconds);
        audioDispatcher.StartAudio(note, samples, CalculateGain(polyphony));

        _ = CompleteAfterDelayAsync(note, duration, completion, cleanupCancellation.Token);
    }

    private async Task CompleteAfterDelayAsync(
        PerformedNote note,
        TimeSpan duration,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, timeProvider, cancellationToken).ConfigureAwait(false);
            noteTimeline.Complete(note, note.StartTime + duration);
            audioDispatcher.Enqueue(() => CleanupPlayback(note, completion));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetResult();
        }
    }

    private void CleanupPlayback(PerformedNote note, TaskCompletionSource completion)
    {
        audioDispatcher.StopAudio(note);

        lock (activeNotesLock)
        {
            activeNotes.RemoveAll(candidate => ReferenceEquals(candidate, note));
            completions.Remove(note);
        }

        lock (primaryNoteLock)
        {
            if (ReferenceEquals(primaryNote, note))
            {
                primaryNote = null;
            }
        }

        completion.TrySetResult();
    }
}
