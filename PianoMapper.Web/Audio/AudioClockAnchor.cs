namespace PianoMapper.Web.Audio;

internal sealed record AudioClockAnchor(
    double PerformanceTimeMilliseconds,
    double AudioTimeSeconds);
