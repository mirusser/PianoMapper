namespace PianoMapper.Web.Input;

public sealed class BrowserKeyboardEvent
{
    public string Code { get; init; } = string.Empty;

    public bool IsRepeat { get; init; }

    public double EventTimestampMilliseconds { get; init; }
}
