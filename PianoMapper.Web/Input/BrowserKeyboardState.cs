namespace PianoMapper.Web.Input;

internal sealed class BrowserKeyboardState
{
    internal BrowserKeyboardState(int initialOctave)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialOctave, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(initialOctave, 8);

        CurrentOctave = initialOctave;
    }

    internal int CurrentOctave { get; private set; }

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

        return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
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
        return new BrowserInputCommand(
            BrowserInputCommandKind.Clear,
            IsHandled: true,
            EventTime: eventTime);
    }
}
