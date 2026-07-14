namespace PianoMapper.Practice;

internal sealed record GradingResult(
    IReadOnlyList<GradedEvent> Events,
    GradingSummary Summary);
