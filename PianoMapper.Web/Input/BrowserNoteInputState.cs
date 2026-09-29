using PianoMapper.Music;

namespace PianoMapper.Web.Input;

/// <summary>
/// Tracks active notes by a stable, source-specific ID (e.g. <c>"midi:roland:0:60"</c>, <c>"pointer:3"</c>,
/// <c>"key:KeyA"</c>) so MIDI, pointer/touch, and computer-key input can all drive the same timeline and command
/// pipeline without any of them pretending to be a MIDI device. Two different sources holding the same pitch are
/// tracked independently — each with its own note-on/off lifecycle — so releasing one never affects the other.
/// <see cref="BrowserMidiInputState"/> is a thin adapter that parses MIDI-specific events into calls on this type.
/// </summary>
internal sealed class BrowserNoteInputState(NoteTimeline timeline)
{
    private readonly Dictionary<string, PerformedNote> activeNotes = new(StringComparer.Ordinal);

    internal int ActiveNoteCount => activeNotes.Count;

    /// <summary>
    /// Starts a note for <paramref name="sourceNoteId"/>. A duplicate start for an ID that's already active is a
    /// no-op (<see cref="BrowserInputCommandKind.None"/>), not a retrigger — this is the same semantics
    /// <see cref="BrowserMidiInputState"/> already had for a repeated MIDI note-on.
    /// </summary>
    internal BrowserInputCommand StartNote(string sourceNoteId, Pitch pitch, TimeSpan eventTime, int velocity)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceNoteId);
        ArgumentOutOfRangeException.ThrowIfNegative(velocity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(velocity, 127);

        if (activeNotes.ContainsKey(sourceNoteId))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        PerformedNote note = timeline.Start(pitch, eventTime);
        activeNotes.Add(sourceNoteId, note);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOn,
            IsHandled: true,
            sourceNoteId,
            pitch,
            note,
            eventTime,
            velocity);
    }

    /// <summary>Releases the note for <paramref name="sourceNoteId"/>. An unknown ID is a no-op, not an error.</summary>
    internal BrowserInputCommand ReleaseNote(string sourceNoteId, TimeSpan eventTime)
    {
        if (!activeNotes.Remove(sourceNoteId, out PerformedNote? note))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        timeline.Complete(note, eventTime);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOff,
            IsHandled: true,
            sourceNoteId,
            note.Pitch,
            note,
            eventTime);
    }

    /// <summary>Drops every active note from the timeline without completing it (e.g. a hard "clear notes").</summary>
    internal BrowserInputCommand Clear(TimeSpan eventTime)
    {
        timeline.Remove(activeNotes.Values.ToArray());
        activeNotes.Clear();
        return new BrowserInputCommand(BrowserInputCommandKind.Clear, IsHandled: true, EventTime: eventTime);
    }

    /// <summary>Completes every active note at <paramref name="eventTime"/> (e.g. a source disconnecting).</summary>
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
        return new BrowserInputCommand(BrowserInputCommandKind.ReleaseHeldNotes, IsHandled: true, EventTime: eventTime);
    }
}
