namespace PianoMapper.Web.Practice;

/// <summary>One session on the trend line: how often the right key was found first, and how often timing was clean.</summary>
/// <param name="TimingCleanPercent">Null for sessions that do not grade timing.</param>
public sealed record TrendPoint(DateTimeOffset CompletedAt, double PitchAccuracyPercent, double? TimingCleanPercent);
