namespace PianoMapper.Server.Omr;

internal sealed record AudiverisOptions(
    string ExecutablePath,
    TimeSpan Timeout);
