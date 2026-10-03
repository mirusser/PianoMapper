using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class LevelProgressionTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_NoHistory_RecommendsLevelOneAndNothingIsPassed()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(SightReadingHistory.Empty);

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(LevelStatus.Recommended, report.Levels[0].Status);
        Assert.All(report.Levels.Skip(1), level => Assert.Equal(LevelStatus.NotTried, level.Status));
        Assert.False(report.AllLevelsPassed);
    }

    [Fact]
    public void Evaluate_ThreePassingMatchingSessions_PassesTheLevelAndRecommendsTheNext()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8),
            Session(2, pitchCorrect: 8),
            Session(3, pitchCorrect: 8)));

        Assert.Equal(LevelStatus.Passed, report.Levels[0].Status);
        Assert.Equal(2, report.Recommended.Level.Number);
        Assert.Equal(3, report.Levels[0].WindowSessionCount);
        Assert.Equal(100, report.Levels[0].AveragePitchPercent);
    }

    [Fact]
    public void Evaluate_TwoPassingSessionsOnly_KeepsTheLevelRecommended()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8),
            Session(2, pitchCorrect: 8)));

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(LevelStatus.Recommended, report.Levels[0].Status);
    }

    [Fact]
    public void Evaluate_AllThreeLatestSessionsMustPass_NotJustAnyThree()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8),
            Session(2, pitchCorrect: 8),
            Session(3, pitchCorrect: 6),
            Session(4, pitchCorrect: 8),
            Session(5, pitchCorrect: 8)));

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(LevelStatus.Recommended, report.Levels[0].Status);
        Assert.Equal(3, report.Levels[0].WindowSessionCount);
    }

    [Fact]
    public void Evaluate_PassThreshold_IsNinetyPercentInclusive()
    {
        // 9 of 10 prompts is exactly 90%.
        LevelProgressionReport atThreshold = LevelProgression.Evaluate(History(
            Session(1, promptCount: 10, pitchCorrect: 9),
            Session(2, promptCount: 10, pitchCorrect: 9),
            Session(3, promptCount: 10, pitchCorrect: 9)));
        LevelProgressionReport below = LevelProgression.Evaluate(History(
            Session(1, promptCount: 16, pitchCorrect: 14),
            Session(2, promptCount: 16, pitchCorrect: 14),
            Session(3, promptCount: 16, pitchCorrect: 14)));

        Assert.Equal(LevelStatus.Passed, atThreshold.Levels[0].Status);
        Assert.NotEqual(LevelStatus.Passed, below.Levels[0].Status);
    }

    [Fact]
    public void Evaluate_SessionsOfOtherLevels_AreIgnored()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8, preset: SightReadingPresetId.OneOctave),
            Session(2, pitchCorrect: 8, staff: Staff.Bass),
            Session(3, pitchCorrect: 8, isGrandStaff: true),
            Session(4, pitchCorrect: 8, mode: NoteReadingMode.PitchAndHold),
            Session(5, pitchCorrect: 8, rhythmPreset: "Basic")));

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(0, report.Levels[0].WindowSessionCount);
        Assert.Null(report.Levels[0].AveragePitchPercent);
        Assert.Equal(LevelStatus.Recommended, report.Levels[0].Status);
    }

    [Theory]
    [InlineData("Melodic")]
    [InlineData("Intervallic")]
    [InlineData("Random")]
    [InlineData(null)]
    public void Evaluate_SessionsOfAnyMotion_CountTowardTheLevelOfTheirRange(string? motion)
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8) with { Motion = motion },
            Session(2, pitchCorrect: 8) with { Motion = motion },
            Session(3, pitchCorrect: 8) with { Motion = motion }));

        Assert.Equal(LevelStatus.Passed, report.Levels[0].Status);
        Assert.Equal(2, report.Recommended.Level.Number);
    }

    [Fact]
    public void Evaluate_OutOfOrderPractice_RecommendsTheFirstUnpassedLevelAndStillMarksTheOtherPassed()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8, staff: Staff.Bass),
            Session(2, pitchCorrect: 8, staff: Staff.Bass),
            Session(3, pitchCorrect: 8, staff: Staff.Bass)));

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(LevelStatus.Passed, report.Levels[1].Status);
        Assert.Equal(LevelStatus.Recommended, report.Levels[0].Status);
    }

    [Fact]
    public void Evaluate_GrandStaffLevel_MatchesOnlyGrandStaffSessions()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8, isGrandStaff: true),
            Session(2, pitchCorrect: 8, isGrandStaff: true),
            Session(3, pitchCorrect: 8, isGrandStaff: true)));

        ExerciseLevel grand = ExerciseLevelCatalog.Levels.Single(level => level.IsGrandStaff && level.PresetId == SightReadingPresetId.FiveNote);
        Assert.Equal(LevelStatus.Passed, report.Levels.Single(level => level.Level == grand).Status);
        Assert.Equal(1, report.Recommended.Level.Number);
    }

    [Fact]
    public void Evaluate_LegacyEntriesWithoutRhythmOrLayout_AreIgnoredBecauseTheyCannotBeMatched()
    {
        LevelProgressionReport report = LevelProgression.Evaluate(History(
            Session(1, pitchCorrect: 8) with { SchemaVersion = 1, RhythmPreset = null, IsGrandStaff = null, PitchFirstTryCorrectCount = null },
            Session(2, pitchCorrect: 8) with { SchemaVersion = 1, RhythmPreset = null, IsGrandStaff = null, PitchFirstTryCorrectCount = null },
            Session(3, pitchCorrect: 8) with { SchemaVersion = 1, RhythmPreset = null, IsGrandStaff = null, PitchFirstTryCorrectCount = null }));

        Assert.Equal(1, report.Recommended.Level.Number);
        Assert.Equal(0, report.Levels[0].WindowSessionCount);
    }

    [Fact]
    public void Evaluate_TimedLevel_NeedsEightyPercentTimingCleanAsWellAsPitch()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);
        SightReadingSessionSummary[] cleanPitchSloppyTiming = Enumerable.Range(1, 3)
            .Select(index => TimedSession(timed, index, pitchCorrect: 10, timingMistakes: 3, promptCount: 10, tempo: 60))
            .ToArray();
        SightReadingSessionSummary[] clean = Enumerable.Range(1, 3)
            .Select(index => TimedSession(timed, index, pitchCorrect: 10, timingMistakes: 2, promptCount: 10, tempo: 60))
            .ToArray();

        LevelProgress sloppy = LevelProgression.Evaluate(History(cleanPitchSloppyTiming)).Levels.Single(level => level.Level == timed);
        LevelProgress good = LevelProgression.Evaluate(History(clean)).Levels.Single(level => level.Level == timed);

        Assert.NotEqual(LevelStatus.Passed, sloppy.Status);
        Assert.Equal(LevelStatus.Passed, good.Status);
        Assert.Equal(80, good.AverageTimingCleanPercent);
    }

    [Fact]
    public void Evaluate_RhythmOnlyLevel_TreatsAnyNonCleanPromptAsATimingMiss()
    {
        ExerciseLevel rhythmOnly = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.RhythmOnly);
        // Four skipped prompts of ten are not timing "mistakes" in the counts, but they are not clean either.
        SightReadingSessionSummary[] skipping = Enumerable.Range(1, 3)
            .Select(index => TimedSession(rhythmOnly, index, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: 60, firstTryCorrect: 6))
            .ToArray();

        LevelProgress progress = LevelProgression.Evaluate(History(skipping)).Levels.Single(level => level.Level == rhythmOnly);

        Assert.NotEqual(LevelStatus.Passed, progress.Status);
        Assert.Equal(60, progress.AverageTimingCleanPercent);
    }

    [Fact]
    public void Evaluate_AllLevelsPassed_KeepsRecommendingTheLastLevel()
    {
        var sessions = new List<SightReadingSessionSummary>();
        int index = 0;
        foreach (ExerciseLevel level in ExerciseLevelCatalog.Levels)
        {
            for (int repeat = 0; repeat < 3; repeat++)
            {
                sessions.Add(TimedSession(level, index++, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: level.StartTempoPulsesPerMinute));
            }
        }

        LevelProgressionReport report = LevelProgression.Evaluate(History([.. sessions]));

        Assert.True(report.AllLevelsPassed);
        Assert.Equal(ExerciseLevelCatalog.Levels[^1], report.Recommended.Level);
        Assert.All(report.Levels, level => Assert.Equal(LevelStatus.Passed, level.Status));
    }

    [Fact]
    public void Tempo_UntimedLevel_HasNoRecommendedTempo()
    {
        Assert.Null(LevelProgression.Evaluate(SightReadingHistory.Empty).Recommended.TempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_TimedLevelWithoutSessions_StartsAtTheLevelsStartTempo()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.StartTempoPulsesPerMinute is not null);

        LevelProgress progress = LevelProgression.Evaluate(SightReadingHistory.Empty).Levels.Single(level => level.Level == timed);

        Assert.Equal(timed.StartTempoPulsesPerMinute, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_AfterAPassingSession_StepsUpByFive()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: 60))).Levels.Single(level => level.Level == timed);

        Assert.Equal(65, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_StepUp_IsCappedAtTheLevelsMaximum()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);
        int maximum = timed.MaximumTempoPulsesPerMinute!.Value;

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: maximum - 2))).Levels.Single(level => level.Level == timed);

        Assert.Equal(maximum, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_TwoSessionsBelowSixtyPercent_StepsDownByFiveFromTheLatest()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: 60),
            TimedSession(timed, 2, pitchCorrect: 5, timingMistakes: 4, promptCount: 10, tempo: 70, firstTryCorrect: 4),
            TimedSession(timed, 3, pitchCorrect: 5, timingMistakes: 4, promptCount: 10, tempo: 70, firstTryCorrect: 5))).Levels.Single(level => level.Level == timed);

        Assert.Equal(65, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_OnlyOneSessionBelowSixtyPercent_DoesNotStepDown()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 10, timingMistakes: 0, promptCount: 10, tempo: 60),
            TimedSession(timed, 2, pitchCorrect: 5, timingMistakes: 4, promptCount: 10, tempo: 70, firstTryCorrect: 4))).Levels.Single(level => level.Level == timed);

        Assert.Equal(65, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_StepDown_NeverGoesBelowTheMinimumTempo()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 3, timingMistakes: 6, promptCount: 10, tempo: 32, firstTryCorrect: 2),
            TimedSession(timed, 2, pitchCorrect: 3, timingMistakes: 6, promptCount: 10, tempo: 32, firstTryCorrect: 2))).Levels.Single(level => level.Level == timed);

        Assert.Equal(SightReadingExerciseOptions.MinimumTempoPulsesPerMinute, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Tempo_StrugglingWithoutAnyPassOnRecord_StaysNearTheLatestTempoInsteadOfJumpingUp()
    {
        ExerciseLevel timed = ExerciseLevelCatalog.Levels.First(level => level.Mode == NoteReadingMode.PitchAndRhythm);

        LevelProgress progress = LevelProgression.Evaluate(History(
            TimedSession(timed, 1, pitchCorrect: 7, timingMistakes: 2, promptCount: 10, tempo: 80, firstTryCorrect: 7))).Levels.Single(level => level.Level == timed);

        Assert.Equal(80, progress.RecommendedTempoPulsesPerMinute);
    }

    [Fact]
    public void Catalog_LevelsAreNumberedFromOneInOrderWithDistinctNamesAndOptions()
    {
        IReadOnlyList<ExerciseLevel> levels = ExerciseLevelCatalog.Levels;

        Assert.Equal(Enumerable.Range(1, levels.Count), levels.Select(level => level.Number));
        Assert.Equal(levels.Count, levels.Select(level => level.Name).Distinct().Count());
        Assert.Equal(levels.Count, levels.Select(level => level.CreateOptions()).Distinct().Count());
        Assert.Equal(NoteReadingMode.PitchAndOrder, levels[0].Mode);
        Assert.Equal(SightReadingPresetId.FiveNote, levels[0].PresetId);
        Assert.Equal(Staff.Treble, levels[0].Staff);
    }

    [Fact]
    public void Catalog_EveryLevelComposesThroughTheComposerAtItsStartAndMaximumTempo()
    {
        foreach (ExerciseLevel level in ExerciseLevelCatalog.Levels)
        {
            foreach (int? tempo in new[] { level.StartTempoPulsesPerMinute, level.MaximumTempoPulsesPerMinute })
            {
                Score score = SightReadingExerciseComposer.Compose(level.CreateOptions(tempo), new Random(level.Number));

                Assert.NotEmpty(score.Measures.SelectMany(measure => measure.Notes));
            }
        }
    }

    [Fact]
    public void Catalog_TimedLevelsHaveAStartTempoWithinRangeAndNotAboveTheirMaximum()
    {
        foreach (ExerciseLevel level in ExerciseLevelCatalog.Levels)
        {
            bool graded = level.Mode.GetGradedAxes() != GradedAxes.Pitch && level.Mode != NoteReadingMode.Off;
            Assert.Equal(graded, level.StartTempoPulsesPerMinute is not null);
            Assert.Equal(graded, level.MaximumTempoPulsesPerMinute is not null);
            if (level.StartTempoPulsesPerMinute is { } start && level.MaximumTempoPulsesPerMinute is { } maximum)
            {
                Assert.InRange(start, SightReadingExerciseOptions.MinimumTempoPulsesPerMinute, maximum);
                Assert.True(maximum <= SightReadingExerciseOptions.MaximumTempoPulsesPerMinute);
            }
        }
    }

    [Fact]
    public void Catalog_FollowsTheBeginnerLadder()
    {
        string[] expectedFirstFive =
        [
            "FiveNote/Treble", "FiveNote/Bass", "FiveNote/Grand", "OneOctave/Treble", "OneOctave/Bass",
        ];

        Assert.Equal(
            expectedFirstFive,
            ExerciseLevelCatalog.Levels.Take(5)
                .Select(level => $"{level.PresetId}/{(level.IsGrandStaff ? "Grand" : level.Staff.ToString())}"));
        Assert.All(ExerciseLevelCatalog.Levels.Take(5), level => Assert.Equal(NoteReadingMode.PitchAndOrder, level.Mode));
        Assert.Equal(NoteReadingMode.RhythmOnly, ExerciseLevelCatalog.Levels[5].Mode);
        Assert.Equal(SightReadingRhythmPreset.Fixed, ExerciseLevelCatalog.Levels[5].RhythmPreset);
        Assert.Equal(60, ExerciseLevelCatalog.Levels[5].StartTempoPulsesPerMinute);
        Assert.Equal(SightReadingRhythmPreset.Basic, ExerciseLevelCatalog.Levels[6].RhythmPreset);
        Assert.Equal(
            [SightReadingPresetId.GMajor, SightReadingPresetId.FMajor, SightReadingPresetId.LedgerLines, SightReadingPresetId.Chords],
            ExerciseLevelCatalog.Levels.Skip(8).Take(4).Select(level => level.PresetId));
        // Level 15 closes the original beginner ladder; levels for presets shipped later are appended after it.
        Assert.Equal(NoteReadingMode.PitchHoldAndRhythm, ExerciseLevelCatalog.Levels[14].Mode);
        Assert.Equal(70, ExerciseLevelCatalog.Levels[14].StartTempoPulsesPerMinute);
    }

    [Fact]
    public void Catalog_AppendsOneUntimedTrebleLevelPerRangePresetShippedAfterTheOriginalLadder()
    {
        Assert.Equal(
            [
                SightReadingPresetId.DMajor,
                SightReadingPresetId.BFlatMajor,
                SightReadingPresetId.AMinor,
                SightReadingPresetId.Accidentals,
            ],
            ExerciseLevelCatalog.Levels.Skip(15).Take(4).Select(level => level.PresetId));
        Assert.All(
            ExerciseLevelCatalog.Levels.Skip(15).Take(4),
            level =>
            {
                Assert.Equal(Staff.Treble, level.Staff);
                Assert.Equal(NoteReadingMode.PitchAndOrder, level.Mode);
                Assert.False(level.IsTimed);
            });
    }

    [Fact]
    public void PassRule_ConstantsLiveInOneRecordWithTheDocumentedDefaults()
    {
        LevelProgressionRules rules = LevelProgressionRules.Default;

        Assert.Equal(3, rules.RequiredSessions);
        Assert.Equal(90, rules.MinimumPitchFirstTryPercent);
        Assert.Equal(80, rules.MinimumTimingCleanPercent);
        Assert.Equal(5, rules.TempoStepPulses);
        Assert.Equal(60, rules.StruggleBelowPercent);
        Assert.Equal(2, rules.StruggleSessionCount);
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
        int promptCount = 8,
        int pitchCorrect = 8,
        SightReadingPresetId preset = SightReadingPresetId.FiveNote,
        Staff staff = Staff.Treble,
        bool isGrandStaff = false,
        NoteReadingMode mode = NoteReadingMode.PitchAndOrder,
        string rhythmPreset = "Fixed") =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            Start.AddHours(index),
            preset.ToString(),
            staff,
            mode,
            promptCount,
            TimeSpan.FromSeconds(30),
            pitchCorrect,
            promptCount - pitchCorrect,
            [])
        {
            RhythmPreset = rhythmPreset,
            IsGrandStaff = isGrandStaff,
            PitchFirstTryCorrectCount = pitchCorrect,
            TimingMistakeCount = 0,
        };

    private static SightReadingSessionSummary TimedSession(
        ExerciseLevel level,
        int index,
        int pitchCorrect,
        int timingMistakes,
        int promptCount,
        int? tempo,
        int? firstTryCorrect = null) =>
        Session(
            index,
            promptCount,
            pitchCorrect,
            level.PresetId,
            level.Staff,
            level.IsGrandStaff,
            level.Mode,
            level.RhythmPreset.ToString()) with
        {
            TempoBeatsPerMinute = tempo,
            TimingMistakeCount = timingMistakes,
            FirstTryCorrectCount = firstTryCorrect ?? (promptCount - timingMistakes),
        };
}
