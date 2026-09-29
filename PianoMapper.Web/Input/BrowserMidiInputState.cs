namespace PianoMapper.Web.Input;

/// <summary>
/// Parses MIDI-specific events (note IDs, MIDI numbers 0-127, the "note-on with velocity 0 means note-off"
/// convention) and drives the source-neutral <see cref="BrowserNoteInputState"/> with them. Existing MIDI behavior
/// (88-key range, velocity handling, timeline updates, browser input commands) is unchanged by this adapter split —
/// see <see cref="BrowserNoteInputState"/> for the actual note tracking.
/// </summary>
internal sealed class BrowserMidiInputState(BrowserNoteInputState noteInputState)
{
    private readonly BrowserNoteInputState noteInputState =
        noteInputState ?? throw new ArgumentNullException(nameof(noteInputState));

    internal int ActiveNoteCount => noteInputState.ActiveNoteCount;

    internal BrowserInputCommand Handle(BrowserMidiEvent midiEvent, TimeSpan eventTime)
    {
        ArgumentNullException.ThrowIfNull(midiEvent);
        ArgumentException.ThrowIfNullOrEmpty(midiEvent.NoteId);
        ArgumentOutOfRangeException.ThrowIfNegative(midiEvent.MidiNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(midiEvent.MidiNumber, 127);
        ArgumentOutOfRangeException.ThrowIfNegative(midiEvent.Velocity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(midiEvent.Velocity, 127);

        if (!midiEvent.IsNoteOn)
        {
            return noteInputState.ReleaseNote(midiEvent.NoteId, eventTime);
        }

        // MIDI convention: a note-on with velocity 0 means note-off. This is a MIDI-specific quirk, so it's handled
        // here in the adapter rather than in the source-neutral state.
        if (midiEvent.Velocity == 0)
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        return noteInputState.StartNote(
            midiEvent.NoteId,
            MidiPitchMapping.CreatePitch(midiEvent.MidiNumber),
            eventTime,
            midiEvent.Velocity);
    }

    internal BrowserInputCommand Clear(TimeSpan eventTime) => noteInputState.Clear(eventTime);

    internal BrowserInputCommand ReleaseAll(TimeSpan eventTime) => noteInputState.ReleaseAll(eventTime);
}
