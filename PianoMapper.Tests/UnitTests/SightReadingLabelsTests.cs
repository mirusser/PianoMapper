using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingLabelsTests
{
    [Fact]
    public void Preset_EveryMember_HasDistinctNonBlankLabelAndOption()
    {
        SightReadingPresetId[] presets = Enum.GetValues<SightReadingPresetId>();

        string[] labels = presets.Select(SightReadingLabels.Preset).ToArray();
        string[] options = presets.Select(SightReadingLabels.PresetOption).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal(options.Length, options.Distinct().Count());
        Assert.All(presets, preset => Assert.StartsWith(SightReadingLabels.Preset(preset), SightReadingLabels.PresetOption(preset)));
    }

    [Fact]
    public void Mode_EveryMember_HasDistinctNonBlankLabelAndOption()
    {
        NoteReadingMode[] modes = Enum.GetValues<NoteReadingMode>();

        string[] labels = modes.Select(SightReadingLabels.Mode).ToArray();
        string[] options = modes.Select(SightReadingLabels.ModeOption).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal(options.Length, options.Distinct().Count());
        Assert.All(modes, mode => Assert.StartsWith(SightReadingLabels.Mode(mode), SightReadingLabels.ModeOption(mode)));
    }

    [Fact]
    public void RhythmPreset_EveryMember_HasDistinctNonBlankLabelAndOption()
    {
        SightReadingRhythmPreset[] presets = Enum.GetValues<SightReadingRhythmPreset>();

        string[] labels = presets.Select(SightReadingLabels.RhythmPreset).ToArray();
        string[] options = presets.Select(SightReadingLabels.RhythmPresetOption).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal(options.Length, options.Distinct().Count());
        Assert.All(
            presets,
            preset => Assert.StartsWith(SightReadingLabels.RhythmPreset(preset), SightReadingLabels.RhythmPresetOption(preset)));
    }

    [Fact]
    public void Motion_EveryMember_HasDistinctNonBlankLabelAndOption()
    {
        SightReadingMotion[] motions = Enum.GetValues<SightReadingMotion>();

        string[] labels = motions.Select(SightReadingLabels.Motion).ToArray();
        string[] options = motions.Select(SightReadingLabels.MotionOption).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal(options.Length, options.Distinct().Count());
        Assert.All(motions, motion => Assert.StartsWith(SightReadingLabels.Motion(motion), SightReadingLabels.MotionOption(motion)));
    }

    [Fact]
    public void Motion_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingLabels.Motion((SightReadingMotion)99));
    }

    [Fact]
    public void IntervalSize_EverySupportedSize_HasADistinctNonBlankLabel()
    {
        string[] labels = Enumerable
            .Range(SightReadingExerciseOptions.MinimumIntervalSteps, SightReadingExerciseOptions.MaximumIntervalSteps)
            .Select(SightReadingLabels.IntervalSize)
            .ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void IntervalSize_UnsupportedSize_Throws(int steps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingLabels.IntervalSize(steps));
    }

    [Fact]
    public void DescribeSession_NonRandomMotion_NamesTheMotion()
    {
        SightReadingSessionSummary melodic = CreateSummary() with { Motion = "Melodic" };
        SightReadingSessionSummary random = CreateSummary() with { Motion = "Random" };
        SightReadingSessionSummary untracked = CreateSummary();

        Assert.Contains(SightReadingLabels.Motion(SightReadingMotion.Melodic), SightReadingLabels.DescribeSession(melodic));
        Assert.DoesNotContain(SightReadingLabels.Motion(SightReadingMotion.Random), SightReadingLabels.DescribeSession(random));
        Assert.Equal(SightReadingLabels.DescribeSession(untracked), SightReadingLabels.DescribeSession(random));
    }

    [Fact]
    public void DescribeSession_UnrecognizedMotion_FallsBackToTheStoredName()
    {
        SightReadingSessionSummary entry = CreateSummary() with { Motion = "FutureMotion" };

        Assert.Contains("FutureMotion", SightReadingLabels.DescribeSession(entry));
    }

    [Fact]
    public void LadderModes_ListTheFiveSelectableModesInLearningOrder()
    {
        Assert.Equal(
            [
                NoteReadingMode.PitchAndOrder,
                NoteReadingMode.RhythmOnly,
                NoteReadingMode.PitchAndRhythm,
                NoteReadingMode.PitchAndHold,
                NoteReadingMode.PitchHoldAndRhythm,
            ],
            SightReadingLabels.LadderModes);
    }

    [Fact]
    public void ModeDescription_EveryLadderMode_HasADistinctDescription()
    {
        string[] descriptions = SightReadingLabels.LadderModes.Select(SightReadingLabels.ModeDescription).ToArray();

        Assert.All(descriptions, description => Assert.False(string.IsNullOrWhiteSpace(description)));
        Assert.Equal(descriptions.Length, descriptions.Distinct().Count());
    }

    [Fact]
    public void ModeDescription_RhythmOnly_TellsTheLearnerToTapOnAnyKey()
    {
        Assert.Contains("any key", SightReadingLabels.ModeDescription(NoteReadingMode.RhythmOnly));
    }

    [Fact]
    public void Mode_FullGradingMode_IsNamedForAllThreeSkills()
    {
        Assert.Equal("Pitch + hold + rhythm", SightReadingLabels.Mode(NoteReadingMode.PitchHoldAndRhythm));
    }

    [Fact]
    public void Verdict_EveryMember_HasADistinctNonBlankLabel()
    {
        string[] labels = Enum.GetValues<Verdict>().Select(SightReadingLabels.Verdict).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
    }

    [Theory]
    [InlineData(Verdict.Correct, "Correct")]
    [InlineData(Verdict.WrongPitch, "Wrong pitch")]
    [InlineData(Verdict.Early, "Early")]
    [InlineData(Verdict.Late, "Late")]
    [InlineData(Verdict.TooShort, "Too short")]
    [InlineData(Verdict.TooLong, "Too long")]
    [InlineData(Verdict.Missed, "Missed")]
    [InlineData(Verdict.Extra, "Extra")]
    public void Verdict_PracticePanelWording_IsKeptExactly(Verdict verdict, string expected)
    {
        Assert.Equal(expected, SightReadingLabels.Verdict(verdict));
    }

    [Fact]
    public void Verdict_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingLabels.Verdict((Verdict)99));
    }

    [Fact]
    public void DescribeVerdictCounts_ListsOnlyNonZeroCountsInVerdictOrder()
    {
        var counts = new Dictionary<Verdict, int>
        {
            [Verdict.Missed] = 1,
            [Verdict.Correct] = 6,
            [Verdict.Late] = 1,
            [Verdict.Extra] = 0,
            [Verdict.TooLong] = 2,
        };

        Assert.Equal(
            "Correct 6 · Late 1 · Too long 2 · Missed 1",
            SightReadingLabels.DescribeVerdictCounts(counts));
    }

    [Fact]
    public void DescribeVerdictCounts_AllZero_IsEmpty()
    {
        Assert.Equal(
            string.Empty,
            SightReadingLabels.DescribeVerdictCounts(new Dictionary<Verdict, int> { [Verdict.Correct] = 0 }));
    }

    [Theory]
    [InlineData(LevelStatus.NotTried)]
    [InlineData(LevelStatus.InProgress)]
    [InlineData(LevelStatus.Recommended)]
    [InlineData(LevelStatus.Passed)]
    public void LevelStatus_EveryMember_HasALabelThatNeverSaysLocked(LevelStatus status)
    {
        string label = SightReadingLabels.LevelStatus(status);

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.DoesNotContain("lock", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LevelStatus_EveryDefinedMember_IsCovered()
    {
        foreach (LevelStatus status in Enum.GetValues<LevelStatus>())
        {
            _ = SightReadingLabels.LevelStatus(status);
        }
    }

    [Fact]
    public void DescribeLevelProgress_PassedLevel_NamesTheLevelThePercentAndTheSessions()
    {
        ExerciseLevel level = ExerciseLevelCatalog.Levels[0];
        var progress = new LevelProgress(level, LevelStatus.Passed, 3, 94.4, null, null);

        string text = SightReadingLabels.DescribeLevelProgress(progress);

        Assert.Contains(level.Name, text);
        Assert.Contains("94%", text);
        Assert.Contains("3 sessions", text);
    }

    [Fact]
    public void DescribeLevelProgress_RecommendedTimedLevel_MentionsTheRecommendedTempoInItsUnit()
    {
        ExerciseLevel simple = ExerciseLevelCatalog.Levels.First(level => level.RhythmPreset == SightReadingRhythmPreset.Basic);
        ExerciseLevel compound = ExerciseLevelCatalog.Levels.First(level => level.RhythmPreset == SightReadingRhythmPreset.Compound);

        string simpleText = SightReadingLabels.DescribeLevelProgress(
            new LevelProgress(simple, LevelStatus.Recommended, 0, null, null, 65));
        string compoundText = SightReadingLabels.DescribeLevelProgress(
            new LevelProgress(compound, LevelStatus.Recommended, 0, null, null, 45));

        Assert.Contains("♩ = 65", simpleText);
        Assert.Contains("♩. = 45", compoundText);
    }

    [Fact]
    public void DescribeLevelProgress_NeverTriedAndInProgress_AreReadableWithoutNumbers()
    {
        ExerciseLevel level = ExerciseLevelCatalog.Levels[0];

        string notTried = SightReadingLabels.DescribeLevelProgress(new LevelProgress(level, LevelStatus.NotTried, 0, null, null, null));
        string inProgress = SightReadingLabels.DescribeLevelProgress(new LevelProgress(level, LevelStatus.InProgress, 2, 72, null, null));

        Assert.False(string.IsNullOrWhiteSpace(notTried));
        Assert.Contains("72%", inProgress);
    }

    [Fact]
    public void Pacing_EveryMember_HasDistinctNonBlankLabelAndOption()
    {
        ExercisePacing[] pacings = Enum.GetValues<ExercisePacing>();

        string[] labels = pacings.Select(SightReadingLabels.Pacing).ToArray();
        string[] options = pacings.Select(SightReadingLabels.PacingOption).ToArray();

        Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal(options.Length, options.Distinct().Count());
        Assert.All(pacings, pacing => Assert.StartsWith(SightReadingLabels.Pacing(pacing), SightReadingLabels.PacingOption(pacing)));
    }

    [Fact]
    public void DescribeSession_PlayAlongEntry_NamesThePacing()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            Mode = NoteReadingMode.PitchAndRhythm,
            Pacing = SightReadingExerciseCoordinator.PlayAlongPacingName,
        };

        Assert.Contains("Play along", SightReadingLabels.DescribeSession(summary));
    }

    [Fact]
    public void DescribeSession_WaitForMeEntry_DoesNotMentionPacing()
    {
        SightReadingSessionSummary summary = CreateSummary() with { Mode = NoteReadingMode.PitchAndRhythm };

        Assert.DoesNotContain("Play along", SightReadingLabels.DescribeSession(summary));
        Assert.DoesNotContain("Wait for me", SightReadingLabels.DescribeSession(summary));
    }

    [Theory]
    [InlineData(Staff.Treble, "Treble")]
    [InlineData(Staff.Bass, "Bass")]
    public void StaffName_EachStaff_IsReadable(Staff staff, string expected)
    {
        Assert.Equal(expected, SightReadingLabels.StaffName(staff));
    }

    [Fact]
    public void IsTimingGraded_UndefinedMode_IsFalseInsteadOfThrowing()
    {
        Assert.False(SightReadingLabels.IsTimingGraded((NoteReadingMode)999));
    }

    [Fact]
    public void Preset_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingLabels.Preset((SightReadingPresetId)999));
    }

    [Theory]
    [InlineData(NoteReadingMode.Off, false)]
    [InlineData(NoteReadingMode.PitchAndOrder, false)]
    [InlineData(NoteReadingMode.PitchAndHold, true)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, true)]
    [InlineData(NoteReadingMode.PitchAndRhythm, true)]
    [InlineData(NoteReadingMode.RhythmOnly, true)]
    public void IsTimingGraded_EachMode_MatchesWhetherTimingIsJudged(NoteReadingMode mode, bool expected)
    {
        Assert.Equal(expected, SightReadingLabels.IsTimingGraded(mode));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Fixed, 60, "♩ = 60")]
    [InlineData(SightReadingRhythmPreset.Basic, 90, "♩ = 90")]
    [InlineData(SightReadingRhythmPreset.Compound, 40, "♩. = 40")]
    public void Tempo_UsesTheRhythmPresetsPulseUnit(SightReadingRhythmPreset rhythmPreset, int pulses, string expected)
    {
        Assert.Equal(expected, SightReadingLabels.Tempo(pulses, rhythmPreset));
    }

    [Fact]
    public void DescribeSession_CurrentPitchOnlyEntry_ListsPresetStaffAndMode()
    {
        SightReadingSessionSummary summary = CreateSummary() with { RhythmPreset = "Fixed", IsGrandStaff = false };

        Assert.Equal("Five notes · Treble · Pitch only", SightReadingLabels.DescribeSession(summary));
    }

    [Fact]
    public void DescribeSession_RhythmEntryWithTempo_ListsRhythmPresetAndTempo()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            Mode = NoteReadingMode.PitchHoldAndRhythm,
            RhythmPreset = "Basic",
            IsGrandStaff = false,
            TempoBeatsPerMinute = 60,
        };

        string description = SightReadingLabels.DescribeSession(summary);

        Assert.Contains("Pitch + hold + rhythm", description);
        Assert.Contains("Basic 4/4", description);
        Assert.EndsWith("♩ = 60", description);
    }

    [Fact]
    public void DescribeSession_RhythmOnlyEntry_OmitsThePitchRangeItNeverUsed()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            Mode = NoteReadingMode.RhythmOnly,
            TempoBeatsPerMinute = 80,
        };

        Assert.Equal("Treble · Rhythm only · ♩ = 80", SightReadingLabels.DescribeSession(summary));
    }

    [Fact]
    public void DescribeSession_GrandStaffEntry_NamesTheGrandStaffInsteadOfTheStoredStaff()
    {
        SightReadingSessionSummary summary = CreateSummary() with { IsGrandStaff = true };

        string description = SightReadingLabels.DescribeSession(summary);

        Assert.Contains("Grand staff", description);
        Assert.DoesNotContain("Treble", description);
    }

    [Fact]
    public void DescribeSession_LegacyEntry_MarksItAsOlderSessionWithoutFailing()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            SchemaVersion = SightReadingSessionSummary.LegacySchemaVersion,
        };

        string description = SightReadingLabels.DescribeSession(summary);

        Assert.StartsWith("Five notes · Treble · Pitch only", description);
        Assert.EndsWith("older session", description);
    }

    [Fact]
    public void DescribeSession_UnrecognizedPresetOrRhythm_FallsBackToTheStoredNames()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            PresetId = "FutureRange",
            RhythmPreset = "FutureRhythm",
        };

        string description = SightReadingLabels.DescribeSession(summary);

        Assert.Contains("FutureRange", description);
        Assert.Contains("FutureRhythm", description);
    }

    [Fact]
    public void DescribeTimingBreakdown_PitchOnlyEntry_ReturnsNull()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            PitchFirstTryCorrectCount = 7,
            TimingMistakeCount = 0,
        };

        Assert.Null(SightReadingLabels.DescribeTimingBreakdown(summary));
    }

    [Fact]
    public void DescribeTimingBreakdown_LegacyTimedEntry_ReturnsNull()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            SchemaVersion = SightReadingSessionSummary.LegacySchemaVersion,
            Mode = NoteReadingMode.PitchAndHold,
        };

        Assert.Null(SightReadingLabels.DescribeTimingBreakdown(summary));
    }

    [Fact]
    public void DescribeTimingBreakdown_TimedEntry_ReportsPitchAndTimingSeparately()
    {
        SightReadingSessionSummary summary = CreateSummary() with
        {
            Mode = NoteReadingMode.PitchHoldAndRhythm,
            PitchFirstTryCorrectCount = 7,
            TimingMistakeCount = 3,
        };

        Assert.Equal(
            "pitch 7 of 8 first-try · 3 timing mistake(s)",
            SightReadingLabels.DescribeTimingBreakdown(summary));
    }

    private static SightReadingSessionSummary CreateSummary() =>
        new(
            SightReadingSessionSummary.CurrentSchemaVersion,
            new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero),
            "FiveNote",
            Staff.Treble,
            NoteReadingMode.PitchAndOrder,
            8,
            TimeSpan.FromSeconds(20),
            6,
            2,
            []);
}
