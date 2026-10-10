using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

/// <summary>
/// A server held in memory. Like the real one it keeps every session it is given and lists the newest ones;
/// <see cref="Reach"/> makes every call behave as if there were no server or it could not be reached.
/// </summary>
internal sealed class InMemoryProgressServer(IEnumerable<SightReadingSessionSummary>? stored = null, List<string>? log = null)
    : IProgressServer
{
    internal List<SightReadingSessionSummary> Sessions { get; } = [.. stored ?? []];

    internal ProgressServerReach Reach { get; set; } = ProgressServerReach.Reached;

    /// <summary>When set, a list call does not answer until this task completes.</summary>
    internal Task? ListGate { get; set; }

    internal List<IReadOnlyList<SightReadingSessionSummary>> Uploads { get; } = [];

    internal int ListCount { get; private set; }

    internal int ClearCount { get; private set; }

    public async Task<ProgressServerSessions> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCount++;
        log?.Add("server list");

        // The answer is fixed when the call is made, as with a real server; the gate only delays it on the way back.
        ProgressServerSessions answer = Reach == ProgressServerReach.Reached
            ? new ProgressServerSessions(
                Reach,
                [.. Sessions.OrderByDescending(session => session.CompletedAt).Take(ProgressApiRoutes.MaximumLimit)])
            : new ProgressServerSessions(Reach, []);
        if (ListGate is { } gate)
        {
            await gate.WaitAsync(cancellationToken);
        }

        return answer;
    }

    public Task<ProgressServerReach> UploadAsync(
        IReadOnlyList<SightReadingSessionSummary> sessions,
        CancellationToken cancellationToken = default)
    {
        Uploads.Add(sessions);
        log?.Add("server upload");
        if (Reach == ProgressServerReach.Reached)
        {
            Sessions.AddRange(sessions.Where(session => Sessions.All(kept => kept.SessionId != session.SessionId)).ToArray());
        }

        return Task.FromResult(Reach);
    }

    public Task<ProgressServerReach> ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCount++;
        log?.Add("server clear");
        if (Reach == ProgressServerReach.Reached)
        {
            Sessions.Clear();
        }

        return Task.FromResult(Reach);
    }
}
