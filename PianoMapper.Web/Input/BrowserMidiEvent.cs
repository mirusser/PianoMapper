namespace PianoMapper.Web.Input;

public sealed class BrowserMidiEvent
{
    public string NoteId { get; init; } = string.Empty;

    public int MidiNumber { get; init; }

    public int Velocity { get; init; }

    public bool IsNoteOn { get; init; }

    public double EventTimestampMilliseconds { get; init; }
}
