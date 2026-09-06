namespace PianoMapper.Server.Omr;

internal sealed record AudiverisCommand(
    string ExecutablePath,
    IReadOnlyList<string> Arguments);
