using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ProgressStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_CachedHistory_ReturnsItBeforeTheServerAnswers()
    {
        SightReadingSessionSummary cached = CreateSummary(1);
        var gate = new TaskCompletionSource();
        var server = new InMemoryProgressServer { ListGate = gate.Task };
        var store = new ProgressStore(new InMemoryProgressCache(CreateHistory(cached)), server);

        await store.LoadAsync();

        Assert.Equal([cached.SessionId], Ids(store.History));
        Assert.False(store.PendingSync.IsCompleted);

        gate.SetResult();
        await store.PendingSync;
    }

    [Fact]
    public async Task LoadAsync_ServerHoldsOtherSessions_MergesThemNotifiesOnceAndWritesTheResultToTheCache()
    {
        SightReadingSessionSummary cached = CreateSummary(1);
        SightReadingSessionSummary serverOnly = CreateSummary(2);
        var cache = new InMemoryProgressCache(CreateHistory(cached));
        var store = new ProgressStore(cache, new InMemoryProgressServer([cached, serverOnly]));
        int notifications = 0;
        store.Changed += () => notifications++;

        await store.LoadAsync();
        await store.PendingSync;

        Assert.Equal([serverOnly.SessionId, cached.SessionId], Ids(store.History));
        Assert.Equal(Ids(store.History), Ids(cache.Stored));
        Assert.Equal(1, notifications);
        Assert.Equal(new ProgressStatus(ProgressSyncState.Synced, IsCacheAvailable: true), store.Status);
    }

    [Fact]
    public async Task LoadAsync_SessionsOnlyInTheCache_UploadsJustThose()
    {
        SightReadingSessionSummary shared = CreateSummary(1);
        SightReadingSessionSummary cacheOnly = CreateSummary(2);
        var server = new InMemoryProgressServer([shared]);
        var store = new ProgressStore(new InMemoryProgressCache(CreateHistory(shared, cacheOnly)), server);

        await store.LoadAsync();
        await store.PendingSync;

        Assert.Equal([cacheOnly.SessionId], Ids(Assert.Single(server.Uploads)));
        Assert.Equal([shared.SessionId, cacheOnly.SessionId], Ids(server.Sessions));
        Assert.Equal([cacheOnly.SessionId, shared.SessionId], Ids(store.History));
    }

    [Fact]
    public async Task LoadAsync_MoreSessionsAcrossBothCopiesThanTheCap_KeepsTheNewestHundredWithoutDuplicates()
    {
        SightReadingSessionSummary[] all = [.. Enumerable.Range(0, 140).Select(CreateSummary)];
        SightReadingSessionSummary[] inCache = all[..80];
        SightReadingSessionSummary[] onServer = all[60..];
        var cache = new InMemoryProgressCache(CreateHistory(inCache));
        var store = new ProgressStore(cache, new InMemoryProgressServer(onServer));

        await store.LoadAsync();
        await store.PendingSync;

        Guid[] expected = [.. all.Skip(40).Reverse().Select(session => session.SessionId)];
        Assert.Equal(expected, Ids(store.History));
        Assert.Equal(expected, Ids(cache.Stored));
    }

    [Fact]
    public async Task LoadAsync_NoServer_KeepsTheCachedHistoryAndReportsNoServer()
    {
        SightReadingSessionSummary cached = CreateSummary(1);
        var server = new InMemoryProgressServer { Reach = ProgressServerReach.NoServer };
        var store = new ProgressStore(new InMemoryProgressCache(CreateHistory(cached)), server);

        await store.LoadAsync();
        await store.PendingSync;

        Assert.Equal([cached.SessionId], Ids(store.History));
        Assert.Equal(new ProgressStatus(ProgressSyncState.NoServer, IsCacheAvailable: true), store.Status);
        Assert.Empty(server.Uploads);
    }

    [Fact]
    public async Task LoadAsync_ServerUnreachable_KeepsTheCachedHistoryAndReportsNotSynced()
    {
        SightReadingSessionSummary cached = CreateSummary(1);
        var server = new InMemoryProgressServer { Reach = ProgressServerReach.Unreachable };
        var store = new ProgressStore(new InMemoryProgressCache(CreateHistory(cached)), server);
        int notifications = 0;
        store.Changed += () => notifications++;

        await store.LoadAsync();
        await store.PendingSync;

        Assert.Equal([cached.SessionId], Ids(store.History));
        Assert.Equal(new ProgressStatus(ProgressSyncState.NotSynced, IsCacheAvailable: true), store.Status);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task RecordAsync_FinishedSession_WritesTheCacheFirstThenTheServer()
    {
        List<string> log = [];
        var cache = new InMemoryProgressCache(log: log);
        var server = new InMemoryProgressServer(log: log);
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;
        log.Clear();
        SightReadingSessionSummary session = CreateSummary(1);

        await store.RecordAsync(session);
        await store.PendingSync;

        Assert.Equal(["cache write", "server upload"], log);
        Assert.Equal([session.SessionId], Ids(store.History));
        Assert.Equal([session.SessionId], Ids(cache.Stored));
        Assert.Equal([session.SessionId], Ids(Assert.Single(server.Uploads)));
    }

    [Fact]
    public async Task RecordAsync_ServerUnreachable_KeepsTheSessionCachedAndReportsNotSynced()
    {
        var cache = new InMemoryProgressCache();
        var server = new InMemoryProgressServer();
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;
        server.Reach = ProgressServerReach.Unreachable;
        int notifications = 0;
        store.Changed += () => notifications++;
        SightReadingSessionSummary session = CreateSummary(1);

        await store.RecordAsync(session);
        await store.PendingSync;

        Assert.Equal([session.SessionId], Ids(store.History));
        Assert.Equal([session.SessionId], Ids(cache.Stored));
        Assert.Equal(new ProgressStatus(ProgressSyncState.NotSynced, IsCacheAvailable: true), store.Status);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task RecordAsync_ServerBackAfterAFailedWrite_UploadsTheSessionsItMissed()
    {
        var server = new InMemoryProgressServer();
        var store = new ProgressStore(new InMemoryProgressCache(), server);
        await store.LoadAsync();
        await store.PendingSync;
        server.Reach = ProgressServerReach.Unreachable;
        SightReadingSessionSummary missed = CreateSummary(1);
        await store.RecordAsync(missed);
        await store.PendingSync;
        server.Reach = ProgressServerReach.Reached;
        SightReadingSessionSummary next = CreateSummary(2);

        await store.RecordAsync(next);
        await store.PendingSync;

        Assert.Equal(new[] { missed.SessionId, next.SessionId }.Order(), Ids(server.Sessions).Order());
        Assert.Equal(ProgressSyncState.Synced, store.Status.Sync);
    }

    [Fact]
    public async Task RecordAsync_NoServer_OnlyWritesTheCache()
    {
        var cache = new InMemoryProgressCache();
        var server = new InMemoryProgressServer { Reach = ProgressServerReach.NoServer };
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;
        SightReadingSessionSummary session = CreateSummary(1);

        await store.RecordAsync(session);
        await store.PendingSync;

        Assert.Equal([session.SessionId], Ids(cache.Stored));
        Assert.Equal(ProgressSyncState.NoServer, store.Status.Sync);
        Assert.Empty(server.Uploads);
        Assert.Equal(1, server.ListCount);
    }

    [Fact]
    public async Task LoadAsync_CacheUnavailableServerReachable_KeepsTheHistoryInMemory()
    {
        SightReadingSessionSummary older = CreateSummary(1);
        SightReadingSessionSummary newer = CreateSummary(2);
        var cache = new InMemoryProgressCache { IsAvailable = false };
        var server = new InMemoryProgressServer([older, newer]);
        var store = new ProgressStore(cache, server);

        await store.LoadAsync();
        await store.PendingSync;
        SightReadingSessionSummary session = CreateSummary(3);
        await store.RecordAsync(session);
        await store.PendingSync;

        Assert.Equal([session.SessionId, newer.SessionId, older.SessionId], Ids(store.History));
        Assert.Contains(session.SessionId, Ids(server.Sessions));
        Assert.Equal(new ProgressStatus(ProgressSyncState.Synced, IsCacheAvailable: false), store.Status);
    }

    [Fact]
    public async Task RecordAsync_CacheUnavailableAndServerUnreachable_StillKeepsTheSessionInMemory()
    {
        var cache = new InMemoryProgressCache { IsAvailable = false };
        var server = new InMemoryProgressServer { Reach = ProgressServerReach.Unreachable };
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;
        SightReadingSessionSummary session = CreateSummary(1);

        await store.RecordAsync(session);
        await store.PendingSync;

        Assert.Equal([session.SessionId], Ids(store.History));
        Assert.Equal(new ProgressStatus(ProgressSyncState.NotSynced, IsCacheAvailable: false), store.Status);
    }

    [Fact]
    public async Task ClearAsync_BothCopiesHoldHistory_RemovesThem()
    {
        SightReadingSessionSummary session = CreateSummary(1);
        var cache = new InMemoryProgressCache(CreateHistory(session));
        var server = new InMemoryProgressServer([session]);
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;

        await store.ClearAsync();

        Assert.Empty(store.History.Entries);
        Assert.Empty(cache.Stored.Entries);
        Assert.Empty(server.Sessions);
        Assert.Equal(ProgressSyncState.Synced, store.Status.Sync);
    }

    [Fact]
    public async Task ClearAsync_ServerCannotDelete_ClearsTheBrowserAndReportsTheFailedDelete()
    {
        SightReadingSessionSummary session = CreateSummary(1);
        var cache = new InMemoryProgressCache(CreateHistory(session));
        var server = new InMemoryProgressServer([session]);
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;
        server.Reach = ProgressServerReach.Unreachable;

        await store.ClearAsync();

        Assert.Empty(store.History.Entries);
        Assert.Empty(cache.Stored.Entries);
        Assert.Equal(ProgressSyncState.DeleteFailed, store.Status.Sync);
    }

    [Fact]
    public async Task ClearAsync_NoServer_ClearsTheBrowserWithoutCallingTheServer()
    {
        var cache = new InMemoryProgressCache(CreateHistory(CreateSummary(1)));
        var server = new InMemoryProgressServer { Reach = ProgressServerReach.NoServer };
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();
        await store.PendingSync;

        await store.ClearAsync();

        Assert.Empty(cache.Stored.Entries);
        Assert.Equal(0, server.ClearCount);
        Assert.Equal(ProgressSyncState.NoServer, store.Status.Sync);
    }

    [Fact]
    public async Task ClearAsync_WhileTheFirstSyncIsStillRunning_DoesNotBringTheSessionsBack()
    {
        SightReadingSessionSummary session = CreateSummary(1);
        var gate = new TaskCompletionSource();
        var cache = new InMemoryProgressCache(CreateHistory(session));
        var server = new InMemoryProgressServer([session]) { ListGate = gate.Task };
        var store = new ProgressStore(cache, server);
        await store.LoadAsync();

        Task clearing = store.ClearAsync().AsTask();
        gate.SetResult();
        await clearing;
        await store.PendingSync;

        Assert.Empty(store.History.Entries);
        Assert.Empty(cache.Stored.Entries);
        Assert.Empty(server.Sessions);
    }

    [Fact]
    public async Task RecordAsync_WhileTheFirstSyncIsStillRunning_UploadsTheSessionOnce()
    {
        var gate = new TaskCompletionSource();
        var server = new InMemoryProgressServer { ListGate = gate.Task };
        var store = new ProgressStore(new InMemoryProgressCache(), server);
        await store.LoadAsync();
        SightReadingSessionSummary session = CreateSummary(1);

        await store.RecordAsync(session);
        gate.SetResult();
        await store.PendingSync;

        Assert.Equal([session.SessionId], Ids(store.History));
        Assert.Equal([session.SessionId], Ids(server.Sessions));
    }

    [Fact]
    public async Task LoadAsync_PreviousSyncWasCancelled_StillSyncs()
    {
        SightReadingSessionSummary onServer = CreateSummary(1);
        var server = new InMemoryProgressServer([onServer]) { ListGate = new TaskCompletionSource().Task };
        var store = new ProgressStore(new InMemoryProgressCache(), server);
        using var cancellation = new CancellationTokenSource();
        await store.LoadAsync(cancellation.Token);
        await cancellation.CancelAsync();
        server.ListGate = null;

        await store.LoadAsync();
        await store.PendingSync;

        Assert.Equal([onServer.SessionId], Ids(store.History));
        Assert.Equal(ProgressSyncState.Synced, store.Status.Sync);
    }

    private static SightReadingSessionSummary CreateSummary(int minutesAfterStart) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Guid.NewGuid(),
            Start.AddMinutes(minutesAfterStart),
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(20),
            6,
            2,
            []);

    private static SightReadingHistory CreateHistory(params SightReadingSessionSummary[] summaries) =>
        summaries.Aggregate(SightReadingHistory.Empty, (history, summary) => history.WithCompletedSession(summary));

    private static Guid[] Ids(SightReadingHistory history) => Ids(history.Entries);

    private static Guid[] Ids(IEnumerable<SightReadingSessionSummary> summaries) => [.. summaries.Select(entry => entry.SessionId)];
}
