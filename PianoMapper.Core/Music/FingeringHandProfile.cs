namespace PianoMapper.Music;

/// <summary>Generic and pair-specific reach limits for one hand.</summary>
public sealed class FingeringHandProfile
{
    private readonly IReadOnlyList<FingeringFingerPairLimit> pairLimits;

    public FingeringHandProfile(
        FingeringReach defaultReach,
        IEnumerable<FingeringFingerPairLimit>? pairLimits = null)
    {
        ArgumentNullException.ThrowIfNull(defaultReach);
        DefaultReach = defaultReach;

        FingeringFingerPairLimit[] limits = (pairLimits ?? []).ToArray();
        if (limits.Any(limit => limit is null))
        {
            throw new ArgumentException("Finger-pair limits cannot contain null values.", nameof(pairLimits));
        }

        if (limits.GroupBy(limit => (limit.FirstFinger, limit.SecondFinger)).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A finger pair can have only one reach limit.", nameof(pairLimits));
        }

        this.pairLimits = limits;
    }

    public FingeringReach DefaultReach { get; }

    public IReadOnlyList<FingeringFingerPairLimit> PairLimits => pairLimits;

    public FingeringReach GetReach(int firstFinger, int secondFinger)
    {
        int lowerFinger = Math.Min(firstFinger, secondFinger);
        int upperFinger = Math.Max(firstFinger, secondFinger);
        return pairLimits.FirstOrDefault(limit =>
            limit.FirstFinger == lowerFinger && limit.SecondFinger == upperFinger)?.Reach ?? DefaultReach;
    }
}
