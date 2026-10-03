namespace PianoMapper.Practice;

public sealed record GradingOptions
{
    public TimeSpan OnsetTolerance { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan OnTimeTolerance { get; init; } = TimeSpan.FromMilliseconds(60);

    public double MinimumDurationRatio { get; init; } = 0.5;

    public double MaximumDurationRatio { get; init; } = 1.5;

    /// <summary>
    /// Any key matches an expected note within the onset window (rhythm-only practice): a note is graded on when
    /// and for how long it was played, never on which key, so <see cref="Verdict.WrongPitch"/> is never produced.
    /// </summary>
    public bool IgnorePitch { get; init; }
}
