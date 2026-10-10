using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

/// <summary>A browser cache held in memory; <see cref="IsAvailable"/> switches it to a disabled-storage browser.</summary>
internal sealed class InMemoryProgressCache(SightReadingHistory? stored = null, List<string>? log = null) : IProgressCache
{
    internal SightReadingHistory Stored { get; private set; } = stored ?? SightReadingHistory.Empty;

    internal bool IsAvailable { get; set; } = true;

    public ValueTask<ProgressCacheRead> ReadAsync(CancellationToken cancellationToken = default)
    {
        log?.Add("cache read");
        return ValueTask.FromResult(new ProgressCacheRead(IsAvailable ? Stored : SightReadingHistory.Empty, IsAvailable));
    }

    public ValueTask<bool> WriteAsync(SightReadingHistory history, CancellationToken cancellationToken = default)
    {
        log?.Add("cache write");
        if (IsAvailable)
        {
            Stored = history;
        }

        return ValueTask.FromResult(IsAvailable);
    }

    public ValueTask<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        log?.Add("cache clear");
        if (IsAvailable)
        {
            Stored = SightReadingHistory.Empty;
        }

        return ValueTask.FromResult(IsAvailable);
    }
}
