using System.ComponentModel;
using System.Diagnostics;

namespace PianoMapper.Server.Omr;

internal sealed class AudiverisProcessRunner : IAudiverisProcessRunner
{
    public async Task<AudiverisProcessResult> RunAsync(
        AudiverisCommand command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new AudiverisUnavailableException(
                $"Could not start Audiveris at '{command.ExecutablePath}'. Install Audiveris or configure Omr:AudiverisExecutable.",
                exception);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Audiveris did not finish within {timeout.TotalSeconds:F0} seconds.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        await standardOutput;
        return new AudiverisProcessResult(process.ExitCode, await standardError);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between HasExited and Kill.
        }
    }
}
