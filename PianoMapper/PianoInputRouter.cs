using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Practice;

namespace PianoMapper;

internal static class PianoInputRouter
{
    private static readonly Keys[] KeysInPriorityOrder =
    [
        Keys.Escape,
        Keys.Q,
        Keys.Space,
        Keys.Enter,
        Keys.Tab,
        Keys.PageUp,
        Keys.PageDown,
        Keys.P,
        Keys.Up,
        Keys.Down,
        Keys.D1,
        Keys.D2,
        Keys.D3,
        Keys.D4,
        Keys.D5,
        Keys.D6,
        Keys.D7,
        Keys.D8,
        Keys.M,
    ];

    internal static IReadOnlyList<Keys> ControlKeys => KeysInPriorityOrder;

    internal static PianoInputCommand Resolve(
        IReadOnlyCollection<Keys> pressedKeys,
        bool hasLoadedScore,
        PracticeSessionState? practiceState,
        int octave)
    {
        ArgumentNullException.ThrowIfNull(pressedKeys);
        bool isPracticeVisible = practiceState is not null and not PracticeSessionState.Idle;

        foreach (var key in KeysInPriorityOrder)
        {
            if (!pressedKeys.Contains(key))
            {
                continue;
            }

            return key switch
            {
                Keys.Escape => new PianoInputCommand(
                    isPracticeVisible ? PianoInputAction.AbortPractice : PianoInputAction.Exit),
                Keys.Q => new PianoInputCommand(PianoInputAction.Exit),
                Keys.Space => new PianoInputCommand(
                    isPracticeVisible ? PianoInputAction.AbortPractice : PianoInputAction.Clear),
                Keys.Enter when hasLoadedScore && practiceState is null or PracticeSessionState.Idle or PracticeSessionState.Finished =>
                    new PianoInputCommand(PianoInputAction.StartPractice),
                Keys.Enter => default,
                Keys.Tab => new PianoInputCommand(PianoInputAction.ToggleView),
                Keys.PageUp when hasLoadedScore => new PianoInputCommand(PianoInputAction.ScrollPrevious),
                Keys.PageDown when hasLoadedScore => new PianoInputCommand(PianoInputAction.ScrollNext),
                Keys.P when hasLoadedScore && !isPracticeVisible => new PianoInputCommand(PianoInputAction.StartScorePlayback),
                Keys.Up => new PianoInputCommand(PianoInputAction.ChangeOctave, octave + 1),
                Keys.Down => new PianoInputCommand(PianoInputAction.ChangeOctave, octave - 1),
                >= Keys.D1 and <= Keys.D8 => new PianoInputCommand(PianoInputAction.ChangeOctave, key - Keys.D0),
                Keys.M => new PianoInputCommand(PianoInputAction.PlayRandomMeasure),
                _ => default,
            };
        }

        return default;
    }
}
