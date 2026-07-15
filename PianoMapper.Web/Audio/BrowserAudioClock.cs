namespace PianoMapper.Web.Audio;

internal static class BrowserAudioClock
{
    internal static TimeSpan MapEventTimestamp(AudioClockAnchor anchor, double eventTimestampMilliseconds)
    {
        double elapsedSeconds = (eventTimestampMilliseconds - anchor.PerformanceTimeMilliseconds) / 1000;
        return TimeSpan.FromSeconds(anchor.AudioTimeSeconds + elapsedSeconds);
    }
}
