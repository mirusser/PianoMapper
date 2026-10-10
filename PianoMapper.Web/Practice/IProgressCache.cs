using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The browser's copy of the learner's history, the one <see cref="ProgressStore"/> reads first and writes first. An
/// adapter never throws for a storage failure (disabled, full or private-mode storage): it reports that the cache is
/// unavailable instead, so practising keeps working either way.
/// </summary>
internal interface IProgressCache
{
    ValueTask<ProgressCacheRead> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the whole history. Returns false when the cache cannot be used.</summary>
    ValueTask<bool> WriteAsync(SightReadingHistory history, CancellationToken cancellationToken = default);

    /// <summary>Removes the stored history. Returns false when the cache cannot be used.</summary>
    ValueTask<bool> ClearAsync(CancellationToken cancellationToken = default);
}
