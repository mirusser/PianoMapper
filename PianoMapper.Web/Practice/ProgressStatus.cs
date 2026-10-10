namespace PianoMapper.Web.Practice;

/// <summary>What the store can say about where the history is kept, for the Progress panel to show.</summary>
internal sealed record ProgressStatus(ProgressSyncState Sync, bool IsCacheAvailable);
