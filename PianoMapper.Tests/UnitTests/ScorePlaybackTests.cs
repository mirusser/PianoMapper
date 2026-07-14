using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScorePlaybackTests
{
    [Fact]
    public void GetDueEvents_FakeClockAdvances_ReturnsOnlyNewlyDueEvents()
    {
        var time = new FakeTimeProvider();
        long startTimestamp = time.GetTimestamp();
        var schedule = new[]
        {
            new ScheduledScoreEvent(
                new ScoreEvent(new Pitch(NoteLetter.C, 0, 4), 0, 1, Staff.Treble, []),
                TimeSpan.Zero,
                TimeSpan.FromSeconds(0.5)),
            new ScheduledScoreEvent(
                new ScoreEvent(new Pitch(NoteLetter.D, 0, 4), 1, 1, Staff.Treble, []),
                TimeSpan.FromSeconds(0.5),
                TimeSpan.FromSeconds(0.5)),
        };

        var initiallyDue = ScorePlayback.GetDueEvents(schedule, time.GetElapsedTime(startTimestamp), startIndex: 0);
        time.Advance(TimeSpan.FromSeconds(0.5));
        var laterDue = ScorePlayback.GetDueEvents(schedule, time.GetElapsedTime(startTimestamp), startIndex: initiallyDue.Count);

        Assert.Single(initiallyDue);
        Assert.Single(laterDue);
        Assert.Equal(new Pitch(NoteLetter.D, 0, 4), laterDue[0].Event.Pitch);
    }

    [Fact]
    public void GetCursorBeats_ElapsedTime_ReturnsTempoRelativePosition()
    {
        double beats = ScorePlayback.GetCursorBeats(
            now: TimeSpan.FromSeconds(6),
            anchor: TimeSpan.FromSeconds(5),
            new Tempo(120));

        Assert.Equal(2, beats);
    }

    [Fact]
    public void GetDueEvents_ChordAtAnchor_ReturnsEveryChordMemberTogether()
    {
        var score = new Score(
            "Chord",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [
                        new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
                        new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble),
                    ],
                    []),
            ]);
        var schedule = ScorePlayback.CreateSchedule(score, TimeSpan.FromSeconds(5));

        var due = ScorePlayback.GetDueEvents(schedule, TimeSpan.FromSeconds(5), startIndex: 0);

        Assert.Equal(2, due.Count);
        Assert.All(due, item => Assert.Equal(TimeSpan.FromSeconds(5), item.DueTime));
    }
}
