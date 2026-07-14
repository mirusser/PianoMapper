using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class GraderTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Tempo Tempo = new(60);
    private static readonly TimeSpan Anchor = TimeSpan.FromSeconds(10);

    [Fact]
    public void Grade_ExactPitchOnsetAndDuration_ReturnsCorrect()
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        PerformedNote[] performed =
        [
            new PerformedNote
            {
                Pitch = C4,
                StartTime = Anchor,
                ReleaseTime = Anchor + TimeSpan.FromSeconds(1),
            },
        ];

        var result = Grader.Grade(expected, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(1));

        var gradedEvent = Assert.Single(result.Events);
        Assert.Equal(Verdict.Correct, gradedEvent.Verdict);
        Assert.Equal(100, result.Summary.AccuracyPercent);
    }

    [Theory]
    [InlineData(-100, 0, 1.0, (int)Verdict.Early)]
    [InlineData(100, 0, 1.0, (int)Verdict.Late)]
    [InlineData(-250, 0, 1.0, (int)Verdict.Missed)]
    [InlineData(250, 0, 1.0, (int)Verdict.Missed)]
    [InlineData(0, 1, 1.0, (int)Verdict.WrongPitch)]
    [InlineData(0, 0, 0.4, (int)Verdict.TooShort)]
    [InlineData(0, 0, 1.6, (int)Verdict.TooLong)]
    public void Grade_SinglePerformance_ReturnsExpectedVerdict(
        int onsetOffsetMilliseconds,
        int pitchOffset,
        double durationRatio,
        int expectedVerdictValue)
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        TimeSpan start = Anchor + TimeSpan.FromMilliseconds(onsetOffsetMilliseconds);
        var performedPitch = new Pitch(NoteLetter.C, pitchOffset, 4);
        PerformedNote[] performed =
        [
            new PerformedNote
            {
                Pitch = performedPitch,
                StartTime = start,
                ReleaseTime = start + TimeSpan.FromSeconds(durationRatio),
            },
        ];

        var result = Grader.Grade(expected, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(2));

        Assert.Equal((Verdict)expectedVerdictValue, result.Events[0].Verdict);
    }

    [Fact]
    public void Grade_UnmatchedExpectedAndPerformed_ReturnsMissedAndExtra()
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        PerformedNote[] performed =
        [
            new PerformedNote
            {
                Pitch = new Pitch(NoteLetter.D, 0, 4),
                StartTime = Anchor + TimeSpan.FromSeconds(1),
                ReleaseTime = Anchor + TimeSpan.FromSeconds(2),
            },
        ];

        var result = Grader.Grade(expected, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(2));

        Assert.Collection(
            result.Events,
            gradedEvent => Assert.Equal(Verdict.Missed, gradedEvent.Verdict),
            gradedEvent => Assert.Equal(Verdict.Extra, gradedEvent.Verdict));
    }

    [Fact]
    public void Grade_ChordWithOneWrongMember_ReturnsCorrectAndWrongPitch()
    {
        var e4 = new Pitch(NoteLetter.E, 0, 4);
        ScoreEvent[] expected =
        [
            new ScoreEvent(C4, 0, 1, Staff.Treble, []),
            new ScoreEvent(e4, 0, 1, Staff.Treble, []),
        ];
        PerformedNote[] performed =
        [
            Completed(C4, Anchor, TimeSpan.FromSeconds(1)),
            Completed(new Pitch(NoteLetter.D, 0, 4), Anchor, TimeSpan.FromSeconds(1)),
        ];

        var result = Grader.Grade(expected, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(1));

        Assert.Equal(Verdict.Correct, result.Events[0].Verdict);
        Assert.Equal(Verdict.WrongPitch, result.Events[1].Verdict);
    }

    [Fact]
    public void Grade_RepeatedPitchInCloseSuccession_MatchesNearestOnsets()
    {
        ScoreEvent[] expected =
        [
            new ScoreEvent(C4, 0, 0.1, Staff.Treble, []),
            new ScoreEvent(C4, 0.3, 0.1, Staff.Treble, []),
        ];
        var nearSecond = Completed(C4, Anchor + TimeSpan.FromSeconds(0.29), TimeSpan.FromSeconds(0.1));
        var nearFirst = Completed(C4, Anchor + TimeSpan.FromSeconds(0.01), TimeSpan.FromSeconds(0.1));

        var result = Grader.Grade(expected, Tempo, [nearSecond, nearFirst], Anchor, Anchor + TimeSpan.FromSeconds(1));

        Assert.Same(nearFirst, result.Events[0].Performed);
        Assert.Same(nearSecond, result.Events[1].Performed);
    }

    [Fact]
    public void Grade_WrongPitchCloserThanCorrectPitch_PrefersCorrectPitch()
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        var wrong = Completed(new Pitch(NoteLetter.C, 1, 4), Anchor, TimeSpan.FromSeconds(1));
        var correctButLate = Completed(C4, Anchor + TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));

        var result = Grader.Grade(expected, Tempo, [wrong, correctButLate], Anchor, Anchor + TimeSpan.FromSeconds(2));

        Assert.Same(correctButLate, result.Events[0].Performed);
        Assert.Equal(Verdict.Late, result.Events[0].Verdict);
        Assert.Equal(Verdict.Extra, result.Events[1].Verdict);
    }

    [Fact]
    public void Grade_CustomOnsetTolerance_UsesOptionsValue()
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        var performed = Completed(C4, Anchor + TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        var options = new GradingOptions { OnsetTolerance = TimeSpan.FromMilliseconds(50) };

        var result = Grader.Grade(expected, Tempo, [performed], Anchor, Anchor + TimeSpan.FromSeconds(2), options);

        Assert.Equal(Verdict.Missed, result.Events[0].Verdict);
    }

    [Theory]
    [InlineData(0.4, (int)Verdict.TooShort)]
    [InlineData(0.6, (int)Verdict.Correct)]
    [InlineData(0.8, (int)Verdict.TooLong)]
    public void Grade_CustomDurationRatios_UsesOptionsValues(double durationRatio, int expectedVerdictValue)
    {
        ScoreEvent[] expected = [new ScoreEvent(C4, 0, 1, Staff.Treble, [])];
        var performed = Completed(C4, Anchor, TimeSpan.FromSeconds(durationRatio));
        var options = new GradingOptions
        {
            MinimumDurationRatio = 0.5,
            MaximumDurationRatio = 0.7,
        };

        var result = Grader.Grade(expected, Tempo, [performed], Anchor, Anchor + TimeSpan.FromSeconds(1), options);

        Assert.Equal((Verdict)expectedVerdictValue, Assert.Single(result.Events).Verdict);
    }

    [Fact]
    public void Grade_MixedPerformance_SummarizesEveryVerdictAndAccuracy()
    {
        ScoreEvent[] expected =
        [
            new ScoreEvent(C4, 0, 1, Staff.Treble, []),
            new ScoreEvent(new Pitch(NoteLetter.D, 0, 4), 1, 1, Staff.Treble, []),
        ];
        PerformedNote[] performed =
        [
            Completed(C4, Anchor, TimeSpan.FromSeconds(1)),
            Completed(new Pitch(NoteLetter.E, 0, 4), Anchor + TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)),
        ];

        var result = Grader.Grade(expected, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(3));

        Assert.Equal(50, result.Summary.AccuracyPercent);
        Assert.Equal(1, result.Summary.Counts[Verdict.Correct]);
        Assert.Equal(1, result.Summary.Counts[Verdict.Missed]);
        Assert.Equal(1, result.Summary.Counts[Verdict.Extra]);
        Assert.Equal(Enum.GetValues<Verdict>().Length, result.Summary.Counts.Count);
    }

    private static PerformedNote Completed(Pitch pitch, TimeSpan start, TimeSpan duration) =>
        new()
        {
            Pitch = pitch,
            StartTime = start,
            ReleaseTime = start + duration,
        };
}
