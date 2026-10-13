using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PerformedNoteTests
{
    [Fact]
    public void Completed_PitchAndTimes_ReturnsTheNoteAlreadyReleased()
    {
        var pitch = new Pitch(NoteLetter.C, 0, 4);

        PerformedNote note = PerformedNote.Completed(pitch, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5));

        Assert.Equal(pitch, note.Pitch);
        Assert.Equal(TimeSpan.FromSeconds(1), note.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(1.5), note.ReleaseTime);
    }
}
