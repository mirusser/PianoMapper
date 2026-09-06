namespace PianoMapper.Server.Omr;

internal interface IAudiverisProcessRunner
{
    Task<AudiverisProcessResult> RunAsync(
        AudiverisCommand command,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
