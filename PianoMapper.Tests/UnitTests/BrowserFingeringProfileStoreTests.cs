using Microsoft.JSInterop;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserFingeringProfileStoreTests
{
    [Fact]
    public async Task LoadAsync_StoredJson_ReturnsTheSettingsAndReportsStorageAvailable()
    {
        var module = new FakeModule { ReadResult = new FingeringProfileStorageResult("""{"rightComfortable":5,"rightMaximum":7,"leftComfortable":4,"leftMaximum":6}""", true) };
        var store = new BrowserFingeringProfileStore(new FakeRuntime(module));

        FingeringProfileSettings settings = await store.LoadAsync();

        Assert.Equal(new FingeringProfileSettings(5, 7, 4, 6), settings);
        Assert.True(store.IsStorageAvailable);
    }

    [Fact]
    public async Task LoadAsync_StorageDisabled_ReturnsTheDefaultAndReportsStorageUnavailable()
    {
        var module = new FakeModule { ReadResult = new FingeringProfileStorageResult(null, false) };
        var store = new BrowserFingeringProfileStore(new FakeRuntime(module));

        Assert.Equal(FingeringProfileSettings.Default, await store.LoadAsync());
        Assert.False(store.IsStorageAvailable);
    }

    [Fact]
    public async Task LoadAsync_ModuleCannotBeLoaded_ReturnsTheDefaultAndReportsStorageUnavailable()
    {
        var store = new BrowserFingeringProfileStore(new FakeRuntime(module: null));

        Assert.Equal(FingeringProfileSettings.Default, await store.LoadAsync());
        Assert.False(store.IsStorageAvailable);
    }

    [Fact]
    public async Task SaveAsync_WritesTheSettingsJsonThroughTheModule()
    {
        var module = new FakeModule();
        var store = new BrowserFingeringProfileStore(new FakeRuntime(module));
        var settings = new FingeringProfileSettings(5, 7, 4, 6);

        await store.SaveAsync(settings);

        Assert.Equal(settings, FingeringProfileSettingsJson.Parse(module.WrittenJson));
        Assert.True(store.IsStorageAvailable);
    }

    [Fact]
    public async Task SaveAsync_StorageFull_ReportsStorageUnavailableWithoutThrowing()
    {
        var module = new FakeModule { WriteResult = new FingeringProfileStorageResult(null, false) };
        var store = new BrowserFingeringProfileStore(new FakeRuntime(module));

        await store.SaveAsync(FingeringProfileSettings.Default);

        Assert.False(store.IsStorageAvailable);
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

    private sealed class FakeModule : IJSObjectReference
    {
        internal FingeringProfileStorageResult ReadResult { get; init; } = new(null, true);

        internal FingeringProfileStorageResult WriteResult { get; init; } = new(null, true);

        internal string? WrittenJson { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "readFingeringProfileJson":
                    return ValueTask.FromResult((TValue)(object)ReadResult);
                case "writeFingeringProfileJson":
                    WrittenJson = (string?)args?[0];
                    return ValueTask.FromResult((TValue)(object)WriteResult);
                default:
                    throw new InvalidOperationException($"Unexpected call {identifier}.");
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
