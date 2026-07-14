using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class PracticeSessionTests
{
    [Fact]
    public void Update_ClockAdvances_TransitionsThroughCountInRunningAndFinished()
    {
        var time = new FakeTimeProvider();
        var session = new PracticeSession(CreateScore(), time);

        Assert.Equal(PracticeSessionState.Idle, session.State);
        session.Start(TimeSpan.Zero);
        Assert.Equal(PracticeSessionState.CountingIn, session.State);
        Assert.Equal(1, session.CountInTicksDue);

        for (int expectedTicks = 2; expectedTicks <= 4; expectedTicks++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(expectedTicks, session.CountInTicksDue);
        }

        time.Advance(TimeSpan.FromSeconds(1));
        session.Update();
        Assert.Equal(PracticeSessionState.Running, session.State);
        Assert.Equal(TimeSpan.FromSeconds(4), session.PracticeAnchor);

        time.Advance(TimeSpan.FromSeconds(1.2));
        session.Update();
        Assert.Equal(PracticeSessionState.Finished, session.State);
    }

    [Fact]
    public void AbortAndStart_ActiveOrFinishedSession_ReturnsToCountIn()
    {
        var time = new FakeTimeProvider();
        var session = new PracticeSession(CreateScore(), time);
        session.Start(TimeSpan.Zero);

        time.Advance(TimeSpan.FromSeconds(5.2));
        session.Update();
        Assert.Equal(PracticeSessionState.Finished, session.State);

        session.Start(TimeSpan.FromSeconds(10));
        Assert.Equal(PracticeSessionState.CountingIn, session.State);
        Assert.Equal(TimeSpan.FromSeconds(14), session.PracticeAnchor);
    }

    [Fact]
    public void Grade_RunningPerformance_UsesPracticeAnchorAndMeasuredNote()
    {
        var time = new FakeTimeProvider();
        var session = new PracticeSession(CreateScore(), time);
        session.Start(TimeSpan.FromSeconds(10));
        time.Advance(TimeSpan.FromSeconds(4));
        session.Update();
        var performed = new PerformedNote
        {
            Pitch = new Pitch(NoteLetter.C, 0, 4),
            StartTime = session.PracticeAnchor,
            ReleaseTime = session.PracticeAnchor + TimeSpan.FromSeconds(1),
        };
        time.Advance(TimeSpan.FromSeconds(1));

        var result = session.Grade([performed]);

        Assert.Equal(1, session.CursorBeats);
        Assert.Equal(Verdict.Correct, Assert.Single(result.Events).Verdict);
    }

    [Fact]
    public void IsVerdictDue_OnsetWindowCloses_ReturnsTrue()
    {
        var time = new FakeTimeProvider();
        var session = new PracticeSession(CreateScore(), time);
        session.Start(TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(4));
        session.Update();
        var expected = Assert.Single(ScoreDerivation.Flatten(CreateScore()));

        Assert.False(session.IsVerdictDue(expected));
        time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.True(session.IsVerdictDue(expected));
    }

    [Fact]
    public void BuildVisibleVerdicts_TiedEvent_ColorsEverySourceNote()
    {
        var time = new FakeTimeProvider();
        var score = CreateTiedScore();
        var session = new PracticeSession(score, time);
        session.Start(TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(9.2));
        session.Update();
        var performed = new PerformedNote
        {
            Pitch = score.Measures[0].Notes[0].Pitch,
            StartTime = session.PracticeAnchor + TimeSpan.FromSeconds(3),
            ReleaseTime = session.PracticeAnchor + TimeSpan.FromSeconds(5),
        };

        var result = session.Grade([performed]);
        var verdicts = session.BuildVisibleVerdicts(result);

        Assert.Equal(2, verdicts.Count);
        Assert.All(score.Measures.SelectMany(measure => measure.Notes), note =>
            Assert.Equal(Verdict.Correct, verdicts[note]));
    }

    [Fact]
    public void GetSessionNotes_CountInAndFutureNotes_OnlyReturnsGradeEligibleWindow()
    {
        var time = new FakeTimeProvider();
        var session = new PracticeSession(CreateScore(), time);
        session.Start(TimeSpan.Zero);
        time.Advance(TimeSpan.FromSeconds(4));
        session.Update();
        var pitch = new Pitch(NoteLetter.C, 0, 4);
        PerformedNote[] notes =
        [
            new PerformedNote { Pitch = pitch, StartTime = TimeSpan.FromSeconds(3) },
            new PerformedNote { Pitch = pitch, StartTime = TimeSpan.FromSeconds(3.9) },
            new PerformedNote { Pitch = pitch, StartTime = TimeSpan.FromSeconds(4) },
            new PerformedNote { Pitch = pitch, StartTime = TimeSpan.FromSeconds(4.1) },
        ];

        var sessionNotes = session.GetSessionNotes(notes);

        Assert.Equal([notes[1], notes[2]], sessionNotes);
    }

    private static Score CreateScore() =>
        new(
            "Practice",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)],
                    []),
            ]);

    private static Score CreateTiedScore()
    {
        var pitch = new Pitch(NoteLetter.D, 0, 4);
        return new Score(
            "Tied practice",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(60),
            0,
            [
                new ScoreMeasure([new ScoreNote(pitch, new NoteValue(4), 0, 3, Staff.Treble, TiesToNext: true)], []),
                new ScoreMeasure([new ScoreNote(pitch, new NoteValue(4), 1, 0, Staff.Treble)], []),
            ]);
    }
}
