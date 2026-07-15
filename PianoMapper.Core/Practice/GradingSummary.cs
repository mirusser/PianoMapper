namespace PianoMapper.Practice;

public sealed record GradingSummary(
    double AccuracyPercent,
    IReadOnlyDictionary<Verdict, int> Counts);
