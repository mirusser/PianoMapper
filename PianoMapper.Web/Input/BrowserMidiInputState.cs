using PianoMapper.Music;

namespace PianoMapper.Web.Input;

internal sealed class BrowserMidiInputState(NoteTimeline timeline)
{
    private readonly Dictionary<string, PerformedNote> activeNotes = new(StringComparer.Ordinal);

    internal int ActiveNoteCount => activeNotes.Count;

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
            return Release(midiEvent.NoteId, eventTime);
        }

        if (midiEvent.Velocity == 0 || activeNotes.ContainsKey(midiEvent.NoteId))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        Pitch pitch = CreatePitch(midiEvent.MidiNumber);
        var note = timeline.Start(pitch, eventTime);
        activeNotes.Add(midiEvent.NoteId, note);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOn,
            IsHandled: true,
            midiEvent.NoteId,
            pitch,
            note,
            eventTime,
            midiEvent.Velocity);
    }

    internal BrowserInputCommand Clear(TimeSpan eventTime)
    {
        timeline.Remove(activeNotes.Values.ToArray());
        activeNotes.Clear();
        return new BrowserInputCommand(
            BrowserInputCommandKind.Clear,
            IsHandled: true,
            EventTime: eventTime);
    }

    internal BrowserInputCommand ReleaseAll(TimeSpan eventTime)
    {
        if (activeNotes.Count == 0)
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        foreach (PerformedNote note in activeNotes.Values)
        {
            timeline.Complete(note, eventTime);
        }

        activeNotes.Clear();
        return new BrowserInputCommand(
            BrowserInputCommandKind.ReleaseHeldNotes,
            IsHandled: true,
            EventTime: eventTime);
    }

    private BrowserInputCommand Release(string noteId, TimeSpan eventTime)
    {
        if (!activeNotes.Remove(noteId, out var note))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        timeline.Complete(note, eventTime);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOff,
            IsHandled: true,
            noteId,
            note.Pitch,
            note,
            eventTime);
    }

    private static Pitch CreatePitch(int midiNumber)
    {
        int octave = (midiNumber / 12) - 1;
        return (midiNumber % 12) switch
        {
            0 => new Pitch(NoteLetter.C, 0, octave),
            1 => new Pitch(NoteLetter.C, 1, octave),
            2 => new Pitch(NoteLetter.D, 0, octave),
            3 => new Pitch(NoteLetter.D, 1, octave),
            4 => new Pitch(NoteLetter.E, 0, octave),
            5 => new Pitch(NoteLetter.F, 0, octave),
            6 => new Pitch(NoteLetter.F, 1, octave),
            7 => new Pitch(NoteLetter.G, 0, octave),
            8 => new Pitch(NoteLetter.G, 1, octave),
            9 => new Pitch(NoteLetter.A, 0, octave),
            10 => new Pitch(NoteLetter.A, 1, octave),
            11 => new Pitch(NoteLetter.B, 0, octave),
            _ => throw new InvalidOperationException(),
        };
    }
}
