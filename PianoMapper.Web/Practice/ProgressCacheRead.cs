using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>What the cache held, and whether it could be read at all (an unavailable cache reads as empty).</summary>
internal sealed record ProgressCacheRead(SightReadingHistory History, bool IsAvailable);
