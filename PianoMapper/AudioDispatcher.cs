using OpenTK.Audio.OpenAL;

namespace PianoMapper;

// A helper class to dispatch all OpenAL calls on a dedicated thread.
public class AudioDispatcher : IDisposable
{
    private readonly Thread thread;
    private readonly Queue<Action> queue = new();
    private readonly AutoResetEvent signal = new(false);
    private bool running = true;
    private readonly object lockObj = new object();

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

        var context = ALC.CreateContext(device, new int[] { 0 });
        if (context == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create audio context.");
            ALC.CloseDevice(device);
            return;
        }

        ALC.MakeContextCurrent(context);
        Console.WriteLine("Audio context successfully created on the audio thread.");

        while (running)
        {
            Action action = null;
            lock (lockObj)
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
                }

                activeNotes.Clear();
            }
        });
    }

    // Enqueue an action to run on the audio thread.
    public void Enqueue(Action action)
    {
        lock (lockObj)
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
    }
}