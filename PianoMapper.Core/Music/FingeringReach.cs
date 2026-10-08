namespace PianoMapper.Music;

/// <summary>
/// A hand span expressed as white-key centre spacings. The comfortable span is a cost boundary;
/// the maximum span is a physical feasibility boundary.
/// </summary>
public sealed class FingeringReach
{
    public FingeringReach(double comfortableWhiteKeySpan, double maximumWhiteKeySpan)
    {
        if (!double.IsFinite(comfortableWhiteKeySpan) || comfortableWhiteKeySpan < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(comfortableWhiteKeySpan));
        }

        if (!double.IsFinite(maximumWhiteKeySpan) || maximumWhiteKeySpan < comfortableWhiteKeySpan)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWhiteKeySpan));
        }

        ComfortableWhiteKeySpan = comfortableWhiteKeySpan;
        MaximumWhiteKeySpan = maximumWhiteKeySpan;
    }

    public double ComfortableWhiteKeySpan { get; }

    public double MaximumWhiteKeySpan { get; }
}
