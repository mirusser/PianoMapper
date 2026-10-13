using System.Runtime.InteropServices;
using Microsoft.JSInterop;
using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Web.Audio;

internal sealed class WebAudioSession(IJSRuntime jsRuntime) : IBrowserScoreAudio, IBrowserMetronomeAudio, IAsyncDisposable
{
    private const string ModulePath = "./js/audio.js";
    private const string ExternalMidiSoundSourceName = "external-midi";
    internal const int DefaultNoteVelocity = 80;
    private const int PackedScoreEventValueCount = 3;

    private IJSObjectReference? module;
    private AudioClockAnchor? anchor;

    internal bool IsInitialized => anchor is not null;

    internal async ValueTask<AudioClockAnchor> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (anchor is not null)
        {
            return anchor;
        }

        IJSObjectReference audioModule = await ImportModuleAsync(cancellationToken);
        anchor = await audioModule.InvokeAsync<AudioClockAnchor>("initialize", cancellationToken);
        return anchor;
    }

    /// <summary>
    /// Asks the browser whether audio could start without a user gesture. Creates no audio context, so it is safe to
    /// call before the first click or key press.
    /// </summary>
    internal async ValueTask<bool> CanStartWithoutUserGestureAsync(CancellationToken cancellationToken = default)
    {
        IJSObjectReference audioModule = await ImportModuleAsync(cancellationToken);
        return await audioModule.InvokeAsync<bool>("canStartWithoutUserGesture", cancellationToken);
    }

    internal ValueTask StartNoteAsync(
        string noteId,
        Pitch pitch,
        TimeSpan startTime,
        bool shouldMeasureLatency = true,
        int velocity = DefaultNoteVelocity,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(velocity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(velocity, 127);

        var initializedModule = GetInitializedModule();
        double? eventPerformanceTimeMilliseconds = shouldMeasureLatency
            ? anchor!.PerformanceTimeMilliseconds +
                ((startTime.TotalSeconds - anchor.AudioTimeSeconds) * 1000)
            : null;
        return initializedModule.InvokeVoidAsync(
            "noteOn",
            cancellationToken,
            noteId,
            pitch.Frequency,
            startTime.TotalSeconds,
            eventPerformanceTimeMilliseconds,
            velocity);
    }

    internal ValueTask SetSoundSourceAsync(
        BrowserSoundSource soundSource,
        CancellationToken cancellationToken = default)
    {
        string sourceName = soundSource switch
        {
            BrowserSoundSource.Synth => "synth",
            BrowserSoundSource.Piano => "piano",
            BrowserSoundSource.ExternalMidi => ExternalMidiSoundSourceName,
            _ => throw new ArgumentOutOfRangeException(nameof(soundSource)),
        };
        return GetInitializedModule().InvokeVoidAsync("setSoundSource", cancellationToken, sourceName);
    }

    internal async ValueTask<BrowserSoundSource> GetSoundSourceAsync(
        CancellationToken cancellationToken = default)
    {
        string sourceName = await GetInitializedModule().InvokeAsync<string>(
            "getSoundSource",
            cancellationToken);
        return sourceName switch
        {
            "synth" => BrowserSoundSource.Synth,
            "piano" => BrowserSoundSource.Piano,
            ExternalMidiSoundSourceName => BrowserSoundSource.ExternalMidi,
            _ => throw new InvalidOperationException($"Unknown browser sound source: {sourceName}"),
        };
    }

    internal ValueTask StopNoteAsync(
        string noteId,
        TimeSpan releaseTime,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync(
            "noteOff",
            cancellationToken,
            noteId,
            releaseTime.TotalSeconds);

    internal ValueTask ClearAsync(
        TimeSpan releaseTime,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync(
            "clear",
            cancellationToken,
            releaseTime.TotalSeconds);

    internal ValueTask<AudioLatencySummary> GetSchedulingLatencyAsync(
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeAsync<AudioLatencySummary>(
            "getSchedulingLatency",
            cancellationToken);

    public async ValueTask<TimeSpan> GetCurrentTimeAsync(CancellationToken cancellationToken = default)
    {
        double seconds = await GetInitializedModule().InvokeAsync<double>(
            "getCurrentTime",
            cancellationToken);
        return TimeSpan.FromSeconds(seconds);
    }

    public ValueTask ScheduleScoreAsync(
        IReadOnlyList<BrowserScoreAudioEvent> events,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync("scheduleScore", cancellationToken, [CreateScoreCommands(events)]);

    public async ValueTask<TimeSpan> ScheduleScoreAheadAsync(
        IReadOnlyList<BrowserScoreAudioEvent> events,
        TimeSpan lead,
        CancellationToken cancellationToken = default)
    {
        double anchorSeconds = await GetInitializedModule().InvokeAsync<double>(
            "scheduleScoreAhead",
            cancellationToken,
            [PackScoreEvents(events), lead.TotalSeconds, DefaultNoteVelocity]);
        return TimeSpan.FromSeconds(anchorSeconds);
    }

    public ValueTask StopScoreAsync(CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync("stopScore", cancellationToken);

    public ValueTask StartMetronomeAsync(
        TimeSpan anchor,
        TimeSpan beatDuration,
        int beatsPerMeasure,
        IReadOnlyList<int> groupStartBeatIndices,
        double volume,
        MetronomeTimbre timbre,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync(
            "startMetronome",
            cancellationToken,
            anchor.TotalSeconds,
            beatDuration.TotalSeconds,
            beatsPerMeasure,
            groupStartBeatIndices,
            volume,
            timbre.ToString().ToLowerInvariant());

    public ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync("stopMetronome", cancellationToken);

    public ValueTask SetMetronomeSoundAsync(
        double volume,
        MetronomeTimbre timbre,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync(
            "setMetronomeSound",
            cancellationToken,
            volume,
            timbre.ToString().ToLowerInvariant());

    public async ValueTask DisposeAsync()
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("dispose");
        await module.DisposeAsync();
    }

    private static object[] CreateScoreCommands(IReadOnlyList<BrowserScoreAudioEvent> events) =>
        events.Select(scoreEvent => (object)new
        {
            scoreEvent.NoteId,
            scoreEvent.Frequency,
            Velocity = DefaultNoteVelocity,
            StartTimeSeconds = scoreEvent.StartTime.TotalSeconds,
            DurationSeconds = scoreEvent.Duration.TotalSeconds,
        }).ToArray();

    // Frequency, start and duration of every event as one byte array, which Blazor passes across in a few milliseconds
    // where a thousand event objects took a tenth of a second of WebAssembly time. The audio engine names the notes by
    // their position, so their ids are not sent.
    private static byte[] PackScoreEvents(IReadOnlyList<BrowserScoreAudioEvent> events)
    {
        var values = new double[events.Count * PackedScoreEventValueCount];
        for (int index = 0; index < events.Count; index++)
        {
            values[index * PackedScoreEventValueCount] = events[index].Frequency;
            values[(index * PackedScoreEventValueCount) + 1] = events[index].StartTime.TotalSeconds;
            values[(index * PackedScoreEventValueCount) + 2] = events[index].Duration.TotalSeconds;
        }

        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private async ValueTask<IJSObjectReference> ImportModuleAsync(CancellationToken cancellationToken) =>
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            [ModulePath]);

    private IJSObjectReference GetInitializedModule() =>
        IsInitialized
            ? module!
            : throw new InvalidOperationException("Audio is not initialized.");
}
