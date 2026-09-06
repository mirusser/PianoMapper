namespace PianoMapper.Server.Omr;

internal sealed record AudiverisProcessResult(
    int ExitCode,
    string StandardError);
