using Microsoft.JSInterop;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Loads and saves the fingering reach profile in browser local storage. A storage failure never throws out of this
/// type: loading falls back to the default profile and <see cref="IsStorageAvailable"/> reports it, so fingering
/// generation keeps working with the in-memory profile.
/// </summary>
internal sealed class BrowserFingeringProfileStore(IJSRuntime jsRuntime)
{
    private const string ModulePath = "./js/fingering-profile.js";
    private const string ReadFunction = "readFingeringProfileJson";
    private const string WriteFunction = "writeFingeringProfileJson";

    private IJSObjectReference? module;

    internal bool IsStorageAvailable { get; private set; } = true;

    internal async ValueTask<FingeringProfileSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<FingeringProfileStorageResult>(ReadFunction, cancellationToken);
            IsStorageAvailable = result.IsAvailable;
            return FingeringProfileSettingsJson.Parse(result.Json);
        }
        catch (JSException)
        {
            IsStorageAvailable = false;
            return FingeringProfileSettings.Default;
        }
    }

    internal async ValueTask SaveAsync(FingeringProfileSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            IJSObjectReference loadedModule = await GetModuleAsync(cancellationToken);
            var result = await loadedModule.InvokeAsync<FingeringProfileStorageResult>(
                WriteFunction,
                cancellationToken,
                FingeringProfileSettingsJson.Serialize(settings));
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
