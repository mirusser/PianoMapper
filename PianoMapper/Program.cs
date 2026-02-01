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

List<Task> playingTasks = [];

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
            case ConsoleKey.M:
                Note[] pallette = { new Note { Name = "C", Frequency = 440}};
                await PlayRandomMeasureAsync(pallette, 4,4, 60, 60);
                continue;
        }

        if (keyToFrequencyMap.TryGetValue(keyInfo.Key, out var note))
        {
            //var durationInSeconds = PCM.GetNoteDuration(note.Frequency);
            var durationInSeconds = PCM.GetTimedNoteDuration(note.Frequency, 90, 4, 1);
            // Fire off note playback asynchronously.
            Console.WriteLine($" Note: {note.Name} - Frequency: {note.Frequency}Hz - duration: {durationInSeconds}s {Environment.NewLine}");
            playingTasks.Add(PlayNoteAsync(note.Frequency, durationInSeconds));
            
            var samples = PCM.GenerateSineWave(note.Frequency, durationInSeconds);
            PCM.VisualizeWave(samples);
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

async Task PlayRandomMeasureAsync(
    Note[] palette,
    int minNumerator = 2,
    int maxNumerator = 7,
    int minBpm = 60,
    int maxBpm = 180)
{
    var rnd = new Random();

    // 1. Pick a random time signature
    int numerator = rnd.Next(minNumerator, maxNumerator + 1);
    int[] possibleDenoms = { 1, 2, 4, 8, 16 };
    //int beatNoteValue = possibleDenoms[rnd.Next(possibleDenoms.Length)];
    int beatNoteValue = 4;

    // 2. Pick a random tempo
    int bpm = rnd.Next(minBpm, maxBpm + 1);

    Console.WriteLine(
        $"Playing a {numerator}/{beatNoteValue} bar at {bpm} BPM...");

    // 3. How many beats we need to fill
    double beatsRemaining = numerator;

    // 4. Fire notes until we exactly fill the bar
    //var playingTasks = new List<Task>();
    
    while (beatsRemaining > 0)
    {
        // Pick a random note value that will fit
        int noteDen = possibleDenoms[rnd.Next(possibleDenoms.Length)];
        double noteBeats  = (double)beatNoteValue / noteDen;
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
            $"- Freq: {note.Frequency} Hz " +
            $"- Value: 1/{noteDen} ({noteBeats:F2} beats) " +
            $"- Dur: {durationInSeconds:F2}s");

        // Schedule playback
        playingTasks.Add(PlayNoteAsync(note.Frequency, durationInSeconds));

        // Subtract the beats we’ve just used
        beatsRemaining -= noteBeats;
    }

    // 5. Wait for the bar to finish
    await Task.WhenAll(playingTasks);

    Console.WriteLine("— Measure complete —\n");
}
