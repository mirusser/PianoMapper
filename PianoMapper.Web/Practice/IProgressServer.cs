using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The durable copy of the learner's sessions. An adapter never throws for a server problem: it says how far the call
/// got (<see cref="ProgressServerReach"/>), so the store can tell "no server" from "server down".
/// </summary>
internal interface IProgressServer
{
    /// <summary>The newest sessions the server kept, at most <see cref="ProgressApiRoutes.MaximumLimit"/>.</summary>
    Task<ProgressServerSessions> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the sessions; one the server already has is left alone.</summary>
    Task<ProgressServerReach> UploadAsync(
        IReadOnlyList<SightReadingSessionSummary> sessions,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes every session the server kept.</summary>
    Task<ProgressServerReach> ClearAsync(CancellationToken cancellationToken = default);
}
