using Microsoft.JSInterop;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Loads and saves the versioned, bounded exercise history in browser local storage. A storage failure (disabled,
/// full, or private-mode local storage) never throws out of this type — it degrades to an empty/in-memory history
/// and reports <see cref="IsStorageAvailable"/> so the page can show a non-fatal status. Practicing must keep
/// working either way.
/// </summary>
internal sealed class BrowserSightReadingHistoryStore(IJSRuntime jsRuntime)
{
    private const string ModulePath = "./js/sight-reading-history.js";

    private IJSObjectReference? module;

    internal bool IsStorageAvailable { get; private set; } = true;

    internal async ValueTask<SightReadingHistory> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<SightReadingHistoryStorageResult>(
                "readHistoryJson",
                cancellationToken);
            IsStorageAvailable = result.IsAvailable;
            return SightReadingHistory.FromJson(result.Json);
        }
        catch (JSException)
        {
            IsStorageAvailable = false;
            return SightReadingHistory.Empty;
        }
    }

    internal async ValueTask SaveAsync(SightReadingHistory history, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<SightReadingHistoryStorageResult>(
                "writeHistoryJson",
                cancellationToken,
                history.ToJson());
            IsStorageAvailable = result.IsAvailable;
        }
        catch (JSException)
        {
            IsStorageAvailable = false;
        }
    }

    internal async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<SightReadingHistoryStorageResult>(
                "clearHistory",
                cancellationToken);
            IsStorageAvailable = result.IsAvailable;
        }
        catch (JSException)
        {
            IsStorageAvailable = false;
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken) =>
        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, [ModulePath]);
}
