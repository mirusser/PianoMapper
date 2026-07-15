using Microsoft.JSInterop;
using PianoMapper.Music;
using PianoMapper.Web.Audio;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class WebAudioSessionTests
{
    [Fact]
    public async Task InitializeAsync_FirstCall_ImportsModuleBeforeInitializingAudio()
    {
        var calls = new List<string>();
        var expectedAnchor = new AudioClockAnchor(1250, 4.5);
        var module = new RecordingJsModule(calls, expectedAnchor);
        var runtime = new RecordingJsRuntime(calls, module);
        await using var session = new WebAudioSession(runtime);

        var anchor = await session.InitializeAsync();

        Assert.Equal(expectedAnchor, anchor);
        Assert.Equal(
            ["runtime:import:./js/audio.js", "module:initialize"],
            calls.Take(2));
    }

    [Fact]
    public async Task NoteCommands_ReplayedNoteThenClear_PreserveCommandOrder()
    {
        var calls = new List<string>();
        var module = new RecordingJsModule(calls, new AudioClockAnchor(1000, 2));
        var runtime = new RecordingJsRuntime(calls, module);
        await using var session = new WebAudioSession(runtime);
        var pitch = new Pitch(NoteLetter.C, 0, 4);
        await session.InitializeAsync();

        await session.StartNoteAsync("KeyA", pitch, TimeSpan.FromSeconds(2));
        await session.StopNoteAsync("KeyA", TimeSpan.FromSeconds(2.25));
        await session.StartNoteAsync("KeyA", pitch, TimeSpan.FromSeconds(2.5));
        await session.ClearAsync(TimeSpan.FromSeconds(2.75));

        Assert.Equal(
            [
                "runtime:import:./js/audio.js",
                "module:initialize",
                "module:noteOn",
                "module:noteOff",
                "module:noteOn",
                "module:clear",
            ],
            calls.Take(6));
    }

    [Fact]
    public async Task GetSchedulingLatencyAsync_InitializedSession_RequestsOneSummary()
    {
        var calls = new List<string>();
        var module = new RecordingJsModule(calls, new AudioClockAnchor(1000, 2));
        var runtime = new RecordingJsRuntime(calls, module);
        await using var session = new WebAudioSession(runtime);
        await session.InitializeAsync();

        await session.GetSchedulingLatencyAsync();

        Assert.Contains("module:getSchedulingLatency", calls);
    }

    [Fact]
    public async Task ScoreCommands_InitializedSession_QueryClockThenScheduleAndStopBatch()
    {
        var calls = new List<string>();
        var module = new RecordingJsModule(calls, new AudioClockAnchor(1000, 2));
        var runtime = new RecordingJsRuntime(calls, module);
        await using var session = new WebAudioSession(runtime);
        await session.InitializeAsync();
        BrowserScoreAudioEvent[] events =
        [
            new("score-0", 440, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(0.5)),
            new("score-1", 660, TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(0.25)),
        ];

        TimeSpan currentTime = await session.GetCurrentTimeAsync();
        await session.ScheduleScoreAsync(events);
        await session.StopScoreAsync();

        Assert.Equal(TimeSpan.FromSeconds(3), currentTime);
        Assert.Contains("module:scheduleScore", calls);
        Assert.Contains("module:stopScore", calls);
        var scheduleArguments = Assert.Single(module.Arguments["scheduleScore"]!);
        var scheduleBatch = Assert.IsAssignableFrom<Array>(scheduleArguments);
        Assert.Equal(2, scheduleBatch.Length);
    }

    private sealed class RecordingJsRuntime(List<string> calls, IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            calls.Add($"runtime:{identifier}:{args?[0]}");
            return ValueTask.FromResult((TValue)module);
        }
    }

    private sealed class RecordingJsModule(List<string> calls, AudioClockAnchor anchor) : IJSObjectReference
    {
        internal Dictionary<string, object?[]?> Arguments { get; } = new(StringComparer.Ordinal);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            calls.Add($"module:{identifier}");
            Arguments[identifier] = args;
            object? result = identifier switch
            {
                "initialize" => anchor,
                "getCurrentTime" => 3.0,
                _ => default(TValue),
            };
            return ValueTask.FromResult((TValue)result!);
        }

        public ValueTask DisposeAsync()
        {
            calls.Add("module:dispose-reference");
            return ValueTask.CompletedTask;
        }
    }
}
