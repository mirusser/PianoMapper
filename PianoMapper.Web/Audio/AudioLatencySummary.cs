namespace PianoMapper.Web.Audio;

internal sealed record AudioLatencySummary(
    int SampleCount,
    double MedianMilliseconds,
    double P95Milliseconds);
