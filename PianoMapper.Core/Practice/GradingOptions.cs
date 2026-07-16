namespace PianoMapper.Practice;

public sealed record GradingOptions
{
    public TimeSpan OnsetTolerance { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan OnTimeTolerance { get; init; } = TimeSpan.FromMilliseconds(60);

    public double MinimumDurationRatio { get; init; } = 0.5;

    public double MaximumDurationRatio { get; init; } = 1.5;
}
