using System.Collections.Concurrent;
using OpenTK.Audio.OpenAL;
using PianoMapper.Audio;

namespace PianoMapper;

/// <summary>
/// A helper class to dispatch all OpenAL calls on a dedicated thread.
/// </summary>
internal sealed class AudioDispatcher : IAudioDispatcher
{
    private readonly Thread thread;
    private readonly Queue<Action> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private volatile bool running = true;
    private readonly Lock queLock = new();

    // Refreshed on the audio thread (the only thread allowed to touch the AL context)
    // and read directly by the render thread; a frame of staleness is acceptable for
    // a visual oscilloscope. Keyed by PerformedNote reference (not the AL source id)
    // because OpenAL reuses small integer source ids as soon as they're deleted, and a
    // refresh queued just before cleanup could otherwise resurrect a stale offset under
    // a reused id after cleanup forgets it.
    private readonly ConcurrentDictionary<PerformedNote, int> sampleOffsets = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<PerformedNote, AudioState> audioStates = new(ReferenceEqualityComparer.Instance);

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
        if (device == IntPtr.Zero)
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

        while (running || HasQueuedActions())
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
    public void ClearActiveNotes(IReadOnlyCollection<PerformedNote> activeNotes)
    {
        Enqueue(() =>
        {
            foreach (var note in activeNotes)
            {
                if (!audioStates.TryRemove(note, out var state))
                {
                    continue;
                }

                AL.SourceStop(state.SourceId);
                AL.DeleteSource(state.SourceId);
                AL.DeleteBuffer(state.BufferId);
                CheckAlError($"clearing note '{note.Pitch}'");
                sampleOffsets.TryRemove(note, out _);
            }
        });
    }

    /// <summary>
    /// Enqueues a live sample-offset query for the given note's source; the audio
    /// thread updates the cached value, which <see cref="TryGetSampleOffset"/> then
    /// reads without blocking the caller.
    /// </summary>
    public void RequestSampleOffsetRefresh(PerformedNote note)
    {
        Enqueue(() =>
        {
            if (!audioStates.TryGetValue(note, out var state))
            {
                return;
            }

            AL.GetSource(state.SourceId, ALGetSourcei.SampleOffset, out var offset);
            CheckAlError($"querying sample offset for '{note.Pitch}'");
            sampleOffsets[note] = offset;
        });
    }

    /// <summary>
    /// Reads the most recently refreshed live sample offset for a note, if any.
    /// </summary>
    public bool TryGetSampleOffset(PerformedNote note, out int sampleOffset) =>
        sampleOffsets.TryGetValue(note, out sampleOffset);

    public void RegisterAudio(PerformedNote note, short[] samples, int sourceId, int bufferId)
    {
        audioStates[note] = new AudioState(samples, sourceId, bufferId);
    }

    public void StartAudio(PerformedNote note, short[] samples, float gain)
    {
        int bufferId = AL.GenBuffer();
        CheckAlError($"generating buffer for '{note.Pitch}'");
        AL.BufferData(bufferId, ALFormat.Mono16, samples, Consts.SampleRate);
        CheckAlError($"uploading buffer data for '{note.Pitch}'");
        int sourceId = AL.GenSource();
        CheckAlError($"generating source for '{note.Pitch}'");
        AL.Source(sourceId, ALSourcei.Buffer, bufferId);
        CheckAlError($"binding buffer to source for '{note.Pitch}'");
        AL.Source(sourceId, ALSourcef.Gain, gain);
        CheckAlError($"setting gain for '{note.Pitch}'");
        RegisterAudio(note, samples, sourceId, bufferId);
        AL.SourcePlay(sourceId);
        CheckAlError($"starting playback for '{note.Pitch}'");
    }

    public void StopAudio(PerformedNote note)
    {
        if (!TryForgetAudio(note, out int sourceId, out int bufferId))
        {
            return;
        }

        AL.SourceStop(sourceId);
        AL.DeleteSource(sourceId);
        AL.DeleteBuffer(bufferId);
        CheckAlError($"cleaning up note '{note.Pitch}'");
    }

    public bool TryGetSamples(PerformedNote note, out short[] samples)
    {
        if (audioStates.TryGetValue(note, out var state))
        {
            samples = state.Samples;
            return true;
        }

        samples = [];
        return false;
    }

    public bool TryForgetAudio(PerformedNote note, out int sourceId, out int bufferId)
    {
        sampleOffsets.TryRemove(note, out _);
        if (audioStates.TryRemove(note, out var state))
        {
            sourceId = state.SourceId;
            bufferId = state.BufferId;
            return true;
        }

        sourceId = 0;
        bufferId = 0;
        return false;
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

    private bool HasQueuedActions()
    {
        lock (queLock)
        {
            return queue.Count > 0;
        }
    }

    private sealed record AudioState(short[] Samples, int SourceId, int BufferId);
}
