using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using PianoMapper;

// Our dedicated audio dispatcher.
AudioDispatcher audioDispatcher = new();

// A list to store all currently active notes.
List<NoteInstance> activeNotes = [];
object activeNotesLock = new object();

Console.WriteLine("Press piano keys (A, W, S, E, D, F, R, J, U, K, I, L, ;) to play notes concurrently.");
Console.WriteLine("Press Spacebar to clear all active notes.");
Console.WriteLine("Press Q to exit.");

List<Task> playingTasks = new List<Task>();

var counter = 0;
var octave = 1;
var keyToFrequencyMap = Consts.GenerateKeyToFrequencyMapping(octave);

while (true)
{
    if (Console.KeyAvailable)
    {
        var keyInfo = Console.ReadKey(intercept: true);
        var key = keyInfo.Key;
        Console.WriteLine($"{++counter} - Key pressed: {keyInfo.Key} ");

        // Check for octave change actions.
        int? newOctave = key switch
        {
            ConsoleKey.UpArrow => octave + 1,
            ConsoleKey.DownArrow => octave - 1,
            ConsoleKey.D1 => 1,
            ConsoleKey.D2 => 2,
            ConsoleKey.D3 => 3,
            ConsoleKey.D4 => 4,
            ConsoleKey.D5 => 5,
            ConsoleKey.D6 => 6,
            ConsoleKey.D7 => 7,
            ConsoleKey.D8 => 8,
            _ => null
        };

        if (newOctave.HasValue)
        {
            octave = newOctave.Value;
            Console.WriteLine($"Changing octave to: {octave}");
            keyToFrequencyMap = Consts.GenerateKeyToFrequencyMapping(octave);
            // Skip further processing on an octave-change key.
            continue;
        }

        switch (key)
        {
            // Special keys: exit and clear.
            case ConsoleKey.Q:
                Console.WriteLine("Exiting...");
                return;
            case ConsoleKey.Spacebar:
                Console.Clear();
                Console.WriteLine("Clearing active notes...");
                audioDispatcher.ClearActiveNotes(activeNotes, activeNotesLock);
                continue;
        }

        if (keyToFrequencyMap.TryGetValue(keyInfo.Key, out var note))
        {
            //var durationInSeconds = PCM.GetNoteDuration(note.Frequency);
            var durationInSeconds = PCM.GetTimedNoteDuration(note.Frequency, 60, 4, 1);
            // Fire off note playback asynchronously.
            Console.WriteLine($" Note: {note.Name} - Frequency: {note.Frequency}Hz - duration: {durationInSeconds}s {Environment.NewLine}");
            playingTasks.Add(PlayNoteAsync(note.Frequency, durationInSeconds));
        }
    }

    await Task.Yield();
}

await Task.WhenAll(playingTasks);
audioDispatcher.Dispose();

/// <summary>
/// Plays a note asynchronously. Each note gets its own source and buffer.
/// </summary>
Task PlayNoteAsync(float frequency, float durationSeconds)
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

        // Create a note instance and add it to activeNotes.
        var note = new NoteInstance { SourceId = sourceId, BufferId = bufferId };
        lock (activeNotesLock)
        {
            activeNotes.Add(note);
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
                lock (activeNotesLock)
                {
                    activeNotes.Remove(note);
                }

                tcs.SetResult(true);
            });
        });
    });

    return tcs.Task;
}
