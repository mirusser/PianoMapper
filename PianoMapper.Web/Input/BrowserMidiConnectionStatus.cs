namespace PianoMapper.Web.Input;

public sealed class BrowserMidiConnectionStatus
{
    public bool IsSupported { get; init; }

    public bool IsPermissionRequired { get; init; }

    public IReadOnlyList<string> InputNames { get; init; } = [];

    public string? OutputName { get; init; }
}
