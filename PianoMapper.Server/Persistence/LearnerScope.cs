namespace PianoMapper.Server.Persistence;

/// <summary>
/// Whose progress a request is about. There are no users yet, so everyone shares one well-known learner; this is the
/// only place that decides it. Adding users later means resolving the learner from the request here (from
/// authentication) and migrating the default learner's rows; no table or route changes.
/// </summary>
internal static class LearnerScope
{
    internal static readonly Guid DefaultLearnerId = new("6c0d6e0a-3b1f-4f0e-9a52-2d8f5c1a7b01");

    internal static Guid Resolve() => DefaultLearnerId;
}
