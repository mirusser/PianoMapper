using PianoMapper.Music;
using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserComputerPianoStateTests
{
    [Fact]
    public void HandleKeyDown_WhenDisabled_IsUnhandledAndDoesNotStartANote()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);

        BrowserInputCommand command = state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);

        Assert.False(command.IsHandled);
        Assert.Equal(0, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyDown_WhenEnabledForAMappedCode_StartsTheNote()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);

        BrowserInputCommand command = state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.NoteOn, command.Kind);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 4), command.Pitch);
        Assert.Equal(1, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyDown_WhenEnabledForAnUnmappedCode_IsUnhandled()
    {
        var noteInputState = new BrowserNoteInputState(new NoteTimeline());
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);

        BrowserInputCommand command = state.HandleKeyDown("KeyQ", isRepeat: false, octave: 4, TimeSpan.Zero);

        Assert.False(command.IsHandled);
    }

    [Fact]
    public void HandleKeyDown_KeyRepeat_DoesNotRetrigger()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);
        state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);

        BrowserInputCommand repeatCommand = state.HandleKeyDown(
            "KeyZ",
            isRepeat: true,
            octave: 4,
            TimeSpan.FromMilliseconds(50));

        // Handled (not just a no-op Kind): the code is a claimed piano key, so it must not fall through to
        // control-shortcut handling, which would happen if this returned IsHandled: false.
        Assert.True(repeatCommand.IsHandled);
        Assert.Equal(BrowserInputCommandKind.None, repeatCommand.Kind);
        Assert.Equal(1, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyDown_SameCodeHeldTwice_DoesNotRetrigger()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);
        state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);

        BrowserInputCommand secondDown = state.HandleKeyDown(
            "KeyZ",
            isRepeat: false,
            octave: 4,
            TimeSpan.FromMilliseconds(10));

        // Handled, for the same reason as the repeat case: an unmapped-elsewhere "already held" event must not
        // fall through to control shortcuts bound to the same code.
        Assert.True(secondDown.IsHandled);
        Assert.Equal(BrowserInputCommandKind.None, secondDown.Kind);
        Assert.Equal(1, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyUp_ReleasesTheOriginallyStartedPitchEvenAfterAnOctaveChange()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);
        state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);

        // Octave changes mid-hold (e.g. the user pressed an octave-up shortcut while still holding the key) do not
        // affect release: HandleKeyUp doesn't even take an octave parameter, it releases whatever pitch was
        // recorded at key-down time.
        BrowserInputCommand released = state.HandleKeyUp("KeyZ", TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.NoteOff, released.Kind);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 4), released.Pitch);
        Assert.Equal(0, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyUp_UnheldCode_IsUnhandled()
    {
        var state = new BrowserComputerPianoState(new BrowserNoteInputState(new NoteTimeline()));
        state.SetEnabled(true);

        BrowserInputCommand command = state.HandleKeyUp("KeyZ", TimeSpan.Zero);

        Assert.False(command.IsHandled);
    }

    [Fact]
    public void ReleaseAll_HeldKeys_ReleasesEveryOneAndReturnsOneCommandEach()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);
        state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);
        state.HandleKeyDown("KeyX", isRepeat: false, octave: 4, TimeSpan.Zero);

        IReadOnlyList<BrowserInputCommand> released = state.ReleaseAll(TimeSpan.FromSeconds(1));

        Assert.Equal(2, released.Count);
        Assert.All(released, command => Assert.Equal(BrowserInputCommandKind.NoteOff, command.Kind));
        Assert.Equal(0, noteInputState.ActiveNoteCount);
    }

    [Fact]
    public void ReleaseAll_NoHeldKeys_ReturnsEmpty()
    {
        var state = new BrowserComputerPianoState(new BrowserNoteInputState(new NoteTimeline()));

        IReadOnlyList<BrowserInputCommand> released = state.ReleaseAll(TimeSpan.Zero);

        Assert.Empty(released);
    }

    [Fact]
    public void SetEnabled_False_DisablesFurtherKeyDownHandlingButDoesNotReleaseAlreadyHeldNotes()
    {
        var timeline = new NoteTimeline();
        var noteInputState = new BrowserNoteInputState(timeline);
        var state = new BrowserComputerPianoState(noteInputState);
        state.SetEnabled(true);
        state.HandleKeyDown("KeyZ", isRepeat: false, octave: 4, TimeSpan.Zero);

        state.SetEnabled(false);
        BrowserInputCommand newKeyDown = state.HandleKeyDown("KeyX", isRepeat: false, octave: 4, TimeSpan.Zero);

        Assert.False(newKeyDown.IsHandled);
        Assert.Equal(1, noteInputState.ActiveNoteCount);
    }
}
