namespace PianoMapper.Web.Input;

public sealed class BrowserPointerNoteOnEvent
{
    public string PointerId { get; init; } = string.Empty;

    public string Pitch { get; init; } = string.Empty;

    public double EventTimestampMilliseconds { get; init; }
}
