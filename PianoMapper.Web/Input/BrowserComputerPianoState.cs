using PianoMapper.Music;

namespace PianoMapper.Web.Input;

/// <summary>
/// Tracks the opt-in "desktop-style" computer-key piano: whether it's enabled, which keyboard codes are currently
/// held, and which pitch each one started (so an octave change mid-hold can't change what key-up releases). Starts
/// and releases notes through the shared, source-neutral <see cref="BrowserNoteInputState"/>. Kept as its own type
/// rather than mixed into <see cref="BrowserKeyboardState"/>'s control-shortcut table, per this feature's design.
/// </summary>
internal sealed class BrowserComputerPianoState(BrowserNoteInputState noteInputState)
{
    private readonly BrowserNoteInputState noteInputState =
        noteInputState ?? throw new ArgumentNullException(nameof(noteInputState));
    private readonly Dictionary<string, Pitch> heldPitchesByCode = new(StringComparer.Ordinal);

    internal bool IsEnabled { get; private set; }

    internal void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    /// <summary>
    /// Starts the note mapped to <paramref name="code"/> at <paramref name="octave"/>. Returns an unhandled
    /// <see cref="BrowserInputCommand"/> (so the caller can fall back to control-shortcut handling) only when
    /// disabled or when the code isn't a piano key — in both cases nothing here claims the code, so shortcuts
    /// (several of which intentionally share codes with piano notes) still work normally. On repeat or when the
    /// code is already held, returns a *handled* no-op instead: the code IS a piano key and IS claimed, so it must
    /// not fall through and accidentally trigger an unrelated shortcut bound to the same code (e.g. re-triggering
    /// the "Clear" shortcut on <c>KeyC</c> while that piano key is still being held).
    /// </summary>
    internal BrowserInputCommand HandleKeyDown(string code, bool isRepeat, int octave, TimeSpan eventTime)
    {
        if (!IsEnabled || !BrowserComputerPianoBindings.TryGetPitch(code, octave, out Pitch pitch))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: false);
        }

        if (isRepeat || heldPitchesByCode.ContainsKey(code))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: true);
        }

        heldPitchesByCode.Add(code, pitch);
        return noteInputState.StartNote($"key:{code}", pitch, eventTime, WebAudioDefaultVelocity);
    }

    /// <summary>
    /// Releases the pitch that was started for <paramref name="code"/>, regardless of the current octave. Returns
    /// an unhandled command if that code isn't currently held (including when the computer piano is disabled, since
    /// nothing could have started it).
    /// </summary>
    internal BrowserInputCommand HandleKeyUp(string code, TimeSpan eventTime)
    {
        if (!heldPitchesByCode.Remove(code))
        {
            return new BrowserInputCommand(BrowserInputCommandKind.None, IsHandled: false);
        }

        return noteInputState.ReleaseNote($"key:{code}", eventTime);
    }

    /// <summary>Releases every currently held computer-key note (e.g. on focus/visibility loss), one command each.</summary>
    internal IReadOnlyList<BrowserInputCommand> ReleaseAll(TimeSpan eventTime)
    {
        if (heldPitchesByCode.Count == 0)
        {
            return [];
        }

        BrowserInputCommand[] commands = heldPitchesByCode.Keys
            .Select(code => noteInputState.ReleaseNote($"key:{code}", eventTime))
            .ToArray();
        heldPitchesByCode.Clear();
        return commands;
    }

    // Computer-key input has no velocity concept; use the same default MIDI/pointer input falls back to.
    private const int WebAudioDefaultVelocity = 80;
}
