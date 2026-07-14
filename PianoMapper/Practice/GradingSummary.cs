namespace PianoMapper.Practice;

internal sealed record GradingSummary(
    double AccuracyPercent,
    IReadOnlyDictionary<Verdict, int> Counts);
