namespace PianoMapper.Music;

/// <summary>Explicit constraints and result count for a fingering-generation request.</summary>
public sealed class ScoreFingeringGenerationOptions
{
    private readonly IReadOnlyList<ScoreFingeringLock> locks;

    public ScoreFingeringGenerationOptions(
        ScoreFingeringProfile? profile = null,
        IEnumerable<ScoreFingeringLock>? locks = null,
        int maximumAlternatives = 1)
    {
        if (maximumAlternatives is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAlternatives));
        }

        ScoreFingeringLock[] copiedLocks = (locks ?? []).ToArray();
        if (copiedLocks.Any(@lock => @lock is null))
        {
            throw new ArgumentException("Fingering locks cannot contain null values.", nameof(locks));
        }

        if (copiedLocks.GroupBy(@lock => @lock.Address).Any(group => group.Select(@lock => @lock.FingerNumber).Distinct().Count() > 1))
        {
            throw new ArgumentException("A note cannot be locked to more than one finger.", nameof(locks));
        }

        Profile = profile ?? ScoreFingeringProfile.Default;
        this.locks = copiedLocks.GroupBy(@lock => @lock.Address).Select(group => group.First()).ToArray();
        MaximumAlternatives = maximumAlternatives;
    }

    public ScoreFingeringProfile Profile { get; }

    public IReadOnlyList<ScoreFingeringLock> Locks => locks;

    public int MaximumAlternatives { get; }
}
