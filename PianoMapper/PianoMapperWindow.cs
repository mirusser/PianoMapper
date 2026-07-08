using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Audio;
using PianoMapper.Rendering;

namespace PianoMapper;

public sealed class PianoMapperWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    private readonly AudioDispatcher audioDispatcher = new();
    private readonly NoteTimeline noteTimeline = new();
    private readonly List<NoteInstance> activeNotes = [];
    private readonly object activeNotesLock = new();
    private readonly List<Task> playingTasks = [];
    private readonly object primaryNoteLock = new();

    private int octave = 1;
    private Dictionary<Keys, Note> keyToFrequencyMap = Consts.GenerateKeyToFrequencyMapping(1);
    private PianoRollRenderer? pianoRollRenderer;
    private OscilloscopeRenderer? oscilloscopeRenderer;
    private SpectrumRenderer? spectrumRenderer;

    // The most recently triggered note; the oscilloscope/spectrum track this one
    // rather than overlaying every concurrently active note (see plan's Open Questions).
    private NoteInstance? primaryNote;

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0f, 0f, 0f, 1f);
        pianoRollRenderer = new PianoRollRenderer();
        oscilloscopeRenderer = new OscilloscopeRenderer();
        spectrumRenderer = new SpectrumRenderer();

        Console.WriteLine("Press piano keys (A, W, S, E, D, F, R, J, U, K, I, L, ;) to play notes concurrently.");
        Console.WriteLine("Press Spacebar to clear all active notes.");
        Console.WriteLine("Press Q to exit.");
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        var input = KeyboardState;

        if (input.IsKeyPressed(Keys.Escape) || input.IsKeyPressed(Keys.Q))
        {
            Console.WriteLine("Exiting...");
            Close();
            return;
        }

        if (input.IsKeyPressed(Keys.Space))
        {
            Console.WriteLine("Clearing active notes...");

            NoteInstance[] notesBeingCleared;
            lock (activeNotesLock)
            {
                notesBeingCleared = activeNotes.ToArray();
            }

            noteTimeline.Remove(notesBeingCleared);
            audioDispatcher.ClearActiveNotes(activeNotes, activeNotesLock);
            return;
        }

        int? newOctave = input switch
        {
            _ when input.IsKeyPressed(Keys.Up) => octave + 1,
            _ when input.IsKeyPressed(Keys.Down) => octave - 1,
            _ when input.IsKeyPressed(Keys.D1) => 1,
            _ when input.IsKeyPressed(Keys.D2) => 2,
            _ when input.IsKeyPressed(Keys.D3) => 3,
            _ when input.IsKeyPressed(Keys.D4) => 4,
            _ when input.IsKeyPressed(Keys.D5) => 5,
            _ when input.IsKeyPressed(Keys.D6) => 6,
            _ when input.IsKeyPressed(Keys.D7) => 7,
            _ when input.IsKeyPressed(Keys.D8) => 8,
            _ => null
        };

        if (newOctave.HasValue)
        {
            octave = newOctave.Value;
            Console.WriteLine($"Changing octave to: {octave}");
            keyToFrequencyMap = Consts.GenerateKeyToFrequencyMapping(octave);
            return;
        }

        if (input.IsKeyPressed(Keys.M))
        {
            Note[] palette = [new Note { Name = "C", Frequency = 440 }];
            _ = PlayRandomMeasureAsync(palette, 4, 4, 60, 60);
            return;
        }

        foreach (var (key, note) in keyToFrequencyMap)
        {
            if (!input.IsKeyPressed(key))
            {
                continue;
            }

            var durationInSeconds = PCM.GetTimedNoteDuration(note.Frequency, 90, 4, 1);
            Console.WriteLine($" Note: {note.Name} - Frequency: {note.Frequency}Hz - duration: {durationInSeconds}s{Environment.NewLine}");
            playingTasks.Add(PlayNoteAsync(note.Name, note.Frequency, durationInSeconds));
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit);

        var now = noteTimeline.Now;
        pianoRollRenderer?.Render(noteTimeline.Snapshot(), now);

        NoteInstance? note;
        lock (primaryNoteLock)
        {
            note = primaryNote;
        }

        if (note is not null)
        {
            bool noteIsStillPlaying = PlaybackPosition.IsNoteStillPlaying(note, now);
            if (noteIsStillPlaying)
            {
                audioDispatcher.RequestSampleOffsetRefresh(note);
            }

            var offset = audioDispatcher.TryGetSampleOffset(note, out var liveOffset)
                ? liveOffset
                : PlaybackPosition.EstimateSampleOffset(note, now);

            var window = PlaybackPosition.ExtractWindow(note.Samples, offset, Consts.ScopeWindowSize);
            oscilloscopeRenderer?.Render(window);

            var magnitudes = Fft.ComputeMagnitudes(window);
            spectrumRenderer?.Render(magnitudes);
        }

        SwapBuffers();
    }

    protected override void OnUnload()
    {
        spectrumRenderer?.Dispose();
        oscilloscopeRenderer?.Dispose();
        pianoRollRenderer?.Dispose();
        audioDispatcher.Dispose();
        base.OnUnload();
    }

    /// <summary>
    /// Plays a note asynchronously. Each note gets its own source and buffer.
    /// </summary>
    private Task PlayNoteAsync(string noteName, float frequency, float durationSeconds)
    {
        var tcs = new TaskCompletionSource<bool>();

        audioDispatcher.Enqueue(() =>
        {
            var samples = PCM.GeneratePianoWave(frequency, durationSeconds);

            int bufferId = AL.GenBuffer();
            // Using the overload that calculates size automatically.
            AL.BufferData(bufferId, ALFormat.Mono16, samples, Consts.SampleRate);
            int sourceId = AL.GenSource();
            AL.Source(sourceId, ALSourcei.Buffer, bufferId);

            // Record the note in the timeline (for rendering) and in activeNotes (for AL cleanup).
            var note = noteTimeline.Add(noteName, frequency, durationSeconds, samples, sourceId, bufferId);
            lock (activeNotesLock)
            {
                activeNotes.Add(note);
            }

            lock (primaryNoteLock)
            {
                primaryNote = note;
            }

            AL.SourcePlay(sourceId);

            // Schedule cleanup after the note duration.
            Task.Delay((int)(durationSeconds * 1000)).ContinueWith(_ =>
            {
                audioDispatcher.Enqueue(() =>
                {
                    AL.SourceStop(sourceId);
                    AL.DeleteSource(sourceId);
                    AL.DeleteBuffer(bufferId);
                    audioDispatcher.ForgetSampleOffset(note);
                    lock (activeNotesLock)
                    {
                        activeNotes.Remove(note);
                    }

                    lock (primaryNoteLock)
                    {
                        if (ReferenceEquals(primaryNote, note))
                        {
                            primaryNote = null;
                        }
                    }

                    tcs.SetResult(true);
                });
            });
        });

        return tcs.Task;
    }

    private async Task PlayRandomMeasureAsync(
        Note[] palette,
        int minNumerator = 2,
        int maxNumerator = 7,
        int minBpm = 60,
        int maxBpm = 180)
    {
        var rnd = new Random();

        // 1. Pick a random time signature
        int numerator = rnd.Next(minNumerator, maxNumerator + 1);
        int beatNoteValue = 4;

        // 2. Pick a random tempo
        int bpm = rnd.Next(minBpm, maxBpm + 1);

        Console.WriteLine($"Playing a {numerator}/{beatNoteValue} bar at {bpm} BPM...");

        // 3. How many beats we need to fill
        double beatsRemaining = numerator;

        int[] possibleDenoms = { 1, 2, 4, 8, 16 };

        // 4. Fire notes until we exactly fill the bar
        while (beatsRemaining > 0)
        {
            // Pick a random note value that will fit
            int noteDen = possibleDenoms[rnd.Next(possibleDenoms.Length)];
            double noteBeats = (double)beatNoteValue / noteDen;
            if (noteBeats > beatsRemaining)
                continue;

            // Pick a random pitch from the palette
            var note = palette[rnd.Next(palette.Length)];

            // Compute how long to play it
            float durationInSeconds = PCM.GetTimedNoteDuration(
                note.Frequency,
                bpm,
                beatNoteValue,
                noteDen);

            Console.WriteLine(
                $" Note: {note.Name} " +
                $"- Freq: {note.Frequency} Hz " +
                $"- Value: 1/{noteDen} ({noteBeats:F2} beats) " +
                $"- Dur: {durationInSeconds:F2}s");

            // Schedule playback
            playingTasks.Add(PlayNoteAsync(note.Name, note.Frequency, durationInSeconds));

            // Subtract the beats we've just used
            beatsRemaining -= noteBeats;
        }

        // 5. Wait for the bar to finish
        await Task.WhenAll(playingTasks);

        Console.WriteLine("— Measure complete —\n");
    }
}
