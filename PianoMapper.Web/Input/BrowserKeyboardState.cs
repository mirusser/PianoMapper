using PianoMapper.Input;

namespace PianoMapper.Web.Input;

internal sealed class BrowserKeyboardState
{
    private readonly NoteTimeline timeline;
    private readonly Dictionary<string, PerformedNote> activeNotes = new(StringComparer.Ordinal);

    internal BrowserKeyboardState(NoteTimeline timeline, int initialOctave)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialOctave, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(initialOctave, 8);

        this.timeline = timeline;
        CurrentOctave = initialOctave;
    }

    internal int CurrentOctave { get; private set; }

    internal int ActiveNoteCount => activeNotes.Count;

    internal BrowserInputCommand HandleKeyDown(string code, bool isRepeat, TimeSpan eventTime)
    {
        if (!BrowserKeyBindings.TryGetAction(code, out var action))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: false);
        }

        if (isRepeat)
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        if (action.Kind == BrowserKeyActionKind.OctaveDown)
        {
            return ChangeOctave(CurrentOctave - 1, eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.OctaveUp)
        {
            return ChangeOctave(CurrentOctave + 1, eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.SelectOctave)
        {
            return ChangeOctave(action.Value, eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.Clear)
        {
            return Clear(eventTime);
        }

        if (action.Kind is BrowserKeyActionKind.PreviousMeasures or BrowserKeyActionKind.NextMeasures)
        {
            return new BrowserInputCommand(
                action.Kind == BrowserKeyActionKind.PreviousMeasures
                    ? BrowserInputCommandKind.PreviousMeasures
                    : BrowserInputCommandKind.NextMeasures,
                IsHandled: true,
                EventTime: eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.PlayScore)
        {
            return new BrowserInputCommand(
                BrowserInputCommandKind.StartScorePlayback,
                IsHandled: true,
                EventTime: eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.StartPractice)
        {
            return new BrowserInputCommand(
                BrowserInputCommandKind.StartPractice,
                IsHandled: true,
                EventTime: eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.ToggleView)
        {
            return new BrowserInputCommand(
                BrowserInputCommandKind.ToggleView,
                IsHandled: true,
                EventTime: eventTime);
        }

        if (action.Kind == BrowserKeyActionKind.PlayRandomMeasure)
        {
            return new BrowserInputCommand(
                BrowserInputCommandKind.PlayRandomMeasure,
                IsHandled: true,
                EventTime: eventTime);
        }

        if (action.Kind != BrowserKeyActionKind.Note || activeNotes.ContainsKey(code))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        var pitch = PianoKeyboardLayout.GetPitch(CurrentOctave, action.Value);
        var note = timeline.Start(pitch, eventTime);
        activeNotes.Add(code, note);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOn,
            IsHandled: true,
            code,
            pitch,
            note,
            eventTime);
    }

    internal BrowserInputCommand HandleKeyUp(string code, TimeSpan eventTime)
    {
        if (!BrowserKeyBindings.TryGetAction(code, out var action))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: false);
        }

        if (action.Kind != BrowserKeyActionKind.Note || !activeNotes.Remove(code, out var note))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        timeline.Complete(note, eventTime);
        return new BrowserInputCommand(
            BrowserInputCommandKind.NoteOff,
            IsHandled: true,
            code,
            note.Pitch,
            note,
            eventTime);
    }

    internal BrowserInputCommand ChangeOctave(int octave, TimeSpan eventTime)
    {
        CurrentOctave = Math.Clamp(octave, 1, 8);
        return new BrowserInputCommand(
            BrowserInputCommandKind.OctaveChanged,
            IsHandled: true,
            EventTime: eventTime);
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

        foreach (var note in activeNotes.Values)
        {
            timeline.Complete(note, eventTime);
        }

        activeNotes.Clear();
        return new BrowserInputCommand(
            BrowserInputCommandKind.ReleaseHeldNotes,
            IsHandled: true,
            EventTime: eventTime);
    }
}
