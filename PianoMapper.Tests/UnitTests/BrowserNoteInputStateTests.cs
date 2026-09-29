using PianoMapper.Music;
using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserNoteInputStateTests
{
    private static readonly Pitch MiddleC = new(NoteLetter.C, 0, 4);

    [Fact]
    public void StartNote_NewNote_ReturnsNoteOnCommandAndTracksIt()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);

        BrowserInputCommand command = state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1), velocity: 90);

        Assert.Equal(BrowserInputCommandKind.NoteOn, command.Kind);
        Assert.Equal("pointer:1", command.NoteId);
        Assert.Equal(MiddleC, command.Pitch);
        Assert.Equal(90, command.Velocity);
        Assert.Equal(1, state.ActiveNoteCount);
    }

    [Fact]
    public void StartNote_DuplicateSourceId_ReturnsNoneCommandWithoutRetriggering()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);
        state.StartNote("key:KeyA", MiddleC, TimeSpan.FromSeconds(1), velocity: 90);

        BrowserInputCommand command = state.StartNote(
            "key:KeyA",
            new Pitch(NoteLetter.D, 0, 4),
            TimeSpan.FromSeconds(1.1),
            velocity: 90);

        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
        Assert.Equal(1, state.ActiveNoteCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void StartNote_VelocityOutOfRange_Throws(int velocity)
    {
        var state = new BrowserNoteInputState(new NoteTimeline());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.StartNote("pointer:1", MiddleC, TimeSpan.Zero, velocity));
    }

    [Fact]
    public void ReleaseNote_TrackedNote_CompletesItInTimelineAndReturnsNoteOff()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);
        BrowserInputCommand started = state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1), 90);

        BrowserInputCommand released = state.ReleaseNote("pointer:1", TimeSpan.FromSeconds(2));

        Assert.Equal(BrowserInputCommandKind.NoteOff, released.Kind);
        Assert.Same(started.Note, released.Note);
        Assert.Equal(TimeSpan.FromSeconds(2), started.Note?.ReleaseTime);
        Assert.Equal(0, state.ActiveNoteCount);
    }

    [Fact]
    public void ReleaseNote_UnknownSourceId_ReturnsNoneCommandWithoutThrowing()
    {
        var state = new BrowserNoteInputState(new NoteTimeline());

        BrowserInputCommand command = state.ReleaseNote("pointer:missing", TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
    }

    [Fact]
    public void Clear_HeldNotes_RemovesThemFromTimeline()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);
        state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1), 90);

        BrowserInputCommand command = state.Clear(TimeSpan.FromSeconds(2));

        Assert.Equal(BrowserInputCommandKind.Clear, command.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.Empty(timeline.Snapshot(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Clear_NoHeldNotes_ReturnsClearCommandWithoutThrowing()
    {
        var state = new BrowserNoteInputState(new NoteTimeline());

        BrowserInputCommand command = state.Clear(TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.Clear, command.Kind);
    }

    [Fact]
    public void ReleaseAll_HeldNotes_CompletesEveryOneAtTheGivenTime()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);
        state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1), 90);
        state.StartNote("key:KeyD", new Pitch(NoteLetter.D, 0, 4), TimeSpan.FromSeconds(1), 90);
        var releaseTime = TimeSpan.FromSeconds(2);

        BrowserInputCommand command = state.ReleaseAll(releaseTime);

        Assert.Equal(BrowserInputCommandKind.ReleaseHeldNotes, command.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.All(timeline.Snapshot(releaseTime), note => Assert.Equal(releaseTime, note.ReleaseTime));
    }

    [Fact]
    public void ReleaseAll_NoHeldNotes_ReturnsNoneCommand()
    {
        var state = new BrowserNoteInputState(new NoteTimeline());

        BrowserInputCommand command = state.ReleaseAll(TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.None, command.Kind);
    }

    [Fact]
    public void StartNote_SamePitchFromTwoDifferentSources_BothTrackedIndependently()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);

        state.StartNote("midi:roland:0:60", MiddleC, TimeSpan.FromSeconds(1), 90);
        BrowserInputCommand pointerStart = state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1.1), 90);

        Assert.Equal(BrowserInputCommandKind.NoteOn, pointerStart.Kind);
        Assert.Equal(2, state.ActiveNoteCount);
    }

    [Fact]
    public void ReleaseNote_OneOfTwoSourcesHoldingSamePitch_OnlyReleasesThatSource()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserNoteInputState(timeline);
        state.StartNote("midi:roland:0:60", MiddleC, TimeSpan.FromSeconds(1), 90);
        state.StartNote("pointer:1", MiddleC, TimeSpan.FromSeconds(1), 90);

        BrowserInputCommand released = state.ReleaseNote("pointer:1", TimeSpan.FromSeconds(2));

        Assert.Equal(BrowserInputCommandKind.NoteOff, released.Kind);
        Assert.Equal(1, state.ActiveNoteCount);
    }
}
