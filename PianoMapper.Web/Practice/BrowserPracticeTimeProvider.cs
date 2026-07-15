namespace PianoMapper.Web.Practice;

internal sealed class BrowserPracticeTimeProvider : TimeProvider
{
    private long timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => timestamp;

    internal void SetTime(TimeSpan time)
    {
        timestamp = time.Ticks;
    }
}
