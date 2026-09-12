using PianoMapper.Music;
using PianoMapper.Web.Input;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserMidiInputStateTests
{
    [Theory]
    [InlineData(21, NoteLetter.A, 0, 0)]
    [InlineData(60, NoteLetter.C, 0, 4)]
    [InlineData(61, NoteLetter.C, 1, 4)]
    [InlineData(108, NoteLetter.C, 0, 8)]
    public void Handle_NoteOn_MapsRolandFp10RangeToPitch(
        int midiNumber,
        NoteLetter expectedLetter,
        int expectedAlter,
        int expectedOctave)
    {
        var timeline = new NoteTimeline();
        var state = new BrowserMidiInputState(timeline);
        var midiEvent = CreateEvent($"midi:roland:0:{midiNumber}", midiNumber, velocity: 96, isNoteOn: true);

        var command = state.Handle(midiEvent, TimeSpan.FromSeconds(1));

        Assert.Equal(BrowserInputCommandKind.NoteOn, command.Kind);
        Assert.Equal(new Pitch(expectedLetter, expectedAlter, expectedOctave), command.Pitch);
        Assert.Equal(96, command.Velocity);
        Assert.Equal(1, state.ActiveNoteCount);
    }

    [Fact]
    public void Handle_NoteOff_CompletesTrackedNote()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserMidiInputState(timeline);
        const string noteId = "midi:roland:0:60";
        var noteOn = state.Handle(CreateEvent(noteId, 60, velocity: 80, isNoteOn: true), TimeSpan.FromSeconds(1));

        var noteOff = state.Handle(CreateEvent(noteId, 60, velocity: 45, isNoteOn: false), TimeSpan.FromSeconds(2));

        Assert.Equal(BrowserInputCommandKind.NoteOff, noteOff.Kind);
        Assert.Same(noteOn.Note, noteOff.Note);
        Assert.Equal(TimeSpan.FromSeconds(2), noteOn.Note?.ReleaseTime);
        Assert.Equal(0, state.ActiveNoteCount);
    }

    [Fact]
    public void Clear_HeldNotes_RemovesThemFromTimeline()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserMidiInputState(timeline);
        state.Handle(CreateEvent("midi:roland:0:60", 60, velocity: 80, isNoteOn: true), TimeSpan.FromSeconds(1));

        var command = state.Clear(TimeSpan.FromSeconds(2));

        Assert.Equal(BrowserInputCommandKind.Clear, command.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.Empty(timeline.Snapshot(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ReleaseAll_DeviceDisconnected_CompletesEveryHeldNote()
    {
        var timeline = new NoteTimeline();
        var state = new BrowserMidiInputState(timeline);
        state.Handle(CreateEvent("midi:roland:0:60", 60, velocity: 80, isNoteOn: true), TimeSpan.FromSeconds(1));
        state.Handle(CreateEvent("midi:roland:0:64", 64, velocity: 80, isNoteOn: true), TimeSpan.FromSeconds(1));
        var releaseTime = TimeSpan.FromSeconds(2);

        var command = state.ReleaseAll(releaseTime);

        Assert.Equal(BrowserInputCommandKind.ReleaseHeldNotes, command.Kind);
        Assert.Equal(0, state.ActiveNoteCount);
        Assert.All(timeline.Snapshot(releaseTime), note => Assert.Equal(releaseTime, note.ReleaseTime));
    }

    private static BrowserMidiEvent CreateEvent(
        string noteId,
        int midiNumber,
        int velocity,
        bool isNoteOn) =>
        new()
        {
            NoteId = noteId,
            MidiNumber = midiNumber,
            Velocity = velocity,
            IsNoteOn = isNoteOn,
        };
}
