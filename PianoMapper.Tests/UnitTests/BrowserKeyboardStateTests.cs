using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserKeyboardStateTests
{
    [Theory]
    [InlineData("BracketLeft", (int)BrowserInputCommandKind.PreviousMeasures)]
    [InlineData("BracketRight", (int)BrowserInputCommandKind.NextMeasures)]
    [InlineData("KeyP", (int)BrowserInputCommandKind.StartScorePlayback)]
    [InlineData("KeyT", (int)BrowserInputCommandKind.StartPractice)]
    [InlineData("KeyV", (int)BrowserInputCommandKind.ToggleView)]
    [InlineData("KeyM", (int)BrowserInputCommandKind.PlayRandomMeasure)]
    public void HandleKeyDown_NonNoteControlCode_ReturnsCommand(
        string code,
        int expectedKindValue)
    {
        var state = new BrowserKeyboardState(new NoteTimeline(new FakeTimeProvider()), initialOctave: 4);

        var command = state.HandleKeyDown(code, isRepeat: false, TimeSpan.FromSeconds(1));

        Assert.True(command.IsHandled);
        Assert.Equal((BrowserInputCommandKind)expectedKindValue, command.Kind);
    }

    [Fact]
    public void HandleKeyDownAndUp_NoteCode_TracksMappedPerformance()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);

        var noteOn = state.HandleKeyDown("KeyA", isRepeat: false, TimeSpan.FromSeconds(2));
        var noteOff = state.HandleKeyUp("KeyA", TimeSpan.FromSeconds(2.4));

        Assert.Equal(BrowserInputCommandKind.NoteOn, noteOn.Kind);
        Assert.Equal(BrowserInputCommandKind.NoteOff, noteOff.Kind);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 4), noteOn.Pitch);
        Assert.Same(noteOn.Note, noteOff.Note);
        Assert.Equal(TimeSpan.FromSeconds(2), noteOn.Note?.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(2.4), noteOn.Note?.ReleaseTime);
        Assert.Equal(0, state.ActiveNoteCount);
    }

    [Fact]
    public void HandleKeyDown_RepeatedNote_DoesNotStartDuplicate()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);
        state.HandleKeyDown("KeyA", isRepeat: false, TimeSpan.FromSeconds(1));

        var repeated = state.HandleKeyDown("KeyA", isRepeat: true, TimeSpan.FromSeconds(1.1));

        Assert.True(repeated.IsHandled);
        Assert.Equal(BrowserInputCommandKind.None, repeated.Kind);
        Assert.Equal(1, state.ActiveNoteCount);
        Assert.Single(timeline.Snapshot(TimeSpan.FromSeconds(1.1)));
    }

    [Fact]
    public void HandleKeyUp_OctaveChangedWhileHeld_ReleasesOriginalPitch()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);
        var noteOn = state.HandleKeyDown("KeyA", isRepeat: false, TimeSpan.FromSeconds(1));

        var octaveChange = state.HandleKeyDown("KeyX", isRepeat: false, TimeSpan.FromSeconds(1.1));
        var noteOff = state.HandleKeyUp("KeyA", TimeSpan.FromSeconds(1.2));

        Assert.Equal(BrowserInputCommandKind.OctaveChanged, octaveChange.Kind);
        Assert.Equal(5, state.CurrentOctave);
        Assert.Equal(new Pitch(NoteLetter.C, 0, 4), noteOn.Pitch);
        Assert.Equal(noteOn.Pitch, noteOff.Pitch);
    }

    [Fact]
    public void HandleKeyDown_Clear_RemovesNotesAndMakesLaterKeyUpHarmless()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);
        state.HandleKeyDown("KeyA", isRepeat: false, TimeSpan.FromSeconds(1));
        state.HandleKeyDown("KeyW", isRepeat: false, TimeSpan.FromSeconds(1));

        var clear = state.HandleKeyDown("KeyC", isRepeat: false, TimeSpan.FromSeconds(1.1));
        var laterKeyUp = state.HandleKeyUp("KeyA", TimeSpan.FromSeconds(1.2));

        Assert.Equal(BrowserInputCommandKind.Clear, clear.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.Empty(timeline.Snapshot(TimeSpan.FromSeconds(1.2)));
        Assert.Equal(BrowserInputCommandKind.None, laterKeyUp.Kind);
    }

    [Fact]
    public void ReleaseAll_FocusLost_CompletesEveryHeldNote()
    {
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);
        state.HandleKeyDown("KeyA", isRepeat: false, TimeSpan.FromSeconds(1));
        state.HandleKeyDown("KeyW", isRepeat: false, TimeSpan.FromSeconds(1.1));
        var releaseTime = TimeSpan.FromSeconds(1.5);

        var clear = state.ReleaseAll(releaseTime);

        Assert.Equal(BrowserInputCommandKind.ReleaseHeldNotes, clear.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.All(
            timeline.Snapshot(releaseTime),
            note => Assert.Equal(releaseTime, note.ReleaseTime));
    }

    [Fact]
    public void ReleaseAll_NoHeldNotes_ReturnsNoCommand()
    {
        var state = new BrowserKeyboardState(new NoteTimeline(new FakeTimeProvider()), initialOctave: 4);

        var command = state.ReleaseAll(TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
    }

    [Fact]
    public void HandleKeyDown_AllNoteCodes_TracksThirteenIndependentNotes()
    {
        string[] codes =
        [
            "KeyA", "KeyW", "KeyS", "KeyE", "KeyD", "KeyF", "KeyR",
            "KeyJ", "KeyU", "KeyK", "KeyI", "KeyL", "Semicolon",
        ];
        var timeline = new NoteTimeline(new FakeTimeProvider());
        var state = new BrowserKeyboardState(timeline, initialOctave: 4);

        foreach (string code in codes)
        {
            state.HandleKeyDown(code, isRepeat: false, TimeSpan.FromSeconds(1));
        }

        Assert.Equal(13, state.ActiveNoteCount);
        Assert.Equal(13, timeline.Snapshot(TimeSpan.FromSeconds(1)).Count);
    }
}
