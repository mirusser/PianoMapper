using Microsoft.JSInterop;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The learner's history in browser local storage, through <c>sight-reading-history.js</c> (which owns the storage
/// key). A storage failure (disabled, full or private-mode local storage) never throws out of this type: it reads as
/// an empty, unavailable cache so <see cref="ProgressStore"/> can carry on from memory and the server.
/// </summary>
internal sealed class BrowserProgressCache(IJSRuntime jsRuntime) : IProgressCache
{
    private const string ModulePath = "./js/sight-reading-history.js";

    private IJSObjectReference? module;

    public async ValueTask<ProgressCacheRead> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<StorageResult>("readHistoryJson", cancellationToken);
            return new ProgressCacheRead(SightReadingHistory.FromJson(result.Json), result.IsAvailable);
        }
        catch (JSException)
        {
            return new ProgressCacheRead(SightReadingHistory.Empty, IsAvailable: false);
        }
    }

    public async ValueTask<bool> WriteAsync(SightReadingHistory history, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        return await InvokeAsync("writeHistoryJson", cancellationToken, history.ToJson());
    }

    public async ValueTask<bool> ClearAsync(CancellationToken cancellationToken = default) =>
        await InvokeAsync("clearHistory", cancellationToken);

    private async ValueTask<bool> InvokeAsync(string function, CancellationToken cancellationToken, params object?[] arguments)
    {
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<StorageResult>(function, cancellationToken, arguments);
            return result.IsAvailable;
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken) =>
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, [ModulePath]);

    /// <summary>What the module's functions return: the stored text (reads only) and whether storage could be used.</summary>
    private sealed record StorageResult(string? Json, bool IsAvailable);
}
