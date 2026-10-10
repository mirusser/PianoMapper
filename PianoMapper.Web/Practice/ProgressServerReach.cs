namespace PianoMapper.Web.Practice;

/// <summary>How far a call to the server got.</summary>
internal enum ProgressServerReach
{
    /// <summary>The server answered and did what was asked.</summary>
    Reached,

    /// <summary>There is no server at all, as in the standalone static app, which answers 404 to every progress route.</summary>
    NoServer,

    /// <summary>A server is meant to be there but could not do it: unreachable, unavailable (503) or failing.</summary>
    Unreachable,
}
