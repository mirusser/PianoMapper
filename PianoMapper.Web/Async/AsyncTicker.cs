namespace PianoMapper.Web.Async;

/// <summary>
/// Runs a caller-supplied tick on a fixed interval until the tick reports it should stop or
/// <see cref="StopAsync"/> is called. Owns the start/cancel/await/dispose lifecycle of the
/// backing <see cref="CancellationTokenSource"/> and <see cref="Task"/> so callers don't
/// reimplement it once per polling loop.
/// </summary>
internal sealed class AsyncTicker
{
    private CancellationTokenSource? cancellation;
    private Task? task;

    /// <summary>True while a tick loop is running (not yet stopped or naturally completed).</summary>
    public bool IsRunning => task is { IsCompleted: false };

    /// <summary>
    /// Starts ticking <paramref name="tickAsync"/> every <paramref name="interval"/> until it
    /// returns <see langword="false"/> or <see cref="StopAsync"/> is called. Any previous run's
    /// cancellation source is disposed first; callers are responsible for awaiting
    /// <see cref="StopAsync"/> before starting again while a run may still be in flight.
    /// </summary>
    public void Start(Func<CancellationToken, Task<bool>> tickAsync, TimeSpan interval)
    {
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        task = RunAsync(tickAsync, interval, cancellation.Token);
    }

    private static async Task RunAsync(
        Func<CancellationToken, Task<bool>> tickAsync,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool shouldContinue = await tickAsync(cancellationToken);
                if (!shouldContinue)
                {
                    break;
                }

                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task StopAsync()
    {
        if (cancellation is null)
        {
            return;
        }

        await cancellation.CancelAsync();
        if (task is not null)
        {
            await task;
        }

        cancellation.Dispose();
        cancellation = null;
        task = null;
    }
}
