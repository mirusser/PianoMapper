namespace PianoMapper.Web.Practice;

/// <summary>Where the server copy of the history stands, as far as the store knows.</summary>
internal enum ProgressSyncState
{
    /// <summary>The server has not been asked yet.</summary>
    Connecting,

    /// <summary>The last server call worked: the server holds what this browser holds.</summary>
    Synced,

    /// <summary>There is no server (standalone app); history lives in the browser cache only.</summary>
    NoServer,

    /// <summary>The server could not be reached; sessions are kept in the browser and upload on the next sync.</summary>
    NotSynced,

    /// <summary>Clearing could not delete the server copy, so the cleared history may come back.</summary>
    DeleteFailed,
}
