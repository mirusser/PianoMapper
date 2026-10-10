using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// Keeps the learner's <see cref="SightReadingHistory"/> in two places, the browser cache and the server, and hides
/// which one answered. Loading reads the cache first and returns at once, then reconciles with the server in the
/// background; recording writes the cache first, then the server; the server being absent or down only changes
/// <see cref="Status"/>, never what the learner can do. <see cref="Changed"/> tells the page when a background step
/// changed the history or the status.
/// </summary>
internal sealed class ProgressStore(IProgressCache cache, IProgressServer server)
{
    /// <summary>Raised after a background step changed <see cref="History"/> or <see cref="Status"/>.</summary>
    internal event Action? Changed;

    /// <summary>The history as it stands now; replaced by a background merge.</summary>
    internal SightReadingHistory History { get; private set; } = SightReadingHistory.Empty;

    internal ProgressStatus Status { get; private set; } = new(ProgressSyncState.Connecting, true);

    /// <summary>
    /// Completes when every server step started so far has finished. The page never waits on it; tests do. Steps run
    /// one after another, so a delete cannot overtake the sync it follows.
    /// </summary>
    internal Task PendingSync { get; private set; } = Task.CompletedTask;

    internal async ValueTask LoadAsync(CancellationToken cancellationToken = default)
    {
        ProgressCacheRead read = await cache.ReadAsync(cancellationToken);
        History = read.History;
        Status = Status with { IsCacheAvailable = read.IsAvailable };
        _ = Enqueue(() => ReconcileAsync(cancellationToken));
    }

    /// <summary>
    /// Keeps a finished session: in memory and in the browser cache at once, then on the server in the background,
    /// so a slow or missing server never holds up practising.
    /// </summary>
    internal async ValueTask RecordAsync(SightReadingSessionSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        History = History.WithCompletedSession(summary);
        await WriteCacheAsync(History, cancellationToken);
        _ = Enqueue(() => UploadRecordedAsync(summary, cancellationToken));
    }

    /// <summary>
    /// Empties the history here, in the browser cache and on the server. It waits behind any sync still running, so a
    /// server list fetched before the delete cannot bring the sessions back. A server that cannot delete leaves
    /// <see cref="ProgressSyncState.DeleteFailed"/> in <see cref="Status"/>.
    /// </summary>
    internal async ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
        await Enqueue(async () =>
        {
            History = SightReadingHistory.Empty;
            Status = Status with { IsCacheAvailable = await cache.ClearAsync(cancellationToken) };
            if (Status.Sync == ProgressSyncState.NoServer)
            {
                return;
            }

            ProgressServerReach reach = await server.ClearAsync(cancellationToken);
            Status = Status with
            {
                Sync = reach == ProgressServerReach.Unreachable ? ProgressSyncState.DeleteFailed : ToSyncState(reach),
            };
        });

    private Task Enqueue(Func<Task> work) => PendingSync = RunAfterAsync(PendingSync, work);

    private static async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        // However the previous step ended (cancelled, even faulted) it only has to be over; its own task reports how.
        await Task.WhenAny(previous);
        await work();
    }

    /// <summary>
    /// A server that took the last call gets just the new session. Otherwise (never asked, unreachable, a failed
    /// delete) a full reconcile runs instead, which uploads the new session along with any the server missed. With no
    /// server there is nothing to do.
    /// </summary>
    private async Task UploadRecordedAsync(SightReadingSessionSummary summary, CancellationToken cancellationToken)
    {
        switch (Status.Sync)
        {
            case ProgressSyncState.NoServer:
                return;
            case ProgressSyncState.Synced:
                ProgressStatus statusBefore = Status;
                ProgressServerReach reach = await server.UploadAsync([summary], cancellationToken);
                Status = Status with { Sync = ToSyncState(reach) };
                if (Status != statusBefore)
                {
                    Changed?.Invoke();
                }

                return;
            default:
                await ReconcileAsync(cancellationToken);
                return;
        }
    }

    /// <summary>
    /// Brings the server and this browser to the same history: what only the server has is added here, and what only
    /// this browser has (old browser data, or a write that failed earlier) is uploaded.
    /// </summary>
    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        Guid[] idsBefore = Ids(History);
        ProgressStatus statusBefore = Status;

        ProgressServerSessions listed = await server.ListAsync(cancellationToken);
        ProgressServerReach reach = listed.Reach;
        if (reach == ProgressServerReach.Reached)
        {
            HashSet<Guid> serverIds = listed.Sessions.Select(session => session.SessionId).ToHashSet();
            SightReadingSessionSummary[] cacheOnly = [.. History.Entries.Where(entry => !serverIds.Contains(entry.SessionId))];

            SightReadingHistory merged = History.MergedWith(listed.Sessions);
            if (!Ids(merged).SequenceEqual(Ids(History)))
            {
                History = merged;
                await WriteCacheAsync(merged, cancellationToken);
            }

            if (cacheOnly.Length > 0)
            {
                reach = await server.UploadAsync(cacheOnly, cancellationToken);
            }
        }

        Status = Status with { Sync = ToSyncState(reach) };
        if (!Ids(History).SequenceEqual(idsBefore) || Status != statusBefore)
        {
            Changed?.Invoke();
        }
    }

    private async Task WriteCacheAsync(SightReadingHistory history, CancellationToken cancellationToken)
    {
        bool isAvailable = await cache.WriteAsync(history, cancellationToken);
        Status = Status with { IsCacheAvailable = isAvailable };
    }

    private static ProgressSyncState ToSyncState(ProgressServerReach reach) => reach switch
    {
        ProgressServerReach.Reached => ProgressSyncState.Synced,
        ProgressServerReach.NoServer => ProgressSyncState.NoServer,
        _ => ProgressSyncState.NotSynced,
    };

    private static Guid[] Ids(SightReadingHistory history) => [.. history.Entries.Select(entry => entry.SessionId)];
}
