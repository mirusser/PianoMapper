using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class PlayAlongResultMapperTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly Tempo Tempo = new(60);
    private static readonly TimeSpan Anchor = TimeSpan.FromSeconds(10);
    private static readonly GradingOptions Options = new();

    [Fact]
    public void Map_OnTimePerformance_ProducesCleanPromptsWithoutResponseTimes()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(D4, 1)];
        PerformedNote[] performed = [Played(C4, 0), Played(D4, 1000)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchHoldAndRhythm);

        Assert.Equal(2, outcome.PromptResults.Count);
        Assert.All(outcome.PromptResults, result =>
        {
            Assert.True(result.IsFirstTryCorrect);
            Assert.True(result.IsPitchFirstTryCorrect);
            Assert.Equal(Verdict.Correct, result.OnsetVerdict);
            Assert.Equal(Verdict.Correct, result.DurationVerdict);
            Assert.False(result.WasMissed);
            Assert.True(result.IsComplete);
            Assert.Null(result.ResponseTime);
            Assert.Equal(0, result.WrongAttemptCount);
        });
        Assert.Equal([0, 1], outcome.PromptResults.Select(result => result.PromptIndex));
        Assert.Equal([0.0, 1.0], outcome.PromptResults.Select(result => result.OnsetBeats));
        Assert.Equal(0, outcome.ExtraNoteCount);
    }

    [Fact]
    public void Map_LateRightPitch_KeepsPitchCorrectAndReportsTheSignedDeviation()
    {
        ScoreEvent[] events = [Event(C4, 0)];
        PerformedNote[] performed = [Played(C4, 120)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        NoteReadingPromptResult result = Assert.Single(outcome.PromptResults);
        Assert.False(result.IsFirstTryCorrect);
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(120), result.OnsetDeviation);
        Assert.Null(result.DurationVerdict);
        Assert.True(result.HasTimingMistake);
        Assert.Equal(1, result.WrongAttemptCount);
    }

    [Fact]
    public void Map_WrongPitchWithinTheWindow_IsAPitchMissAndCapturesThePlayedPitch()
    {
        ScoreEvent[] events = [Event(C4, 0)];
        PerformedNote[] performed = [Played(E4, 20)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        NoteReadingPromptResult result = Assert.Single(outcome.PromptResults);
        Assert.False(result.IsPitchFirstTryCorrect);
        Assert.Equal([E4], result.WrongPlayedPitches.ToArray());
        Assert.Equal(Verdict.Correct, result.OnsetVerdict);
        Assert.False(result.WasMissed);
    }

    [Fact]
    public void Map_SkippedNote_IsMissedAndAPitchMissInPitchGradedModes()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(D4, 1)];
        PerformedNote[] performed = [Played(C4, 0)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        NoteReadingPromptResult skipped = outcome.PromptResults[1];
        Assert.True(skipped.WasMissed);
        Assert.False(skipped.IsFirstTryCorrect);
        Assert.False(skipped.IsPitchFirstTryCorrect);
        Assert.Equal(Verdict.Missed, skipped.OnsetVerdict);
        Assert.Null(skipped.OnsetDeviation);
        Assert.Equal(1, outcome.VerdictCounts[Verdict.Missed]);
    }

    [Fact]
    public void Map_RhythmOnly_NeverMarksAPitchMissEvenForASkippedNote()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(C4, 1)];
        PerformedNote[] performed = [Played(E4, 0)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.RhythmOnly, ignorePitch: true);

        Assert.All(outcome.PromptResults, result => Assert.True(result.IsPitchFirstTryCorrect));
        Assert.Empty(outcome.PromptResults[0].WrongPlayedPitches);
        Assert.True(outcome.PromptResults[1].WasMissed);
        Assert.Equal(Verdict.Missed, outcome.PromptResults[1].OnsetVerdict);
        Assert.Null(outcome.PromptResults[0].DurationVerdict);
    }

    [Fact]
    public void Map_ChordPrompt_GroupsMembersAndReportsTheWorstOnset()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(E4, 0)];
        PerformedNote[] performed = [Played(C4, 0), Played(E4, 130)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        NoteReadingPromptResult result = Assert.Single(outcome.PromptResults);
        Assert.Equal([C4, E4], result.ExpectedPitches.ToArray());
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(130), result.OnsetDeviation);
        Assert.True(result.IsPitchFirstTryCorrect);
    }

    [Fact]
    public void Map_ShortHeldNote_ReportsTheDurationVerdictOnlyWhenDurationIsGraded()
    {
        ScoreEvent[] events = [Event(C4, 0)];
        PerformedNote[] performed = [Played(C4, 0, holdMilliseconds: 300)];

        PlayAlongOutcome graded = Map(events, performed, NoteReadingMode.PitchHoldAndRhythm);
        PlayAlongOutcome notGraded = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        Assert.Equal(Verdict.TooShort, Assert.Single(graded.PromptResults).DurationVerdict);
        Assert.Null(Assert.Single(notGraded.PromptResults).DurationVerdict);
        Assert.True(Assert.Single(notGraded.PromptResults).IsFirstTryCorrect);
    }

    [Fact]
    public void Map_ChordWithMixedDurations_ReportsTheDurationMistake()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(E4, 0)];
        PerformedNote[] performed = [Played(C4, 0), Played(E4, 0, holdMilliseconds: 300)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchHoldAndRhythm);

        NoteReadingPromptResult result = Assert.Single(outcome.PromptResults);
        Assert.Equal(Verdict.TooShort, result.DurationVerdict);
        Assert.False(result.IsFirstTryCorrect);
    }

    [Fact]
    public void Map_DurationNotGraded_VerdictCountsDoNotReportTooShortOrTooLong()
    {
        ScoreEvent[] events = [Event(C4, 0), Event(D4, 1)];
        PerformedNote[] performed = [Played(C4, 0, holdMilliseconds: 200), Played(D4, 1000, holdMilliseconds: 5000)];

        PlayAlongOutcome notGraded = Map(events, performed, NoteReadingMode.PitchAndRhythm);
        PlayAlongOutcome graded = Map(events, performed, NoteReadingMode.PitchHoldAndRhythm);

        Assert.Equal(2, notGraded.VerdictCounts[Verdict.Correct]);
        Assert.Equal(0, notGraded.VerdictCounts[Verdict.TooShort]);
        Assert.Equal(0, notGraded.VerdictCounts[Verdict.TooLong]);
        Assert.Equal(0, graded.VerdictCounts[Verdict.Correct]);
        Assert.Equal(1, graded.VerdictCounts[Verdict.TooShort]);
        Assert.Equal(1, graded.VerdictCounts[Verdict.TooLong]);
    }

    [Fact]
    public void Map_UnmatchedNotes_AreCountedAsExtraAtSessionLevel()
    {
        ScoreEvent[] events = [Event(C4, 0)];
        PerformedNote[] performed = [Played(C4, 0), Played(D4, 600), Played(E4, 650)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndRhythm);

        Assert.Single(outcome.PromptResults);
        Assert.Equal(2, outcome.ExtraNoteCount);
        Assert.Equal(2, outcome.VerdictCounts[Verdict.Extra]);
    }

    [Fact]
    public void Map_ModeWithoutOnsetOrDuration_LeavesTimingOutcomesNull()
    {
        ScoreEvent[] events = [Event(C4, 0)];
        PerformedNote[] performed = [Played(C4, 120)];

        PlayAlongOutcome outcome = Map(events, performed, NoteReadingMode.PitchAndOrder);

        NoteReadingPromptResult result = Assert.Single(outcome.PromptResults);
        Assert.Null(result.OnsetVerdict);
        Assert.Null(result.OnsetDeviation);
        Assert.Null(result.DurationVerdict);
    }

    [Fact]
    public void Map_NoExpectedEvents_ReturnsNoPrompts()
    {
        PlayAlongOutcome outcome = Map([], [], NoteReadingMode.PitchAndRhythm);

        Assert.Empty(outcome.PromptResults);
    }

    [Fact]
    public void Map_UndefinedMode_Throws()
    {
        GradingResult result = Grader.Grade([], Tempo, [], Anchor, Anchor);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayAlongResultMapper.Map(result, (NoteReadingMode)99, Tempo, Anchor, Options));
    }

    /// <summary>
    /// The two engines must agree on what a performance means. The same synthetic performances go through the
    /// pitch-gated <see cref="NoteReadingSession"/> and through <see cref="Grader"/> plus the mapper; where both
    /// define an outcome (pitch always, onset once a key was accepted) they must report the same one.
    /// </summary>
    [Theory]
    [InlineData(0, true, Verdict.Correct)]
    [InlineData(40, true, Verdict.Correct)]
    [InlineData(120, true, Verdict.Late)]
    [InlineData(-120, true, Verdict.Early)]
    public void Engines_EquivalentSingleNotePerformance_AgreeOnPitchAndOnset(
        int onsetOffsetMilliseconds,
        bool expectedPitchCorrect,
        Verdict expectedOnset)
    {
        ScoreNote note = Note(C4, beatOffset: 0);
        Score score = CreateScore([note]);
        ScoreEvent[] events = [.. ScoreDerivation.Flatten(score)];
        TimeSpan pressed = Anchor + TimeSpan.FromMilliseconds(onsetOffsetMilliseconds);

        var session = new NoteReadingSession();
        session.Reset(score, NoteReadingMode.PitchAndRhythm, Options.OnTimeTolerance, Anchor);
        session.Check(C4, pressed);
        PlayAlongOutcome playAlong = Map(
            events,
            [Played(C4, onsetOffsetMilliseconds)],
            NoteReadingMode.PitchAndRhythm);

        NoteReadingPromptResult fromSession = Assert.Single(session.PromptResults);
        NoteReadingPromptResult fromPlayAlong = Assert.Single(playAlong.PromptResults);
        Assert.Equal(expectedPitchCorrect, fromSession.IsPitchFirstTryCorrect);
        Assert.Equal(fromSession.IsPitchFirstTryCorrect, fromPlayAlong.IsPitchFirstTryCorrect);
        Assert.Equal(expectedOnset, fromSession.OnsetVerdict);
        Assert.Equal(fromSession.OnsetVerdict, fromPlayAlong.OnsetVerdict);
        Assert.Equal(fromSession.OnsetDeviation, fromPlayAlong.OnsetDeviation);
        Assert.Equal(fromSession.IsFirstTryCorrect, fromPlayAlong.IsFirstTryCorrect);
    }

    [Fact]
    public void Engines_WrongKeyNeverCorrected_AgreeThatThePitchWasMissed()
    {
        ScoreNote note = Note(C4, beatOffset: 0);
        Score score = CreateScore([note]);
        ScoreEvent[] events = [.. ScoreDerivation.Flatten(score)];

        var session = new NoteReadingSession();
        session.Reset(score, NoteReadingMode.PitchAndRhythm, Options.OnTimeTolerance, Anchor);
        session.Check(E4, Anchor);
        PlayAlongOutcome playAlong = Map(events, [Played(E4, 0)], NoteReadingMode.PitchAndRhythm);

        Assert.False(Assert.Single(session.PromptResults).IsPitchFirstTryCorrect);
        Assert.False(Assert.Single(playAlong.PromptResults).IsPitchFirstTryCorrect);
    }

    [Fact]
    public void Engines_ChordPlayedTogetherOnTime_AgreeOnPromptsAndPitches()
    {
        ScoreNote lower = Note(C4, beatOffset: 0);
        ScoreNote upper = Note(E4, beatOffset: 0);
        ScoreNote next = Note(D4, beatOffset: 1);
        Score score = CreateScore([lower, upper, next]);
        ScoreEvent[] events = [.. ScoreDerivation.Flatten(score)];

        var session = new NoteReadingSession();
        session.Reset(score, NoteReadingMode.PitchAndRhythm, Options.OnTimeTolerance, Anchor);
        session.Check(C4, Anchor);
        session.Check(E4, Anchor);
        session.Check(D4, Anchor + TimeSpan.FromSeconds(1));
        PlayAlongOutcome playAlong = Map(
            events,
            [Played(C4, 0), Played(E4, 0), Played(D4, 1000)],
            NoteReadingMode.PitchAndRhythm);

        Assert.Equal(session.PromptResults.Count, playAlong.PromptResults.Count);
        for (int index = 0; index < session.PromptResults.Count; index++)
        {
            Assert.Equal(
                session.PromptResults[index].ExpectedPitches.ToArray(),
                playAlong.PromptResults[index].ExpectedPitches.ToArray());
            Assert.Equal(session.PromptResults[index].IsFirstTryCorrect, playAlong.PromptResults[index].IsFirstTryCorrect);
            Assert.Equal(session.PromptResults[index].OnsetVerdict, playAlong.PromptResults[index].OnsetVerdict);
        }
    }

    [Fact]
    public void Map_TiedPairsInASyncopatedExercise_AreOnePromptEachAndAPerfectRunIsClean()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.PitchHoldAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Syncopated),
            new Random(4));
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(score);
        Assert.Contains(events, scoreEvent => scoreEvent.SourceNotes.Count == 2);
        PerformedNote[] performed = events
            .Select(scoreEvent => new PerformedNote
            {
                Pitch = scoreEvent.Pitch,
                StartTime = Anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, score.Tempo),
                ReleaseTime = Anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats + scoreEvent.DurationBeats, score.Tempo),
            })
            .ToArray();
        var options = new GradingOptions();
        GradingResult graded = Grader.Grade(
            events,
            score.Tempo,
            performed,
            Anchor,
            Anchor + TimeSpan.FromMinutes(5),
            options);

        PlayAlongOutcome outcome = PlayAlongResultMapper.Map(
            graded,
            NoteReadingMode.PitchHoldAndRhythm,
            score.Tempo,
            Anchor,
            options);

        Assert.Equal(events.Count, outcome.PromptResults.Count);
        Assert.All(outcome.PromptResults, result => Assert.True(result.IsFirstTryCorrect));
        Assert.Contains(outcome.PromptResults, result => result.ExpectedSourceNotes.Length == 2);
        Assert.Equal(0, outcome.ExtraNoteCount);
    }

    private static PlayAlongOutcome Map(
        IReadOnlyList<ScoreEvent> events,
        IReadOnlyList<PerformedNote> performed,
        NoteReadingMode mode,
        bool ignorePitch = false)
    {
        var options = new GradingOptions { IgnorePitch = ignorePitch };
        GradingResult result = Grader.Grade(events, Tempo, performed, Anchor, Anchor + TimeSpan.FromSeconds(60), options);
        return PlayAlongResultMapper.Map(result, mode, Tempo, Anchor, options);
    }

    private static ScoreEvent Event(Pitch pitch, double onsetBeats) =>
        new(pitch, onsetBeats, 1, Staff.Treble, [Note(pitch, onsetBeats)]);

    private static ScoreNote Note(Pitch pitch, double beatOffset) =>
        new(pitch, new NoteValue(4), 0, beatOffset, Staff.Treble);

    private static PerformedNote Played(Pitch pitch, int onsetOffsetMilliseconds, int holdMilliseconds = 1000)
    {
        TimeSpan start = Anchor + TimeSpan.FromMilliseconds(onsetOffsetMilliseconds);
        return new PerformedNote
        {
            Pitch = pitch,
            StartTime = start,
            ReleaseTime = start + TimeSpan.FromMilliseconds(holdMilliseconds),
        };
    }

    private static Score CreateScore(IReadOnlyList<ScoreNote> notes) =>
        new("test", new TimeSignature(4, new NoteValue(4)), Tempo, 0, [new ScoreMeasure(notes, [])]);
}
