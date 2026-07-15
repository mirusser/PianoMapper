namespace PianoMapper.Practice;

public sealed record GradingResult(
    IReadOnlyList<GradedEvent> Events,
    GradingSummary Summary);
