using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingInsightsTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly Pitch G3 = new(NoteLetter.G, 0, 3);
    private static readonly Pitch A3 = new(NoteLetter.A, 0, 3);
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_EmptyHistory_HasNothingToSayAndNoEnoughDataFlag()
    {
        SightReadingInsightReport report = SightReadingInsights.Build(SightReadingHistory.Empty);

        Assert.False(report.HasEnoughData);
        Assert.Empty(report.WeakSpots);
        Assert.Empty(report.Habits);
        Assert.Null(report.Speed);
        Assert.Empty(report.Trend);
        Assert.Equal(TrendDirection.NotEnoughData, report.PitchTrend);
    }

    [Fact]
    public void Build_WeakSpots_AreTheWeakNotesByPitchAndStaffWorstFirstAndCapped()
    {
        SightReadingHistory history = History(
            Session(0, attempts:
            [
                new NoteAttemptSummary(C4, Staff.Bass, 10, 2, 0, null),
                new NoteAttemptSummary(C4, Staff.Treble, 10, 10, 0, null),
                new NoteAttemptSummary(D4, Staff.Treble, 10, 7, 0, null),
                new NoteAttemptSummary(E4, Staff.Treble, 10, 9, 0, null),
            ]));

        SightReadingInsightReport report = SightReadingInsights.Build(history);

        // C4 on bass (20% right) and D4 (70%) are weak; E4 at 90% is below the weakness threshold; treble C4 is perfect.
        Assert.Equal([(C4, Staff.Bass), (D4, Staff.Treble)], report.WeakSpots.Select(spot => (spot.Pitch, spot.Staff)));
        Assert.True(report.HasEnoughData);
    }

    [Fact]
    public void Build_NotesBelowTheAttemptThreshold_AreNeverWeakSpots()
    {
        SightReadingHistory history = History(
            Session(0, attempts: [new NoteAttemptSummary(C4, Staff.Treble, SightReadingHistory.MinimumMasteryAttempts - 1, 0, 0, null)]));

        Assert.Empty(SightReadingInsights.Build(history).WeakSpots);
    }

    [Fact]
    public void Build_WeakSpots_AreLimitedToTheMaximum()
    {
        NoteAttemptSummary[] attempts = Enum.GetValues<NoteLetter>()
            .SelectMany(letter => new[] { 3, 4 }.Select(octave => new Pitch(letter, 0, octave)))
            .Select(pitch => new NoteAttemptSummary(pitch, Staff.Treble, 10, 0, 0, null))
            .ToArray();

        SightReadingInsightReport report = SightReadingInsights.Build(History(Session(0, attempts: attempts)));

        Assert.Equal(SightReadingInsights.MaximumWeakSpots, report.WeakSpots.Count);
    }

    [Fact]
    public void Build_Habits_GroupSimilarConfusionsAcrossDifferentNotesAndSessionsPerStaff()
    {
        // "A step higher than written" on the bass staff happens for three different notes across two sessions.
        SightReadingHistory history = History(
            Session(0, confusions:
            [
                new ConfusionSummary(C4, D4, Staff.Bass, 2),
                new ConfusionSummary(G3, A3, Staff.Bass, 1),
                new ConfusionSummary(C4, E4, Staff.Treble, 1),
            ]),
            Session(1, confusions: [new ConfusionSummary(A3, new Pitch(NoteLetter.B, 0, 3), Staff.Bass, 1)]));

        ConfusionHabit habit = Assert.Single(SightReadingInsights.Build(history).Habits);

        Assert.Equal(Staff.Bass, habit.Staff);
        Assert.Equal(PitchDifferenceKind.Interval, habit.Difference.Kind);
        Assert.True(habit.Difference.IsHigher);
        Assert.Equal(1, habit.Difference.DiatonicSteps);
        Assert.Equal(4, habit.Count);
    }

    [Fact]
    public void Build_RareConfusions_AreNotHabits()
    {
        SightReadingHistory history = History(
            Session(0, confusions:
            [
                new ConfusionSummary(C4, D4, Staff.Treble, SightReadingInsights.MinimumHabitCount - 1),
            ]));

        Assert.Empty(SightReadingInsights.Build(history).Habits);
    }

    [Fact]
    public void Build_Habits_AreTheMostFrequentFirstAndCapped()
    {
        SightReadingHistory history = History(
            Session(0, confusions:
            [
                new ConfusionSummary(C4, D4, Staff.Treble, 4),
                new ConfusionSummary(C4, E4, Staff.Treble, 9),
                new ConfusionSummary(D4, C4, Staff.Treble, 6),
                new ConfusionSummary(E4, G3, Staff.Treble, 5),
                new ConfusionSummary(C4, new Pitch(NoteLetter.C, 0, 5), Staff.Treble, 3),
            ]));

        IReadOnlyList<ConfusionHabit> habits = SightReadingInsights.Build(history).Habits;

        Assert.Equal(SightReadingInsights.MaximumHabits, habits.Count);
        Assert.Equal([9, 6, 5], habits.Select(habit => habit.Count));
    }

    [Fact]
    public void Build_Speed_UsesOnlySelfPacedSessionsAndReportsMedianAndNotesPerMinute()
    {
        SightReadingHistory history = History(
            // Two self-paced sessions: 8 notes in 16 s (2 s each) and 8 notes in 32 s (4 s each).
            Session(0, attempts: [new NoteAttemptSummary(C4, Staff.Treble, 8, 8, 8, 16_000)]),
            Session(1, attempts: [new NoteAttemptSummary(D4, Staff.Treble, 8, 8, 8, 32_000)]),
            // A clock-paced session has no response times and must not influence speed.
            Session(2, attempts: [new NoteAttemptSummary(E4, Staff.Treble, 8, 8, 0, null)]));

        SpeedInsight speed = Assert.IsType<SpeedInsight>(SightReadingInsights.Build(history).Speed);

        Assert.Equal(TimeSpan.FromSeconds(3), speed.MedianResponseTime);
        Assert.Equal(2, speed.SessionCount);
        // 16 timed notes in 48 s of response time: 20 notes a minute.
        Assert.Equal(20, speed.NotesPerMinute, precision: 6);
    }

    [Fact]
    public void Build_NoSelfPacedSessions_HasNoSpeed()
    {
        SightReadingHistory history = History(Session(0, attempts: [new NoteAttemptSummary(C4, Staff.Treble, 8, 8, 0, null)]));

        Assert.Null(SightReadingInsights.Build(history).Speed);
    }

    [Fact]
    public void Build_Trend_IsOldestFirstWithPitchAccuracyAndTheTimingCleanRateOfTimedSessions()
    {
        SightReadingHistory history = History(
            Session(0, pitchCorrect: 6, firstTryCorrect: 6),
            Session(1, mode: NoteReadingMode.PitchAndRhythm, pitchCorrect: 8, firstTryCorrect: 5, timingMistakes: 3));

        IReadOnlyList<TrendPoint> trend = SightReadingInsights.Build(history).Trend;

        Assert.Equal([Start, Start.AddHours(1)], trend.Select(point => point.CompletedAt));
        Assert.Equal([75.0, 100.0], trend.Select(point => point.PitchAccuracyPercent));
        Assert.Null(trend[0].TimingCleanPercent);
        Assert.Equal(62.5, trend[1].TimingCleanPercent);
    }

    [Fact]
    public void Build_Trend_RhythmOnlySessionWithUnplayedNotes_CountsThemAgainstTheTimingCleanRate()
    {
        // Four of ten notes were never played: not timing mistakes in the stored counts, but not clean either.
        SightReadingHistory history = History(
            Session(0, mode: NoteReadingMode.RhythmOnly, promptCount: 10, pitchCorrect: 10, firstTryCorrect: 6, timingMistakes: 0));

        TrendPoint point = Assert.Single(SightReadingInsights.Build(history).Trend);

        Assert.Equal(60, point.TimingCleanPercent);
    }

    [Theory]
    [InlineData(NoteReadingMode.RhythmOnly, "Fixed")]
    [InlineData(NoteReadingMode.PitchAndRhythm, "Basic")]
    public void Build_Trend_TimingCleanRate_IsTheOneTheGuidedPathUsesForTheSameSession(NoteReadingMode mode, string rhythmPreset)
    {
        SightReadingHistory history = History(
            Session(0, mode: mode, promptCount: 10, pitchCorrect: 10, firstTryCorrect: 6, timingMistakes: 3) with
            {
                RhythmPreset = rhythmPreset,
                IsGrandStaff = false,
            });

        TrendPoint point = Assert.Single(SightReadingInsights.Build(history).Trend);
        LevelProgress level = Assert.Single(
            LevelProgression.Evaluate(history).Levels,
            progress => progress.WindowSessionCount > 0);

        Assert.NotNull(point.TimingCleanPercent);
        Assert.Equal(level.AverageTimingCleanPercent, point.TimingCleanPercent);
    }

    [Fact]
    public void Build_Trend_LegacyTimedSessionsWithFusedOutcomesAreLeftOut()
    {
        SightReadingHistory history = History(
            Session(0, pitchCorrect: 6, firstTryCorrect: 6) with { SchemaVersion = 1, PitchFirstTryCorrectCount = null },
            Session(1, mode: NoteReadingMode.PitchAndHold, pitchCorrect: 3, firstTryCorrect: 3) with
            {
                SchemaVersion = 1,
                PitchFirstTryCorrectCount = null,
            });

        Assert.Single(SightReadingInsights.Build(history).Trend);
    }

    [Theory]
    [InlineData(new[] { 50, 55, 80, 90 }, TrendDirection.Improving)]
    [InlineData(new[] { 90, 85, 60, 55 }, TrendDirection.Declining)]
    [InlineData(new[] { 80, 82, 79, 81 }, TrendDirection.Steady)]
    [InlineData(new[] { 80, 82, 79 }, TrendDirection.NotEnoughData)]
    public void Build_PitchTrend_ComparesTheNewerHalfWithTheOlderHalf(int[] percentages, TrendDirection expected)
    {
        SightReadingSessionSummary[] sessions = percentages
            .Select((percent, index) => Session(index, promptCount: 100, pitchCorrect: percent, firstTryCorrect: percent))
            .ToArray();

        Assert.Equal(expected, SightReadingInsights.Build(History(sessions)).PitchTrend);
    }

    [Fact]
    public void Build_Trend_OnlyCoversTheMostRecentSessions()
    {
        SightReadingSessionSummary[] sessions = Enumerable.Range(0, 15)
            .Select(index => Session(index, pitchCorrect: 4, firstTryCorrect: 4))
            .ToArray();

        IReadOnlyList<TrendPoint> trend = SightReadingInsights.Build(History(sessions)).Trend;

        Assert.Equal(SightReadingInsights.TrendSessionCount, trend.Count);
        Assert.Equal(Start.AddHours(5), trend[0].CompletedAt);
    }

    [Fact]
    public void DescribeHabit_NamesTheStaffAndTheDirectionInPlainWords()
    {
        var bassStep = new ConfusionHabit(Staff.Bass, PitchDistance.Measure(C4, D4), 4);
        var trebleThirdLower = new ConfusionHabit(Staff.Treble, PitchDistance.Measure(E4, C4), 3);

        string bass = SightReadingInsightText.DescribeHabit(bassStep);
        string treble = SightReadingInsightText.DescribeHabit(trebleThirdLower);

        Assert.Contains("bass", bass);
        Assert.Contains("a step higher", bass);
        Assert.Contains("treble", treble);
        Assert.Contains("a third lower", treble);
    }

    private static SightReadingHistory History(params SightReadingSessionSummary[] sessions)
    {
        SightReadingHistory history = SightReadingHistory.Empty;
        foreach (SightReadingSessionSummary session in sessions)
        {
            history = history.WithCompletedSession(session);
        }

        return history;
    }

    private static SightReadingSessionSummary Session(
        int index,
        NoteReadingMode mode = NoteReadingMode.PitchAndOrder,
        int promptCount = 8,
        int pitchCorrect = 8,
        int firstTryCorrect = 8,
        int timingMistakes = 0,
        IReadOnlyList<NoteAttemptSummary>? attempts = null,
        IReadOnlyList<ConfusionSummary>? confusions = null) =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Guid.NewGuid(),
            Start.AddHours(index),
            "FiveNote",
            Staff.Treble,
            mode,
            promptCount,
            TimeSpan.FromSeconds(30),
            firstTryCorrect,
            promptCount - firstTryCorrect,
            [])
        {
            PitchFirstTryCorrectCount = pitchCorrect,
            TimingMistakeCount = timingMistakes,
            NoteAttempts = attempts ?? [],
            Confusions = confusions ?? [],
        };
}
