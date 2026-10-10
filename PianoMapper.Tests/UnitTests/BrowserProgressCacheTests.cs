using System.Text.Json;
using Microsoft.JSInterop;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserProgressCacheTests
{
    [Fact]
    public async Task ReadAsync_StoredHistory_ReturnsItAndReportsTheCacheAvailable()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v3.json"));
        var module = new FakeModule { ReadResult = new { json, isAvailable = true } };
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        ProgressCacheRead read = await cache.ReadAsync();

        Assert.True(read.IsAvailable);
        Assert.NotEmpty(read.History.Entries);
        Assert.Equal(SightReadingHistory.FromJson(json).ToJson(), read.History.ToJson());
    }

    [Fact]
    public async Task ReadAsync_NothingStored_ReturnsAnEmptyHistoryAndReportsTheCacheAvailable()
    {
        var module = new FakeModule { ReadResult = new { json = (string?)null, isAvailable = true } };
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        ProgressCacheRead read = await cache.ReadAsync();

        Assert.True(read.IsAvailable);
        Assert.Empty(read.History.Entries);
    }

    [Fact]
    public async Task ReadAsync_StorageDisabled_ReturnsAnEmptyHistoryAndReportsTheCacheUnavailable()
    {
        var module = new FakeModule { ReadResult = new { json = (string?)null, isAvailable = false } };
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        ProgressCacheRead read = await cache.ReadAsync();

        Assert.False(read.IsAvailable);
        Assert.Empty(read.History.Entries);
    }

    [Fact]
    public async Task ReadAsync_ModuleCannotBeLoaded_ReturnsAnEmptyHistoryAndReportsTheCacheUnavailable()
    {
        var cache = new BrowserProgressCache(new FakeRuntime(module: null));

        ProgressCacheRead read = await cache.ReadAsync();

        Assert.False(read.IsAvailable);
        Assert.Empty(read.History.Entries);
    }

    [Fact]
    public async Task WriteAsync_History_WritesItsJsonThroughTheModule()
    {
        SightReadingHistory history = SightReadingHistory.FromJson(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sight-reading-history-v3.json")));
        var module = new FakeModule();
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        bool isAvailable = await cache.WriteAsync(history);

        Assert.True(isAvailable);
        Assert.NotEmpty(history.Entries);
        Assert.Equal(history.ToJson(), SightReadingHistory.FromJson(module.WrittenJson).ToJson());
    }

    [Fact]
    public async Task WriteAsync_StorageFull_ReportsTheCacheUnavailableWithoutThrowing()
    {
        var module = new FakeModule { WriteResult = new { isAvailable = false } };
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        Assert.False(await cache.WriteAsync(SightReadingHistory.Empty));
    }

    [Fact]
    public async Task ClearAsync_CallsTheModuleToRemoveTheStoredHistory()
    {
        var module = new FakeModule();
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        bool isAvailable = await cache.ClearAsync();

        Assert.True(isAvailable);
        Assert.True(module.WasCleared);
    }

    [Fact]
    public async Task ClearAsync_StorageDisabled_ReportsTheCacheUnavailableWithoutThrowing()
    {
        var module = new FakeModule { ClearResult = new { isAvailable = false } };
        var cache = new BrowserProgressCache(new FakeRuntime(module));

        Assert.False(await cache.ClearAsync());
    }

    private sealed class FakeRuntime(FakeModule? module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            module is null
                ? throw new JSException("The module could not be imported.")
                : ValueTask.FromResult((TValue)(object)module);
    }

    /// <summary>Answers like the real module: a JavaScript object, read back into whatever type the caller asked for.</summary>
    private sealed class FakeModule : IJSObjectReference
    {
        internal object ReadResult { get; init; } = new { json = (string?)null, isAvailable = true };

        internal object WriteResult { get; init; } = new { isAvailable = true };

        internal object ClearResult { get; init; } = new { isAvailable = true };

        internal string? WrittenJson { get; private set; }

        internal bool WasCleared { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            object result;
            switch (identifier)
            {
                case "readHistoryJson":
                    result = ReadResult;
                    break;
                case "writeHistoryJson":
                    WrittenJson = (string?)args?[0];
                    result = WriteResult;
                    break;
                case "clearHistory":
                    WasCleared = true;
                    result = ClearResult;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected call {identifier}.");
            }

            return ValueTask.FromResult(
                JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(result), JsonSerializerOptions.Web)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
