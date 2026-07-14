using OpenTK.Windowing.GraphicsLibraryFramework;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class OpenNoteTrackerTests
{
    [Fact]
    public void Release_OctaveMappingChanged_ReturnsOriginallyTrackedNote()
    {
        var tracker = new OpenNoteTracker();
        var note = new PerformedNote { Pitch = new Pitch(NoteLetter.C, 0, 4), StartTime = TimeSpan.Zero };
        tracker.Track(Keys.A, note);

        bool released = tracker.TryRelease(Keys.A, out var releasedNote);

        Assert.True(released);
        Assert.Same(note, releasedNote);
    }

    [Fact]
    public void Release_AfterClear_ReturnsFalse()
    {
        var tracker = new OpenNoteTracker();
        tracker.Track(
            Keys.A,
            new PerformedNote { Pitch = new Pitch(NoteLetter.C, 0, 4), StartTime = TimeSpan.Zero });
        tracker.Clear();

        bool released = tracker.TryRelease(Keys.A, out _);

        Assert.False(released);
    }
}
