using System.Collections.Concurrent;
using OpenTK.Audio.OpenAL;

namespace PianoMapper;

/// <summary>
/// A helper class to dispatch all OpenAL calls on a dedicated thread.
/// </summary>
public sealed class AudioDispatcher : IDisposable
{
    private readonly Thread thread;
    private readonly Queue<Action> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private bool running = true;
    private readonly Lock queLock = new ();

    // Refreshed on the audio thread (the only thread allowed to touch the AL context)
    // and read directly by the render thread; a frame of staleness is acceptable for
    // a visual oscilloscope. Keyed by NoteInstance reference (not the AL source id)
    // because OpenAL reuses small integer source ids as soon as they're deleted, and a
    // refresh queued just before cleanup could otherwise resurrect a stale offset under
    // a reused id after cleanup forgets it.
    private readonly ConcurrentDictionary<NoteInstance, int> sampleOffsets = new(ReferenceEqualityComparer.Instance);

    public AudioDispatcher()
    {
        thread = new Thread(Run)
        {
            IsBackground = true
        };
        thread.Start();
    }

    private void Run()
    {
        // Create OpenAL context on this thread.
        var device = ALC.OpenDevice(null);
        if (device== IntPtr.Zero)
        {
            Console.WriteLine("Failed to open audio device.");
            return;
        }

        var context = ALC.CreateContext(device, [0]);
        if (context == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create audio context.");
            ALC.CloseDevice(device);
            return;
        }

        if (!ALC.MakeContextCurrent(context))
        {
            Console.WriteLine($"Failed to activate audio context: {ALC.GetError(device)}");
            ALC.DestroyContext(context);
            ALC.CloseDevice(device);
            return;
        }
        Console.WriteLine("Audio context successfully created on the audio thread.");

        while (running)
        {
            Action? action = null;
            lock (queLock)
            {
                if (queue.Count > 0)
                    action = queue.Dequeue();
            }

            if (action != null)
            {
                action();
            }
            else
            {
                signal.WaitOne();
            }
        }

        // Cleanup
        ALC.MakeContextCurrent(context);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }
    
    /// <summary>
    /// Logs the pending OpenAL error, if any, tagged with the operation that was
    /// attempted. AL calls fail silently by default -- e.g. GenSource/GenBuffer can
    /// hand back an invalid id when the driver's source pool is exhausted under
    /// heavy polyphony -- so call this after any AL call whose failure would
    /// otherwise go unnoticed. Must be called on the audio thread, since ALError is
    /// per-context state.
    /// </summary>
    public static void CheckAlError(string context)
    {
        var error = AL.GetError();
        if (error != ALError.NoError)
        {
            Console.WriteLine($"OpenAL error during {context}: {AL.GetErrorString(error)}");
        }
    }

    /// <summary>
    /// Clears (stops and deletes) all active notes.
    /// </summary>
    public void ClearActiveNotes(List<NoteInstance> activeNotes, object activeNotesLock)
    {
        Enqueue(() =>
        {
            lock (activeNotesLock)
            {
                foreach (var note in activeNotes)
                {
                    AL.SourceStop(note.SourceId);
                    AL.DeleteSource(note.SourceId);
                    AL.DeleteBuffer(note.BufferId);
                    CheckAlError($"clearing note '{note.NoteName}'");
                    sampleOffsets.TryRemove(note, out _);
                }

                activeNotes.Clear();
            }
        });
    }

    /// <summary>
    /// Enqueues a live sample-offset query for the given note's source; the audio
    /// thread updates the cached value, which <see cref="TryGetSampleOffset"/> then
    /// reads without blocking the caller.
    /// </summary>
    public void RequestSampleOffsetRefresh(NoteInstance note)
    {
        Enqueue(() =>
        {
            AL.GetSource(note.SourceId, ALGetSourcei.SampleOffset, out var offset);
            CheckAlError($"querying sample offset for '{note.NoteName}'");
            sampleOffsets[note] = offset;
        });
    }

    /// <summary>
    /// Reads the most recently refreshed live sample offset for a note, if any.
    /// </summary>
    public bool TryGetSampleOffset(NoteInstance note, out int sampleOffset) =>
        sampleOffsets.TryGetValue(note, out sampleOffset);

    /// <summary>
    /// Drops the cached offset for a note once its source is deleted, so the
    /// dictionary doesn't grow unbounded over a long session.
    /// </summary>
    public void ForgetSampleOffset(NoteInstance note)
    {
        sampleOffsets.TryRemove(note, out _);
    }

    /// <summary>
    /// Enqueue an action to run on the audio thread.
    /// </summary>
    public void Enqueue(Action action)
    {
        lock (queLock)
        {
            queue.Enqueue(action);
        }

        signal.Set();
    }

    public void Dispose()
    {
        running = false;
        signal.Set();
        thread.Join();
        signal.Dispose();
        GC.SuppressFinalize(this);
    }
}