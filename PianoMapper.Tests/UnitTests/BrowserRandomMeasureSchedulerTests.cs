using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class BrowserRandomMeasureSchedulerTests
{
    [Fact]
    public void CreateEvents_RhythmicMeasure_ReturnsSequentialWebAudioSchedule()
    {
        var measure = new RandomMeasure(
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            [
                new RandomMeasureEvent(new Pitch(NoteLetter.A, 0, 4), new NoteValue(4), 1),
                new RandomMeasureEvent(new Pitch(NoteLetter.C, 0, 5), new NoteValue(2), 2),
            ]);

        var events = BrowserRandomMeasureScheduler.CreateEvents(measure, TimeSpan.FromSeconds(10));

        Assert.Collection(
            events,
            scoreEvent =>
            {
                Assert.Equal(TimeSpan.FromSeconds(10), scoreEvent.StartTime);
                Assert.Equal(TimeSpan.FromSeconds(1), scoreEvent.Duration);
            },
            scoreEvent =>
            {
                Assert.Equal(TimeSpan.FromSeconds(11), scoreEvent.StartTime);
                Assert.Equal(TimeSpan.FromSeconds(2), scoreEvent.Duration);
            });
    }
}
