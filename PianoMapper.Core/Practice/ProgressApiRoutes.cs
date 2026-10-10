namespace PianoMapper.Practice;

/// <summary>
/// The server routes that keep the learner's finished sessions. There is no learner in the route: the server resolves
/// whose sessions they are, one shared learner for now.
/// </summary>
public static class ProgressApiRoutes
{
    public const string Sessions = "/api/progress/sessions";

    /// <summary>The most sessions one list returns, which is also the most the client keeps in memory.</summary>
    public const int MaximumLimit = 100;

    public static string List(int limit) => $"{Sessions}?limit={limit}";
}
