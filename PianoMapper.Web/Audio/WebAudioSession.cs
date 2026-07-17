using Microsoft.JSInterop;
using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Web.Audio;

internal sealed class WebAudioSession(IJSRuntime jsRuntime) : IBrowserScoreAudio, IBrowserMetronomeAudio, IAsyncDisposable
{
    private const string ModulePath = "./js/audio.js";
    internal const int DefaultNoteVelocity = 80;

    private IJSObjectReference? module;
    private AudioClockAnchor? anchor;

    internal bool IsInitialized => anchor is not null;

    internal async ValueTask<AudioClockAnchor> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (anchor is not null)
        {
            return anchor;
        }

        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            [ModulePath]);
        anchor = await module.InvokeAsync<AudioClockAnchor>("initialize", cancellationToken);
        return anchor;
    }

    internal ValueTask StartNoteAsync(
        string noteId,
        Pitch pitch,
        TimeSpan startTime,
        bool shouldMeasureLatency = true,
        CancellationToken cancellationToken = default)
    {
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
            DefaultNoteVelocity);
    }

    internal ValueTask SetSoundSourceAsync(
        BrowserSoundSource soundSource,
        CancellationToken cancellationToken = default)
    {
        string sourceName = soundSource switch
        {
            BrowserSoundSource.Synth => "synth",
            BrowserSoundSource.Piano => "piano",
            _ => throw new ArgumentOutOfRangeException(nameof(soundSource)),
        };
        return GetInitializedModule().InvokeVoidAsync("setSoundSource", cancellationToken, sourceName);
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
        CancellationToken cancellationToken = default)
    {
        var commands = events.Select(scoreEvent => new
        {
            scoreEvent.NoteId,
            scoreEvent.Frequency,
            Velocity = DefaultNoteVelocity,
            StartTimeSeconds = scoreEvent.StartTime.TotalSeconds,
            DurationSeconds = scoreEvent.Duration.TotalSeconds,
        }).ToArray();
        return GetInitializedModule().InvokeVoidAsync("scheduleScore", cancellationToken, [commands]);
    }

    public ValueTask StopScoreAsync(CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync("stopScore", cancellationToken);

    public ValueTask StartMetronomeAsync(
        TimeSpan anchor,
        TimeSpan beatDuration,
        int beatsPerMeasure,
        CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync(
            "startMetronome",
            cancellationToken,
            anchor.TotalSeconds,
            beatDuration.TotalSeconds,
            beatsPerMeasure);

    public ValueTask StopMetronomeAsync(CancellationToken cancellationToken = default) =>
        GetInitializedModule().InvokeVoidAsync("stopMetronome", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("dispose");
        await module.DisposeAsync();
    }

    private IJSObjectReference GetInitializedModule() =>
        IsInitialized
            ? module!
            : throw new InvalidOperationException("Audio is not initialized.");
}
