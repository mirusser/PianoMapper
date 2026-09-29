namespace PianoMapper.Web.Input;

public sealed class BrowserPointerNoteOffEvent
{
    public string PointerId { get; init; } = string.Empty;

    public double EventTimestampMilliseconds { get; init; }
}
