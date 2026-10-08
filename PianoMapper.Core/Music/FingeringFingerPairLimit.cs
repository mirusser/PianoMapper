namespace PianoMapper.Music;

/// <summary>Overrides the generic reach for one pair of distinct fingers.</summary>
public sealed class FingeringFingerPairLimit
{
    public FingeringFingerPairLimit(int firstFinger, int secondFinger, FingeringReach reach)
    {
        if (firstFinger is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(firstFinger));
        }

        if (secondFinger is < 1 or > 5 || secondFinger == firstFinger)
        {
            throw new ArgumentOutOfRangeException(nameof(secondFinger));
        }

        ArgumentNullException.ThrowIfNull(reach);
        FirstFinger = Math.Min(firstFinger, secondFinger);
        SecondFinger = Math.Max(firstFinger, secondFinger);
        Reach = reach;
    }

    public int FirstFinger { get; }

    public int SecondFinger { get; }

    public FingeringReach Reach { get; }
}
