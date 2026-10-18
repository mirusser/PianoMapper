namespace PianoMapper.Server.Omr;

/// <summary>
/// The optional alternate recognizer. A missing <paramref name="ExecutablePath"/> means it is not set up, which is the
/// default: the standard Audiveris import never depends on it.
/// </summary>
internal sealed record OemerOptions(
    string? ExecutablePath,
    TimeSpan Timeout);
