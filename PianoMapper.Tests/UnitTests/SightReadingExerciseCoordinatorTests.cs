using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingExerciseCoordinatorTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(60);

    [Fact]
    public void Constructor_NullSession_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SightReadingExerciseCoordinator(null!));
    }

    [Fact]
    public void Phase_Initially_IsInactive()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Equal(SightReadingExercisePhase.Inactive, coordinator.Phase);
        Assert.False(coordinator.IsActive);
        Assert.Null(coordinator.Score);
    }

    [Fact]
    public void PromptCountOption_Initially_IsSixtyFour()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Equal(64, coordinator.PromptCountOption);
    }

    [Fact]
    public void RevealNoteNamesWhileActive_Initially_IsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.RevealNoteNamesWhileActive);
    }

    [Fact]
    public void RevealFingeringWhileActive_Initially_IsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.RevealFingeringWhileActive);
    }

    [Fact]
    public void RevealKeysWhileActive_Initially_IsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.RevealKeysWhileActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRevealKeysWhileActive_UpdatesTheProperty(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.SetRevealKeysWhileActive(value);

        Assert.Equal(value, coordinator.RevealKeysWhileActive);
    }

    [Fact]
    public void SetRevealKeysWhileActive_PersistsAcrossGenerateRetryAndEnd()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRevealKeysWhileActive(true);

        coordinator.Generate(new Random(1), Tolerance);
        Assert.True(coordinator.RevealKeysWhileActive);

        coordinator.Retry(Tolerance);
        Assert.True(coordinator.RevealKeysWhileActive);

        coordinator.End();
        Assert.True(coordinator.RevealKeysWhileActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRevealNoteNamesWhileActive_UpdatesTheProperty(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.SetRevealNoteNamesWhileActive(value);

        Assert.Equal(value, coordinator.RevealNoteNamesWhileActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRevealFingeringWhileActive_UpdatesTheProperty(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.SetRevealFingeringWhileActive(value);

        Assert.Equal(value, coordinator.RevealFingeringWhileActive);
    }

    [Fact]
    public void SetRevealNoteNamesWhileActive_PersistsAcrossGenerate()
    {
        // Like Staff/PresetId/Mode, this is a durable exercise setting, not per-exercise transient state — it
        // should survive Generate exactly the way those settings already do, so a learner doesn't have to
        // re-enable it every time they start a new exercise.
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRevealNoteNamesWhileActive(true);

        coordinator.Generate(new Random(1), Tolerance);

        Assert.True(coordinator.RevealNoteNamesWhileActive);
    }

    [Fact]
    public void SetRevealFingeringWhileActive_PersistsAcrossGenerate()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRevealFingeringWhileActive(true);

        coordinator.Generate(new Random(1), Tolerance);

        Assert.True(coordinator.RevealFingeringWhileActive);
    }

    [Fact]
    public void Generate_ComposesScoreAndResetsSessionToActivePhase()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetStaff(Staff.Treble);
        coordinator.SetPresetId(SightReadingPresetId.FiveNote);
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(42), Tolerance);

        Assert.NotNull(coordinator.Score);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.True(coordinator.IsActive);
        Assert.Equal(8, coordinator.Session.PromptCount);
        Assert.Equal(0, coordinator.Session.CompletedPromptCount);
    }

    [Fact]
    public void Generate_WithMastery_FavorsWeakPitchOverNeutralGeneration()
    {
        var neutralCoordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var weightedCoordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        neutralCoordinator.SetMotion(SightReadingMotion.Random);
        weightedCoordinator.SetMotion(SightReadingMotion.Random);
        neutralCoordinator.SetPromptCountOption(40);
        weightedCoordinator.SetPromptCountOption(40);
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var mastery = new[] { WeakNote(weakPitch, Staff.Treble) };

        neutralCoordinator.Generate(new Random(11), Tolerance);
        weightedCoordinator.Generate(new Random(11), Tolerance, mastery);

        int neutralCount = CountOccurrences(neutralCoordinator.Score!, weakPitch);
        int weightedCount = CountOccurrences(weightedCoordinator.Score!, weakPitch);
        Assert.True(
            weightedCount > neutralCount + 5,
            $"Expected the weighted G4 count ({weightedCount}) to be measurably higher than the neutral count " +
            $"({neutralCount}).");
    }

    [Fact]
    public void Generate_WithEmptyMastery_ProducesTheSameScoreAsOmittingMastery()
    {
        var coordinatorWithoutMastery = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var coordinatorWithEmptyMastery = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinatorWithoutMastery.SetPromptCountOption(24);
        coordinatorWithEmptyMastery.SetPromptCountOption(24);

        coordinatorWithoutMastery.Generate(new Random(29), Tolerance);
        coordinatorWithEmptyMastery.Generate(new Random(29), Tolerance, []);

        Assert.Equal(
            coordinatorWithoutMastery.Score!.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            coordinatorWithEmptyMastery.Score!.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void Generate_CalledAgainWithDifferentMastery_ReflectsTheLatestMasteryRatherThanCachingIt()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMotion(SightReadingMotion.Random);
        coordinator.SetPromptCountOption(40);
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var mastery = new[] { WeakNote(weakPitch, Staff.Treble) };

        coordinator.Generate(new Random(11), Tolerance);
        int neutralCount = CountOccurrences(coordinator.Score!, weakPitch);

        coordinator.Generate(new Random(11), Tolerance, mastery);
        int weightedCount = CountOccurrences(coordinator.Score!, weakPitch);

        Assert.True(
            weightedCount > neutralCount + 5,
            $"Expected the second Generate call (with mastery) to weight G4 more heavily than the first " +
            $"(without mastery) on the same coordinator instance: neutral={neutralCount}, weighted={weightedCount}.");
    }

    private static ScoreFingeringProfile CreateNarrowProfile() =>
        ScoreFingeringProfile.CreateWithThumbToLittleFingerReach(new FingeringReach(0, 0), new FingeringReach(0, 0));

    private static NoteMastery WeakNote(Pitch pitch, Staff? staff, double weakness = 1.0) =>
        new(pitch, staff, AttemptCount: 10, CorrectFirstTryCount: 0, MedianResponseTime: null, WeaknessScore: weakness);

    [Fact]
    public void Generate_StaffAgnosticMastery_WeightsTheNoteOnBothStaves()
    {
        var neutral = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var weighted = new SightReadingExerciseCoordinator(new NoteReadingSession());
        foreach (SightReadingExerciseCoordinator coordinator in new[] { neutral, weighted })
        {
            coordinator.SetStaff(Staff.Bass);
            coordinator.SetMotion(SightReadingMotion.Random);
            coordinator.SetPromptCountOption(40);
        }

        var weakPitch = new Pitch(NoteLetter.G, 0, 3);
        neutral.Generate(new Random(11), Tolerance);
        weighted.Generate(new Random(11), Tolerance, [WeakNote(weakPitch, staff: null)]);

        Assert.True(CountOccurrences(weighted.Score!, weakPitch) > CountOccurrences(neutral.Score!, weakPitch) + 5);
    }

    [Fact]
    public void Generate_MasteryForTheOtherStaff_DoesNotBoostThisStaffsNote()
    {
        var neutral = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var weighted = new SightReadingExerciseCoordinator(new NoteReadingSession());
        foreach (SightReadingExerciseCoordinator coordinator in new[] { neutral, weighted })
        {
            coordinator.SetStaff(Staff.Treble);
            coordinator.SetPromptCountOption(40);
        }

        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        neutral.Generate(new Random(11), Tolerance);
        weighted.Generate(new Random(11), Tolerance, [WeakNote(weakPitch, Staff.Bass)]);

        Assert.Equal(CountOccurrences(neutral.Score!, weakPitch), CountOccurrences(weighted.Score!, weakPitch));
    }

    [Fact]
    public void GenerateWeaknessDrill_ServesTheWeakNoteMoreThanOrdinaryGenerationAtTheSameLength()
    {
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        NoteMastery[] mastery = [WeakNote(weakPitch, Staff.Treble)];
        int ordinaryTotal = 0;
        int drillTotal = 0;
        for (int seed = 0; seed < 60; seed++)
        {
            var ordinary = new SightReadingExerciseCoordinator(new NoteReadingSession());
            var drill = new SightReadingExerciseCoordinator(new NoteReadingSession());
            ordinary.SetMotion(SightReadingMotion.Random);
            drill.SetMotion(SightReadingMotion.Random);
            ordinary.SetPromptCountOption(8);
            drill.SetPromptCountOption(8);
            ordinary.Generate(new Random(seed), Tolerance, mastery);
            drill.GenerateWeaknessDrill(new Random(seed), Tolerance, mastery);
            ordinaryTotal += CountOccurrences(ordinary.Score!, weakPitch);
            drillTotal += CountOccurrences(drill.Score!, weakPitch);
        }

        Assert.True(drillTotal > ordinaryTotal, $"drill {drillTotal} vs ordinary {ordinaryTotal} weak notes over 60 seeds");
    }

    [Fact]
    public void GetDrillAvailability_WeakNoteInTheCurrentRange_IsAvailable()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMotion(SightReadingMotion.Random);

        DrillAvailability availability = coordinator.GetDrillAvailability(
            [WeakNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)]);

        Assert.True(availability.IsAvailable);
        Assert.Null(availability.Reason);
    }

    [Theory]
    [InlineData(NoteLetter.A, 6, Staff.Treble)]
    [InlineData(NoteLetter.G, 4, Staff.Bass)]
    public void GetDrillAvailability_WeakNotesOutsideTheRangeOrOnTheOtherStaff_IsUnavailableWithAReason(
        NoteLetter letter,
        int octave,
        Staff staff)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetStaff(Staff.Treble);

        DrillAvailability availability = coordinator.GetDrillAvailability([WeakNote(new Pitch(letter, 0, octave), staff)]);

        Assert.False(availability.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(availability.Reason));
    }

    [Fact]
    public void GetDrillAvailability_StaffLessWeakNote_CountsOnEitherStaff()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetStaff(Staff.Bass);
        coordinator.SetMotion(SightReadingMotion.Random);

        Assert.True(coordinator.GetDrillAvailability([WeakNote(new Pitch(NoteLetter.E, 0, 3), staff: null)]).IsAvailable);
    }

    [Fact]
    public void GetDrillAvailability_GrandStaff_AcceptsAWeakNoteOnEitherStavesRange()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetMotion(SightReadingMotion.Random);

        Assert.True(coordinator.GetDrillAvailability([WeakNote(new Pitch(NoteLetter.E, 0, 3), Staff.Bass)]).IsAvailable);
        Assert.True(coordinator.GetDrillAvailability([WeakNote(new Pitch(NoteLetter.E, 0, 4), Staff.Treble)]).IsAvailable);
    }

    [Fact]
    public void GetDrillAvailability_NothingWeak_SaysSo()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        DrillAvailability availability = coordinator.GetDrillAvailability([]);

        Assert.False(availability.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(availability.Reason));
    }

    [Fact]
    public void GetDrillAvailability_ChordsAndRhythmOnly_CannotBeDrilledNoteByNote()
    {
        var chords = new SightReadingExerciseCoordinator(new NoteReadingSession());
        chords.SetPresetId(SightReadingPresetId.Chords);
        var rhythmOnly = new SightReadingExerciseCoordinator(new NoteReadingSession());
        rhythmOnly.SetMode(NoteReadingMode.RhythmOnly);
        NoteMastery[] weak = [WeakNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)];

        Assert.False(chords.GetDrillAvailability(weak).IsAvailable);
        Assert.False(rhythmOnly.GetDrillAvailability(weak).IsAvailable);
    }

    [Fact]
    public void GenerateRecommended_TimedLevel_AppliesEveryOptionAndGeneratesAtTheRecommendedTempo()
    {
        ExerciseLevel level = ExerciseLevelCatalog.Levels.First(candidate => candidate.Mode == NoteReadingMode.PitchAndRhythm);
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.GenerateRecommended(new Random(3), Tolerance, new LevelRecommendation(level, 65));

        Assert.Equal(level.Staff, coordinator.Staff);
        Assert.Equal(level.IsGrandStaff, coordinator.IsGrandStaff);
        Assert.Equal(level.PresetId, coordinator.PresetId);
        Assert.Equal(level.Mode, coordinator.Mode);
        Assert.Equal(level.RhythmPreset, coordinator.RhythmPreset);
        Assert.Equal(level.PromptCount, coordinator.PromptCountOption);
        Assert.Equal(65, coordinator.TempoPulsesPerMinute);
        Assert.Equal(65, coordinator.Score!.Tempo.BeatsPerMinute);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
    }

    [Fact]
    public void GenerateRecommended_GrandStaffLevel_GeneratesAGrandStaffExercise()
    {
        ExerciseLevel level = ExerciseLevelCatalog.Levels.First(candidate => candidate.IsGrandStaff);
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.GenerateRecommended(new Random(3), Tolerance, new LevelRecommendation(level, null));

        Assert.True(coordinator.IsGrandStaff);
        Assert.Contains(coordinator.Score!.Measures.SelectMany(measure => measure.Notes), note => note.Staff == Staff.Bass);
        Assert.Contains(coordinator.Score.Measures.SelectMany(measure => measure.Notes), note => note.Staff == Staff.Treble);
    }

    [Fact]
    public void GenerateRecommended_CompoundLevel_UsesDottedQuarterPulses()
    {
        ExerciseLevel level = ExerciseLevelCatalog.Levels.First(candidate => candidate.RhythmPreset == SightReadingRhythmPreset.Compound);
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.GenerateRecommended(new Random(3), Tolerance, new LevelRecommendation(level, 45));

        Assert.Equal(45, coordinator.TempoPulsesPerMinute);
        Assert.Equal(135, coordinator.Score!.Tempo.BeatsPerMinute);
        Assert.Equal(6, coordinator.Score.TimeSignature.Numerator);
    }

    [Fact]
    public void GenerateRecommended_UntimedLevel_LeavesTheLearnersChosenTempoAlone()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetTempoPulsesPerMinute(90);

        coordinator.GenerateRecommended(new Random(3), Tolerance, new LevelRecommendation(ExerciseLevelCatalog.Levels[0], null));

        Assert.Equal(90, coordinator.TempoPulsesPerMinute);
    }

    [Fact]
    public void GenerateRecommended_KeepsTheLearnersOtherSettingsAndTheyCanStillChangeEverything()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRevealNoteNamesWhileActive(true);
        coordinator.SetCoachHints(true);
        coordinator.SetClickWhilePlaying(false);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        coordinator.GenerateRecommended(new Random(3), Tolerance, new LevelRecommendation(ExerciseLevelCatalog.Levels[1], null));
        coordinator.SetStaff(Staff.Treble);
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetMode(NoteReadingMode.RhythmOnly);

        Assert.True(coordinator.RevealNoteNamesWhileActive);
        Assert.True(coordinator.CoachHints);
        Assert.False(coordinator.ClickWhilePlaying);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);
        Assert.Equal(NoteReadingMode.RhythmOnly, coordinator.Mode);
    }

    [Fact]
    public void GenerateRecommended_FollowsTheLadderRecommendationForTheHistory()
    {
        DateTimeOffset start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        SightReadingHistory history = SightReadingHistory.Empty;
        for (int index = 0; index < 3; index++)
        {
            history = history.WithCompletedSession(new SightReadingSessionSummary(
                SightReadingSessionSummary.CurrentSchemaVersion,
                start.AddHours(index),
                "FiveNote",
                Staff.Treble,
                NoteReadingMode.PitchAndOrder,
                8,
                TimeSpan.FromSeconds(20),
                8,
                0,
                [])
            {
                RhythmPreset = "Fixed",
                IsGrandStaff = false,
                PitchFirstTryCorrectCount = 8,
                TimingMistakeCount = 0,
            });
        }

        LevelRecommendation recommendation = LevelProgression.Evaluate(history).Recommended;
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.GenerateRecommended(new Random(3), Tolerance, recommendation, history.ComputeNoteMasteryWeakestFirst());

        Assert.Equal(2, recommendation.Level.Number);
        Assert.Equal(Staff.Bass, coordinator.Staff);
        Assert.Equal(recommendation.Level.PresetId, coordinator.PresetId);
        Assert.All(coordinator.Score!.Measures.SelectMany(measure => measure.Notes), note => Assert.Equal(Staff.Bass, note.Staff));
    }

    [Fact]
    public void GenerateWeaknessDrill_NextOrdinaryGenerateIsBackToCoverageFirst()
    {
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        NoteMastery[] mastery = [WeakNote(weakPitch, Staff.Treble)];
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        var reference = new SightReadingExerciseCoordinator(new NoteReadingSession());

        coordinator.GenerateWeaknessDrill(new Random(5), Tolerance, mastery);
        coordinator.Generate(new Random(21), Tolerance, mastery);
        reference.Generate(new Random(21), Tolerance, mastery);

        Assert.Equal(
            reference.Score!.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void GenerateWeaknessDrill_StaysInThePresetsRangeAndStartsAnActiveExercise()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.FiveNote);
        NoteMastery[] mastery =
        [
            WeakNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble),
            WeakNote(new Pitch(NoteLetter.A, 0, 6), Staff.Treble),
        ];

        coordinator.GenerateWeaknessDrill(new Random(5), Tolerance, mastery);

        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.All(
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(note.Pitch.DiatonicIndex, 28, 32));
    }

    private static int CountOccurrences(Score score, Pitch pitch) =>
        score.Measures.SelectMany(measure => measure.Notes).Count(note => note.Pitch == pitch);

    [Fact]
    public void SetStaff_ReflectedInNextGeneratedScore()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetStaff(Staff.Bass);

        coordinator.Generate(new Random(1), Tolerance);

        Assert.All(
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Equal(Staff.Bass, note.Staff));
    }

    [Fact]
    public void Retry_AfterMistakes_KeepsSameScoreButResetsProgress()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(3), Tolerance);
        Score generatedScore = coordinator.Score!;
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));

        bool didRetry = coordinator.Retry(Tolerance);

        Assert.True(didRetry);
        Assert.Same(generatedScore, coordinator.Score);
        Assert.Equal(0, coordinator.Session.WrongAttemptCount);
        Assert.Equal(0, coordinator.Session.CompletedPromptCount);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
    }

    [Fact]
    public void Retry_WithoutActiveExercise_ReturnsFalseAndStaysInactive()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        bool didRetry = coordinator.Retry(Tolerance);

        Assert.False(didRetry);
        Assert.Equal(SightReadingExercisePhase.Inactive, coordinator.Phase);
    }

    [Fact]
    public void End_AfterGenerate_ClearsScoreAndReturnsToInactivePhase()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(5), Tolerance);

        bool didEnd = coordinator.End();

        Assert.True(didEnd);
        Assert.Null(coordinator.Score);
        Assert.Equal(SightReadingExercisePhase.Inactive, coordinator.Phase);
        Assert.Equal(0, coordinator.Session.PromptCount);
    }

    [Fact]
    public void End_WithoutActiveExercise_ReturnsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        bool didEnd = coordinator.End();

        Assert.False(didEnd);
    }

    [Fact]
    public void Phase_AllPromptsCompleted_TransitionsToReview()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.True(coordinator.Session.IsComplete);
    }

    [Fact]
    public void Generate_ProducesScoreWithFingeringsOnEveryNote()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(9), Tolerance);

        Assert.All(
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes),
            note => Assert.NotNull(note.Fingering));
    }

    [Fact]
    public void Retry_DoesNotRegenerateFingerings()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(9), Tolerance);
        Score generatedScore = coordinator.Score!;

        coordinator.Retry(Tolerance);

        Assert.Same(generatedScore, coordinator.Score);
    }

    [Fact]
    public void BuildReviewFirstTryMap_AllPromptsCorrectFirstTry_MapsEveryNoteToTrue()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();

        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        IReadOnlyDictionary<ScoreNote, bool> reviewMap = coordinator.BuildReviewFirstTryMap();

        Assert.Equal(notes.Length, reviewMap.Count);
        Assert.All(reviewMap.Values, Assert.True);
    }

    [Fact]
    public void BuildReviewFirstTryMap_WrongThenCorrectPrompt_MapsThatNoteToFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        var wrongPitch = new Pitch(NoteLetter.A, 0, 6);

        coordinator.Session.Check(wrongPitch);
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        IReadOnlyDictionary<ScoreNote, bool> reviewMap = coordinator.BuildReviewFirstTryMap();

        Assert.False(reviewMap[notes[0]]);
        Assert.All(notes.Skip(1), note => Assert.True(reviewMap[note]));
    }

    [Fact]
    public void BuildReviewFirstTryMap_BeforeCompletion_OnlyIncludesAttemptedPrompts()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();

        coordinator.Session.Check(notes[0].Pitch);

        IReadOnlyDictionary<ScoreNote, bool> reviewMap = coordinator.BuildReviewFirstTryMap();

        Assert.Single(reviewMap);
        Assert.True(reviewMap[notes[0]]);
    }

    [Fact]
    public void BuildReviewMarks_WhileTheExerciseIsActive_IsNullSoNothingIsDrawnYet()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        Assert.Null(coordinator.BuildReviewMarks());

        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        coordinator.Session.Check(coordinator.Score!.Measures[0].Notes[0].Pitch);

        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.Null(coordinator.BuildReviewMarks());
    }

    [Fact]
    public void BuildReviewMarks_BeforeAnyExercise_IsNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Null(coordinator.BuildReviewMarks());
    }

    [Fact]
    public void BuildReviewMarks_WaitForMeRunWithAWrongKey_MarksThatNotePitchAndTheRestClean()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        IReadOnlyDictionary<ScoreNote, ReviewMark>? marks = coordinator.BuildReviewMarks();

        Assert.NotNull(marks);
        Assert.Equal(notes.Length, marks.Count);
        Assert.Equal(ReviewMark.Pitch, marks[notes[0]]);
        Assert.All(notes.Skip(1), note => Assert.Equal(ReviewMark.Clean, marks[note]));
    }

    [Fact]
    public void BuildReviewMarks_PlayAlongRunWithASkippedPrompt_MarksItMissedAndTheRestClean()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Null(coordinator.BuildReviewMarks());
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));

        IReadOnlyDictionary<ScoreNote, ReviewMark>? marks = coordinator.BuildReviewMarks();

        Assert.NotNull(marks);
        NoteReadingPromptResult skipped = outcome.PromptResults[3];
        Assert.All(skipped.ExpectedSourceNotes, note => Assert.Equal(ReviewMark.Missed, marks[note]));
        Assert.Equal(
            outcome.PromptResults.Count - 1,
            outcome.PromptResults.Count(result => result.ExpectedSourceNotes.All(note => marks[note] == ReviewMark.Clean)));
    }

    [Fact]
    public void BuildReviewMarks_AfterRetry_IsNullAgainBecauseTheRunIsActive()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 3), TimeSpan.FromSeconds(12));
        Assert.NotNull(coordinator.BuildReviewMarks());

        coordinator.Retry(Tolerance);

        Assert.Null(coordinator.BuildReviewMarks());
    }

    [Fact]
    public void HasMissedPrompts_BeforeCompletion_IsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        Assert.False(coordinator.HasMissedPrompts);
    }

    [Fact]
    public void HasMissedPrompts_AllCorrectFirstTry_IsFalseAfterCompletion()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.False(coordinator.HasMissedPrompts);
    }

    [Fact]
    public void HasMissedPrompts_SomeWrongThenCorrect_IsTrueAfterCompletion()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();

        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.HasMissedPrompts);
    }

    [Fact]
    public void RetryMissed_WithMissedPrompts_ComposesCompactedScoreAndResetsToActive()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Score fullExerciseScore = coordinator.Score!;
        bool didRetryMissed = coordinator.RetryMissed(Tolerance);

        Assert.True(didRetryMissed);
        Assert.NotSame(fullExerciseScore, coordinator.Score);
        ScoreNote missedNote = Assert.Single(coordinator.Score!.Measures.SelectMany(measure => measure.Notes));
        Assert.Equal(notes[0].Pitch, missedNote.Pitch);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.Equal(1, coordinator.Session.PromptCount);
    }

    [Fact]
    public void RetryMissed_ProducesScoreWithFingerings()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        coordinator.RetryMissed(Tolerance);

        Assert.All(
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes),
            note => Assert.NotNull(note.Fingering));
    }

    [Fact]
    public void RetryMissed_BeforeReviewPhase_ReturnsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        bool didRetryMissed = coordinator.RetryMissed(Tolerance);

        Assert.False(didRetryMissed);
    }

    [Fact]
    public void RetryMissed_AllCorrectFirstTry_ReturnsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        bool didRetryMissed = coordinator.RetryMissed(Tolerance);

        Assert.False(didRetryMissed);
    }

    [Fact]
    public void ConsumeCompletionSummary_BeforeCompletion_ReturnsNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        Assert.Null(coordinator.ConsumeCompletionSummary());
    }

    [Fact]
    public void ConsumeCompletionSummary_AfterCompletion_ReturnsSummaryMatchingSession()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.SetStaff(Staff.Bass);
        coordinator.SetPresetId(SightReadingPresetId.OneOctave);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        Assert.NotNull(summary);
        Assert.Equal(timeProvider.GetUtcNow(), summary.CompletedAt);
        Assert.Equal("OneOctave", summary.PresetId);
        Assert.Equal(Staff.Bass, summary.Staff);
        Assert.Equal(4, summary.PromptCount);
        Assert.Equal(4, summary.FirstTryCorrectCount);
    }

    [Fact]
    public void PitchAndTimingCounts_LatePitchCorrectPrompt_SplitsPitchFromTimingMistakes()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchHoldAndRhythm);
        coordinator.SetPromptCountOption(8);
        coordinator.Generate(new Random(11), Tolerance);
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(coordinator.Score!);
        TimeSpan start = TimeSpan.FromSeconds(10);
        for (int index = 0; index < events.Count; index++)
        {
            ScoreEvent scoreEvent = events[index];
            TimeSpan onset = start + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, coordinator.Score!.Tempo);
            TimeSpan lateBy = index == 1 ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero;
            coordinator.Session.Check(scoreEvent.Pitch, onset + lateBy);
            coordinator.Session.Release(
                scoreEvent.Pitch,
                onset + lateBy + MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, coordinator.Score.Tempo));
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Equal(events.Count, coordinator.PitchFirstTryCorrectCount);
        Assert.Equal(1, coordinator.TimingMistakeCount);
        Assert.Equal(events.Count - 1, coordinator.Session.FirstTryCorrectCount);
    }

    [Fact]
    public void PitchAndTimingCounts_BeforeAnyAttempt_AreZero()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(11), Tolerance);

        Assert.Equal(0, coordinator.PitchFirstTryCorrectCount);
        Assert.Equal(0, coordinator.TimingMistakeCount);
    }

    [Fact]
    public void ClickWhilePlaying_Initially_IsTrue()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.True(coordinator.ClickWhilePlaying);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetClickWhilePlaying_PersistsAcrossGenerateRetryRetryMissedAndEnd(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetClickWhilePlaying(value);
        coordinator.SetPromptCountOption(4);

        coordinator.Generate(new Random(11), Tolerance);
        Assert.Equal(value, coordinator.ClickWhilePlaying);
        coordinator.Retry(Tolerance);
        Assert.Equal(value, coordinator.ClickWhilePlaying);
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.RetryMissed(Tolerance));
        Assert.Equal(value, coordinator.ClickWhilePlaying);
        coordinator.End();
        Assert.Equal(value, coordinator.ClickWhilePlaying);
    }

    [Fact]
    public void ShouldClickSound_NoExercise_IsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.ShouldClickSound);
    }

    [Fact]
    public void ShouldClickSound_PitchOnlyMode_IsNeverTrue()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);
        coordinator.Generate(new Random(11), Tolerance);

        Assert.False(coordinator.ShouldClickSound);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldClickSound_PitchAndHold_FollowsTheSettingWhileActiveAndStopsAtReview(
        bool clickWhilePlaying,
        bool expectedWhileActive)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndHold);
        coordinator.SetClickWhilePlaying(clickWhilePlaying);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        Assert.Equal(expectedWhileActive, coordinator.ShouldClickSound);

        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(coordinator.Score!);
        foreach (ScoreEvent scoreEvent in events)
        {
            coordinator.Session.Check(scoreEvent.Pitch, TimeSpan.Zero);
            coordinator.Session.Release(scoreEvent.Pitch, TimeSpan.FromSeconds(1));
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.False(coordinator.ShouldClickSound);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldClickSound_RhythmMode_FollowsTheClickSetting(bool clickWhilePlaying, bool expected)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchHoldAndRhythm);
        coordinator.SetClickWhilePlaying(clickWhilePlaying);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.Generate(new Random(1), Tolerance);

        Assert.Equal(expected, coordinator.ShouldClickSound);

        coordinator.End();
        Assert.False(coordinator.ShouldClickSound);
    }

    [Fact]
    public void Generate_RhythmOnlyWithGrandStaffAndChordsSelected_IgnoresThemInsteadOfThrowing()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetIsGrandStaff(true);
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(2), Tolerance);

        Pitch[] pitches = coordinator.Score!.Measures.SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch)
            .Distinct()
            .ToArray();
        Assert.Equal(new Pitch(NoteLetter.B, 0, 4), Assert.Single(pitches));
        Assert.True(coordinator.IsPitchSetupIgnored);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder, false)]
    [InlineData(NoteReadingMode.PitchAndHold, false)]
    [InlineData(NoteReadingMode.PitchAndRhythm, false)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, false)]
    [InlineData(NoteReadingMode.RhythmOnly, true)]
    public void IsPitchSetupIgnored_OnlyRhythmOnlyHasNoPitchPalette(NoteReadingMode mode, bool expected)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);

        Assert.Equal(expected, coordinator.IsPitchSetupIgnored);
    }

    [Theory]
    [InlineData(true, SightReadingPresetId.FiveNote)]
    [InlineData(false, SightReadingPresetId.Chords)]
    public void IsRhythmPresetLocked_GrandStaffOrChords_FallsBackToFixedQuarterNotesOnPurpose(
        bool isGrandStaff,
        SightReadingPresetId presetId)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetIsGrandStaff(isGrandStaff);
        coordinator.SetPresetId(presetId);
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(2), Tolerance);

        Assert.True(coordinator.IsRhythmPresetLocked);
        Assert.Equal(SightReadingRhythmPreset.Fixed, coordinator.EffectiveRhythmPreset);
        Assert.Equal(SightReadingRhythmPreset.Basic, coordinator.RhythmPreset);
        Assert.All(
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Equal(new NoteValue(4), note.NoteValue));
    }

    [Fact]
    public void IsRhythmPresetLocked_PlainSingleStaffExercise_IsFalseAndKeepsTheChosenRhythm()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Compound);

        Assert.False(coordinator.IsRhythmPresetLocked);
        Assert.Equal(SightReadingRhythmPreset.Compound, coordinator.EffectiveRhythmPreset);
    }

    [Fact]
    public void IsRhythmPresetLocked_RhythmOnlyIgnoresGrandStaffAndChordsSoRhythmStaysChoosable()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetIsGrandStaff(true);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);

        Assert.False(coordinator.IsRhythmPresetLocked);
        Assert.Equal(SightReadingRhythmPreset.Basic, coordinator.EffectiveRhythmPreset);
    }

    [Fact]
    public void ConsumeCompletionSummary_LockedRhythmFallback_RecordsTheRhythmThatWasActuallyUsed()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetIsGrandStaff(true);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        Assert.NotNull(summary);
        Assert.Equal("Fixed", summary.RhythmPreset);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder, false)]
    [InlineData(NoteReadingMode.PitchAndHold, false)]
    [InlineData(NoteReadingMode.PitchAndRhythm, true)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, true)]
    [InlineData(NoteReadingMode.RhythmOnly, true)]
    public void IsOnsetGraded_OnlyOnsetGradedModes(NoteReadingMode mode, bool expected)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);

        Assert.Equal(expected, coordinator.IsOnsetGraded);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndRhythm)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm)]
    [InlineData(NoteReadingMode.RhythmOnly)]
    public void Generate_OnsetGradedWaitForMe_HasNoClockUntilTheFirstKeyAnchorsBeatOne(NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.Generate(new Random(1), Tolerance);
        Assert.Null(coordinator.RhythmAnchor);

        // However long the learner took to find the first key, that key is beat one: it grades as on time and the
        // click is anchored there.
        ScoreNote[] notes = coordinator.Score!.Measures[0].Notes.ToArray();
        NoteReadingSession.CheckResult first = coordinator.Session.Check(notes[0].Pitch, TimeSpan.FromSeconds(37));

        Assert.Equal(Verdict.Correct, first.Verdict);
        Assert.Equal(TimeSpan.FromSeconds(37), coordinator.RhythmAnchor);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder)]
    [InlineData(NoteReadingMode.PitchAndHold)]
    public void RhythmAnchor_ModesThatDoNotGradeOnset_StaysUnset(NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.Generate(new Random(1), Tolerance);

        coordinator.Session.Check(coordinator.Score!.Measures[0].Notes[0].Pitch, TimeSpan.FromSeconds(37));

        Assert.Null(coordinator.RhythmAnchor);
    }

    [Fact]
    public void RhythmAnchor_AfterRetry_IsUnsetAgainSoTheNextRunAnchorsOnItsOwnFirstKey()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.Session.Check(coordinator.Score!.Measures[0].Notes[0].Pitch, TimeSpan.FromSeconds(10));
        Assert.NotNull(coordinator.RhythmAnchor);

        Assert.True(coordinator.Retry(Tolerance));

        Assert.Null(coordinator.RhythmAnchor);
    }

    [Fact]
    public void GetRecentOnsetDeviations_FirstKeyIsBeatOneAndLaterNotesAreSignedAgainstIt()
    {
        SightReadingExerciseCoordinator coordinator = CreateOnsetGradedWaitForMeCoordinator();
        ScoreNote[] notes = coordinator.Score!.Measures[0].Notes.ToArray();
        Assert.Empty(coordinator.GetRecentOnsetDeviations(count: 6)!);

        // 120 pulses per minute: a quarter note every 500 ms from the first key.
        coordinator.Session.Check(notes[0].Pitch, TimeSpan.FromSeconds(37));
        coordinator.Session.Check(notes[1].Pitch, TimeSpan.FromSeconds(37.62));
        coordinator.Session.Check(notes[2].Pitch, TimeSpan.FromSeconds(37.96));

        Assert.Equal(
            [TimeSpan.Zero, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(-40)],
            coordinator.GetRecentOnsetDeviations(count: 6));
    }

    [Fact]
    public void GetRecentOnsetDeviations_MoreNotesThanAskedFor_KeepsTheLatestOldestFirst()
    {
        SightReadingExerciseCoordinator coordinator = CreateOnsetGradedWaitForMeCoordinator();
        ScoreNote[] notes = coordinator.Score!.Measures[0].Notes.ToArray();
        coordinator.Session.Check(notes[0].Pitch, TimeSpan.FromSeconds(10));
        coordinator.Session.Check(notes[1].Pitch, TimeSpan.FromSeconds(10.55));
        coordinator.Session.Check(notes[2].Pitch, TimeSpan.FromSeconds(11.10));
        coordinator.Session.Check(notes[3].Pitch, TimeSpan.FromSeconds(11.45));

        Assert.Equal(
            [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(-50)],
            coordinator.GetRecentOnsetDeviations(count: 2));
    }

    [Fact]
    public void GetRecentOnsetDeviations_NoExerciseOrAfterEnd_IsNull()
    {
        SightReadingExerciseCoordinator coordinator = CreateOnsetGradedWaitForMeCoordinator();
        coordinator.End();

        Assert.Null(coordinator.GetRecentOnsetDeviations(count: 6));
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder)]
    [InlineData(NoteReadingMode.PitchAndHold)]
    public void GetRecentOnsetDeviations_ModesThatDoNotGradeTheBeat_IsNull(NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.Generate(new Random(1), Tolerance);

        Assert.Null(coordinator.GetRecentOnsetDeviations(count: 6));
    }

    [Fact]
    public void GetRecentOnsetDeviations_PlayAlongRun_IsNullBecauseItsCursorShowsTheBeat()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        coordinator.Generate(new Random(1), Tolerance);

        Assert.Null(coordinator.GetRecentOnsetDeviations(count: 6));
    }

    [Fact]
    public void GetRecentOnsetDeviations_FinishedRun_KeepsTheGaugeForReview()
    {
        SightReadingExerciseCoordinator coordinator = CreateOnsetGradedWaitForMeCoordinator();
        double beatSeconds = 0.5;
        int index = 0;
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch, TimeSpan.FromSeconds(10 + (index++ * beatSeconds)));
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.NotNull(coordinator.GetRecentOnsetDeviations(count: 6));
    }

    [Fact]
    public void RhythmAnchor_PlayAlongRun_IsUnsetBecauseThePracticeEngineOwnsThatClock()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        coordinator.Generate(new Random(1), Tolerance);

        coordinator.Session.Check(coordinator.Score!.Measures[0].Notes[0].Pitch, TimeSpan.FromSeconds(10));

        Assert.Null(coordinator.RhythmAnchor);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndRhythm)]
    [InlineData(NoteReadingMode.RhythmOnly)]
    public void ShouldClickSound_NewOnsetGradedModes_FollowTheClickSetting(NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.Generate(new Random(1), Tolerance);
        Assert.True(coordinator.ShouldClickSound);

        coordinator.SetClickWhilePlaying(false);
        Assert.False(coordinator.ShouldClickSound);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndRhythm)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm)]
    [InlineData(NoteReadingMode.RhythmOnly)]
    public void RetryMissed_OnsetGradedMode_ReplaysTheMissedMeasureWithItsRhythmAndTempo(NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetTempoPulsesPerMinute(90);
        coordinator.SetPromptCountOption(16);
        coordinator.Generate(new Random(21), Tolerance);
        Score original = coordinator.Score!;
        ScoreMeasure secondMeasure = original.Measures[1];
        PlayThroughWithOneLateNote(coordinator, original, lateNote: secondMeasure.Notes[1]);

        Assert.True(coordinator.HasMissedPrompts);
        Assert.True(coordinator.RetryMissed(Tolerance));

        ScoreMeasure replayed = Assert.Single(coordinator.Score!.Measures);
        Assert.Equal(
            secondMeasure.Notes.Select(note => (note.NoteValue, note.BeatOffset, note.BeamState)),
            replayed.Notes.Select(note => (note.NoteValue, note.BeatOffset, note.BeamState)));
        Assert.Equal(
            secondMeasure.Rests.Select(rest => (rest.NoteValue, rest.BeatOffset)),
            replayed.Rests.Select(rest => (rest.NoteValue, rest.BeatOffset)));
        Assert.Equal(original.Tempo, coordinator.Score.Tempo);
        Assert.Equal(original.TimeSignature, coordinator.Score.TimeSignature);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
    }

    [Fact]
    public void RetryMissed_PitchOnlyMode_StillFlattensMissedPromptsIntoQuarterNotes()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetPromptCountOption(16);
        coordinator.Generate(new Random(21), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.RetryMissed(Tolerance));

        ScoreMeasure replayed = Assert.Single(coordinator.Score!.Measures);
        Assert.Single(replayed.Notes);
        Assert.Equal(new NoteValue(4), replayed.Notes[0].NoteValue);
        Assert.Empty(replayed.Rests);
    }

    private static void PlayThroughWithOneLateNote(
        SightReadingExerciseCoordinator coordinator,
        Score score,
        ScoreNote lateNote)
    {
        TimeSpan start = TimeSpan.FromSeconds(10);
        foreach (ScoreEvent scoreEvent in ScoreDerivation.Flatten(score))
        {
            bool isLate = scoreEvent.SourceNotes.Contains(lateNote);
            TimeSpan onset = start + MusicalTime.BeatsToDuration(
                (scoreEvent.SourceNotes[0].MeasureIndex * score.TimeSignature.Numerator) + scoreEvent.SourceNotes[0].BeatOffset,
                score.Tempo) + (isLate ? TimeSpan.FromMilliseconds(250) : TimeSpan.Zero);
            coordinator.Session.Check(scoreEvent.Pitch, onset);
            coordinator.Session.Release(
                scoreEvent.Pitch,
                onset + MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, score.Tempo));
        }
    }

    [Fact]
    public void Pacing_Initially_IsWaitForMe()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Equal(ExercisePacing.WaitForMe, coordinator.Pacing);
        Assert.Equal(ExercisePacing.WaitForMe, coordinator.EffectivePacing);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder, false)]
    [InlineData(NoteReadingMode.PitchAndHold, false)]
    [InlineData(NoteReadingMode.PitchAndRhythm, true)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, true)]
    [InlineData(NoteReadingMode.RhythmOnly, true)]
    public void EffectivePacing_PlayAlongChosen_AppliesOnlyToOnsetGradedModes(
        NoteReadingMode mode,
        bool expectsPlayAlong)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);
        Assert.Equal(
            expectsPlayAlong ? ExercisePacing.PlayAlong : ExercisePacing.WaitForMe,
            coordinator.EffectivePacing);
    }

    [Fact]
    public void SetPacing_PersistsAcrossGenerateRetryRetryMissedAndEnd()
    {
        var coordinator = CreatePlayAlongCoordinator();
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);

        coordinator.Retry(Tolerance);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);

        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 2), TimeSpan.FromSeconds(8));
        Assert.True(coordinator.RetryMissed(Tolerance));
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);

        coordinator.End();
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.Pacing);
    }

    [Fact]
    public void CompletePlayAlong_MovesTheExerciseToReviewAndExposesTheMappedResults()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);

        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Equal(outcome.PromptResults.Count, coordinator.PromptCount);
        Assert.Equal(outcome.PromptResults.Count, coordinator.CompletedPromptCount);
        Assert.Equal(outcome.PromptResults.Count - 1, coordinator.FirstTryCorrectCount);
        Assert.Equal(1, coordinator.WrongAttemptCount);
        Assert.Equal(100.0 * (outcome.PromptResults.Count - 1) / outcome.PromptResults.Count, coordinator.FirstTryAccuracyPercent, 6);
        Assert.Equal(TimeSpan.FromSeconds(12), coordinator.ElapsedTime);
        Assert.Same(outcome.PromptResults, coordinator.PromptResults);
        Assert.True(coordinator.HasMissedPrompts);
    }

    [Fact]
    public void CompletePlayAlong_ReviewMapAndCompletionSummaryReadTheSameOutcome()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));

        IReadOnlyDictionary<ScoreNote, bool> reviewMap = coordinator.BuildReviewFirstTryMap();
        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        NoteReadingPromptResult missed = outcome.PromptResults[3];
        Assert.All(missed.ExpectedSourceNotes, note => Assert.False(reviewMap[note]));
        Assert.NotNull(summary);
        Assert.Equal("playAlong", summary.Pacing);
        Assert.Equal(outcome.PromptResults.Count, summary.PromptCount);
        Assert.Equal(outcome.PromptResults.Count - 1, summary.FirstTryCorrectCount);
        Assert.Equal(TimeSpan.FromSeconds(12), summary.ElapsedTime);
        Assert.Equal(coordinator.TempoPulsesPerMinute, summary.TempoBeatsPerMinute);
    }

    [Fact]
    public void CoachHints_Initially_AreOff()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.CoachHints);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetCoachHints_PersistsAcrossGenerateRetryRetryMissedAndEnd(bool value)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetCoachHints(value);
        coordinator.SetPromptCountOption(4);

        coordinator.Generate(new Random(11), Tolerance);
        Assert.Equal(value, coordinator.CoachHints);
        coordinator.Retry(Tolerance);
        Assert.Equal(value, coordinator.CoachHints);
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.RetryMissed(Tolerance));
        Assert.Equal(value, coordinator.CoachHints);
        coordinator.End();
        Assert.Equal(value, coordinator.CoachHints);
    }

    [Fact]
    public void GetCoachHint_HintsOff_NeverHintsHoweverManyWrongKeys()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();

        PlayWrongKeys(coordinator, count: 6);

        Assert.Null(coordinator.GetCoachHint());
    }

    [Theory]
    [InlineData(0, ExerciseCoachHintLevel.None)]
    [InlineData(1, ExerciseCoachHintLevel.None)]
    [InlineData(2, ExerciseCoachHintLevel.Direction)]
    [InlineData(3, ExerciseCoachHintLevel.Direction)]
    [InlineData(4, ExerciseCoachHintLevel.Name)]
    [InlineData(7, ExerciseCoachHintLevel.Name)]
    public void GetCoachHint_HintsOn_FollowsTheWrongKeyThresholds(int wrongKeys, ExerciseCoachHintLevel expected)
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        coordinator.SetCoachHints(true);

        PlayWrongKeys(coordinator, wrongKeys);

        Assert.Equal(expected, coordinator.GetCoachHint()?.Level ?? ExerciseCoachHintLevel.None);
    }

    [Fact]
    public void GetCoachHint_DirectionLevel_CarriesTheLastWrongKeyAndHowItDiffers()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        coordinator.SetCoachHints(true);
        Pitch expected = coordinator.Session.ExpectedNotes.Single().Pitch;
        var firstWrong = new Pitch(NoteLetter.B, 0, 6);
        Pitch lastWrong = new(expected.Letter == NoteLetter.G ? NoteLetter.A : NoteLetter.G, 0, expected.Octave + 1);
        coordinator.Session.Check(firstWrong);
        coordinator.Session.Check(lastWrong);

        ExerciseCoachHint? hint = coordinator.GetCoachHint();

        Assert.NotNull(hint);
        Assert.Equal(ExerciseCoachHintLevel.Direction, hint.Level);
        Assert.Equal(lastWrong, hint.PressedPitch);
        Assert.Equal([expected], hint.ExpectedPitches);
        Assert.Equal(PitchDistance.Measure(lastWrong, expected), hint.Difference);
        Assert.False(hint.Difference?.IsHigher);
    }

    [Fact]
    public void GetCoachHint_NameLevel_ListsEveryExpectedPitchOfAChord()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetPromptCountOption(4);
        coordinator.SetCoachHints(true);
        coordinator.Generate(new Random(3), Tolerance);
        Pitch[] expected = coordinator.Session.ExpectedNotes.Select(note => note.Pitch).OrderBy(pitch => pitch.MidiNumber).ToArray();

        PlayWrongKeys(coordinator, count: 4);

        ExerciseCoachHint hint = Assert.IsType<ExerciseCoachHint>(coordinator.GetCoachHint());
        Assert.Equal(ExerciseCoachHintLevel.Name, hint.Level);
        Assert.Equal(expected, hint.ExpectedPitches.OrderBy(pitch => pitch.MidiNumber).ToArray());
    }

    [Fact]
    public void GetCoachHint_PromptAlreadyAnsweredAfterMistakes_MovesOnToTheNextPrompt()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        coordinator.SetCoachHints(true);
        ScoreNote first = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).First();
        PlayWrongKeys(coordinator, count: 4);
        Assert.NotNull(coordinator.GetCoachHint());

        coordinator.Session.Check(first.Pitch);

        Assert.Null(coordinator.GetCoachHint());
    }

    [Fact]
    public void GetCoachHint_UsingAHintNeverChangesFirstTryAccuracy()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        coordinator.SetCoachHints(true);
        ScoreNote first = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).First();
        PlayWrongKeys(coordinator, count: 4);

        ExerciseCoachHint? hint = coordinator.GetCoachHint();
        coordinator.Session.Check(first.Pitch);

        Assert.NotNull(hint);
        Assert.Equal(0, coordinator.FirstTryCorrectCount);
        Assert.False(coordinator.PromptResults[0].IsFirstTryCorrect);
        Assert.False(coordinator.PromptResults[0].IsPitchFirstTryCorrect);
        Assert.Equal(1, coordinator.CompletedPromptCount);
    }

    [Fact]
    public void GetCoachHint_ReviewPhaseOrPlayAlong_IsNull()
    {
        SightReadingExerciseCoordinator waitForMe = CreateWaitForMeCoordinator();
        waitForMe.SetCoachHints(true);
        PlayWrongKeys(waitForMe, count: 4);
        foreach (ScoreNote note in waitForMe.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            waitForMe.Session.Check(note.Pitch);
        }

        Assert.Equal(SightReadingExercisePhase.Review, waitForMe.Phase);
        Assert.Null(waitForMe.GetCoachHint());

        SightReadingExerciseCoordinator playAlong = CreatePlayAlongCoordinator();
        playAlong.SetCoachHints(true);
        playAlong.Generate(new Random(21), Tolerance);
        PlayWrongKeys(playAlong, count: 4);

        Assert.Null(playAlong.GetCoachHint());
    }

    [Fact]
    public void GetCoachHint_TimingMistakesWithoutWrongKeys_DoNotHint()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetPromptCountOption(8);
        coordinator.SetCoachHints(true);
        coordinator.Generate(new Random(5), Tolerance);
        ScoreEvent[] events = [.. ScoreDerivation.Flatten(coordinator.Score!)];
        TimeSpan start = TimeSpan.FromSeconds(10);
        coordinator.Session.Check(events[0].Pitch, start);
        // The second note is pressed far too early; wrong timing is not a wrong key.
        coordinator.Session.Check(events[1].Pitch, start + TimeSpan.FromMilliseconds(100));

        Assert.Contains(coordinator.PromptResults, result => result.HasTimingMistake);
        Assert.Null(coordinator.GetCoachHint());
    }

    private static SightReadingExerciseCoordinator CreateOnsetGradedWaitForMeCoordinator()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.SetPromptCountOption(8);
        coordinator.Generate(new Random(1), Tolerance);
        return coordinator;
    }

    private static SightReadingExerciseCoordinator CreateWaitForMeCoordinator()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(8);
        coordinator.Generate(new Random(11), Tolerance);
        return coordinator;
    }

    private static void PlayWrongKeys(SightReadingExerciseCoordinator coordinator, int count)
    {
        // Pitches far outside the exercise's range, so none of them can ever be the right key.
        for (int index = 0; index < count; index++)
        {
            coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6 - (index % 2)));
        }
    }

    [Fact]
    public void ReviewMistakes_WhileTheExerciseIsActive_IsNullEvenAfterAMistake()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);

        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));

        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.Null(coordinator.ReviewMistakes);
    }

    [Fact]
    public void ReviewMistakes_NoExercise_IsNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Null(coordinator.ReviewMistakes);
    }

    [Fact]
    public void ReviewMistakes_ReviewPhaseAfterAWrongNote_ListsThatPromptWithWhatWasPlayed()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        var wrong = new Pitch(NoteLetter.B, 0, 6);
        coordinator.Session.Check(wrong);
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        ExerciseReview? review = coordinator.ReviewMistakes;

        Assert.NotNull(review);
        ExerciseReviewLine line = Assert.Single(review.Lines);
        Assert.Equal(1, line.BarNumber);
        Assert.Equal(1, line.BeatNumber);
        Assert.Equal(wrong, line.PlayedPitch);
        Assert.Equal([notes[0].Pitch], line.ExpectedPitches);
    }

    [Fact]
    public void ReviewMistakes_PlayAlongRunWithASkippedNote_ListsItAsNotPlayed()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Null(coordinator.ReviewMistakes);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 3), TimeSpan.FromSeconds(9));

        ExerciseReview? review = coordinator.ReviewMistakes;

        Assert.NotNull(review);
        Assert.True(Assert.Single(review.Lines).WasMissed);
    }

    [Fact]
    public void PlayAlongVerdictCounts_AfterARun_ExposeTheRunsPerVerdictCountsUntilRetry()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Null(coordinator.PlayAlongVerdictCounts);

        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));

        Assert.NotNull(coordinator.PlayAlongVerdictCounts);
        Assert.Equal(1, coordinator.PlayAlongVerdictCounts[Verdict.Missed]);
        Assert.Equal(outcome.PromptResults.Count - 1, coordinator.PlayAlongVerdictCounts[Verdict.Correct]);

        coordinator.Retry(Tolerance);
        Assert.Null(coordinator.PlayAlongVerdictCounts);
    }

    [Fact]
    public void PlayAlongVerdictCounts_WaitForMeExercise_IsNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Null(coordinator.PlayAlongVerdictCounts);
    }

    [Fact]
    public void ConsumeCompletionSummary_PlayAlong_IsProducedExactlyOncePerRun()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Assert.Null(coordinator.ConsumeCompletionSummary());
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 1), TimeSpan.FromSeconds(9));

        Assert.NotNull(coordinator.ConsumeCompletionSummary());
        Assert.Null(coordinator.ConsumeCompletionSummary());

        coordinator.Retry(Tolerance);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 1), TimeSpan.FromSeconds(9));
        Assert.NotNull(coordinator.ConsumeCompletionSummary());
    }

    [Fact]
    public void ConsumeCompletionSummary_WaitForMe_LeavesPacingNull()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.Null(coordinator.ConsumeCompletionSummary()!.Pacing);
    }

    [Fact]
    public void Retry_AfterPlayAlongCompletes_ReturnsToActiveWithNoResults()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 0), TimeSpan.FromSeconds(9));

        Assert.True(coordinator.Retry(Tolerance));

        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.Empty(coordinator.PromptResults);
        Assert.Equal(0, coordinator.CompletedPromptCount);
        Assert.False(coordinator.HasMissedPrompts);
    }

    [Fact]
    public void RetryMissed_AfterPlayAlong_ReplaysTheMeasureThatContainsTheMissedNote()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        Score original = coordinator.Score!;
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 6);
        int missedMeasureIndex = outcome.PromptResults[6].ExpectedSourceNotes[0].MeasureIndex;
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(9));

        Assert.True(coordinator.RetryMissed(Tolerance));

        ScoreMeasure replayed = Assert.Single(coordinator.Score!.Measures);
        Assert.Equal(
            original.Measures[missedMeasureIndex].Notes.Select(note => (note.NoteValue, note.BeatOffset)),
            replayed.Notes.Select(note => (note.NoteValue, note.BeatOffset)));
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
    }

    [Fact]
    public void CompletePlayAlong_WhenWaitForMeApplies_IsRejected()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        coordinator.Generate(new Random(21), Tolerance);
        var outcome = new PlayAlongOutcome([], 0, new Dictionary<Verdict, int>());

        Assert.Throws<InvalidOperationException>(() => coordinator.CompletePlayAlong(outcome, TimeSpan.Zero));
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
    }

    [Fact]
    public void CompletePlayAlong_WithoutAnExerciseOrTwice_IsRejected()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        var empty = new PlayAlongOutcome([], 0, new Dictionary<Verdict, int>());
        Assert.Throws<InvalidOperationException>(() => coordinator.CompletePlayAlong(empty, TimeSpan.Zero));

        coordinator.Generate(new Random(21), Tolerance);
        coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 1), TimeSpan.FromSeconds(9));

        Assert.Throws<InvalidOperationException>(
            () => coordinator.CompletePlayAlong(MapPlayAlong(coordinator, skippedPromptIndex: 1), TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void WaitForMe_PromptResultsAndCountsStillComeFromTheSession()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.Same(coordinator.Session.PromptResults, coordinator.PromptResults);
        Assert.Equal(coordinator.Session.CompletedPromptCount, coordinator.CompletedPromptCount);
        Assert.Equal(coordinator.Session.FirstTryCorrectCount, coordinator.FirstTryCorrectCount);
        Assert.Equal(coordinator.Session.WrongAttemptCount, coordinator.WrongAttemptCount);
        Assert.Equal(coordinator.Session.FirstTryAccuracyPercent, coordinator.FirstTryAccuracyPercent);
        Assert.Equal(coordinator.Session.PromptCount, coordinator.PromptCount);
    }

    private static SightReadingExerciseCoordinator CreatePlayAlongCoordinator()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetPromptCountOption(16);
        coordinator.SetPacing(ExercisePacing.PlayAlong);
        return coordinator;
    }

    /// <summary>Plays the whole exercise perfectly in time, except one prompt that is never played.</summary>
    private static PlayAlongOutcome MapPlayAlong(SightReadingExerciseCoordinator coordinator, int skippedPromptIndex)
    {
        Score score = coordinator.Score!;
        TimeSpan anchor = TimeSpan.FromSeconds(10);
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(score);
        IReadOnlyList<IReadOnlyList<ScoreEvent>> prompts = ScoreDerivation.GroupByOnset(events);
        ScoreEvent[] skipped = [.. prompts[skippedPromptIndex]];
        PerformedNote[] performed = events
            .Where(scoreEvent => !skipped.Contains(scoreEvent))
            .Select(scoreEvent =>
            {
                TimeSpan start = anchor + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, score.Tempo);
                return new PerformedNote
                {
                    Pitch = scoreEvent.Pitch,
                    StartTime = start,
                    ReleaseTime = start + MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, score.Tempo),
                };
            })
            .ToArray();
        var options = new GradingOptions();
        GradingResult result = Grader.Grade(events, score.Tempo, performed, anchor, anchor + TimeSpan.FromMinutes(5), options);
        return PlayAlongResultMapper.Map(result, coordinator.Mode, score.Tempo, anchor, options);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Fixed, 60)]
    [InlineData(SightReadingRhythmPreset.Basic, 60)]
    [InlineData(SightReadingRhythmPreset.Compound, 40)]
    public void TempoPulsesPerMinute_Default_FollowsTheRhythmPresetsPulseUnit(
        SightReadingRhythmPreset rhythmPreset,
        int expectedPulses)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(rhythmPreset);

        Assert.Equal(expectedPulses, coordinator.TempoPulsesPerMinute);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(201)]
    public void SetTempoPulsesPerMinute_OutOfRange_Throws(int pulses)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.SetTempoPulsesPerMinute(pulses));
    }

    [Fact]
    public void Generate_WithChosenTempo_ComposesTheScoreAtThatTempo()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetTempoPulsesPerMinute(90);
        coordinator.Generate(new Random(3), Tolerance);

        Assert.Equal(90, coordinator.Score!.Tempo.BeatsPerMinute);
        Assert.Equal(90, coordinator.TempoPulsesPerMinute);
    }

    [Fact]
    public void SetTempoPulsesPerMinute_PersistsAcrossGenerateRetryAndEnd()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetTempoPulsesPerMinute(75);

        coordinator.Generate(new Random(3), Tolerance);
        coordinator.Retry(Tolerance);
        coordinator.End();
        coordinator.Generate(new Random(4), Tolerance);

        Assert.Equal(75, coordinator.TempoPulsesPerMinute);
        Assert.Equal(75, coordinator.Score!.Tempo.BeatsPerMinute);
    }

    [Fact]
    public void RetryMissed_KeepsTheChosenTempo()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetTempoPulsesPerMinute(80);
        coordinator.SetPromptCountOption(8);
        coordinator.Generate(new Random(5), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.B, 0, 6));
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.RetryMissed(Tolerance));

        Assert.Equal(80, coordinator.Score!.Tempo.BeatsPerMinute);
    }

    [Fact]
    public void SetRhythmPreset_ChangingThePulseUnit_ReturnsTheTempoToThePresetDefault()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetTempoPulsesPerMinute(90);

        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        Assert.Equal(90, coordinator.TempoPulsesPerMinute);

        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Compound);
        Assert.Equal(40, coordinator.TempoPulsesPerMinute);

        coordinator.SetTempoPulsesPerMinute(55);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Fixed);
        Assert.Equal(60, coordinator.TempoPulsesPerMinute);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder, null)]
    [InlineData(NoteReadingMode.PitchAndHold, 70)]
    [InlineData(NoteReadingMode.PitchHoldAndRhythm, 70)]
    public void ConsumeCompletionSummary_RecordsTheTempoOnlyWhenTimingIsGraded(
        NoteReadingMode mode,
        int? expectedTempo)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(mode);
        coordinator.SetTempoPulsesPerMinute(70);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(coordinator.Score!);
        TimeSpan start = TimeSpan.FromSeconds(10);
        foreach (ScoreEvent scoreEvent in events)
        {
            TimeSpan onset = start + MusicalTime.BeatsToDuration(scoreEvent.OnsetBeats, coordinator.Score!.Tempo);
            coordinator.Session.Check(scoreEvent.Pitch, onset);
            coordinator.Session.Release(
                scoreEvent.Pitch,
                onset + MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, coordinator.Score.Tempo));
        }

        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        Assert.NotNull(summary);
        Assert.Equal(expectedTempo, summary.TempoBeatsPerMinute);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Fixed, false)]
    [InlineData(SightReadingRhythmPreset.Basic, false)]
    [InlineData(SightReadingRhythmPreset.Fixed, true)]
    public void ConsumeCompletionSummary_AfterCompletion_RecordsRhythmPresetAndStaffLayout(
        SightReadingRhythmPreset rhythmPreset,
        bool isGrandStaff)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(rhythmPreset);
        coordinator.SetIsGrandStaff(isGrandStaff);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        Assert.NotNull(summary);
        Assert.Equal(rhythmPreset.ToString(), summary.RhythmPreset);
        Assert.Equal(isGrandStaff, summary.IsGrandStaff);
    }

    [Fact]
    public void ConsumeCompletionSummary_CalledTwiceForSameCompletion_ReturnsNullTheSecondTime()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        coordinator.ConsumeCompletionSummary();
        SightReadingSessionSummary? secondSummary = coordinator.ConsumeCompletionSummary();

        Assert.Null(secondSummary);
    }

    [Fact]
    public void ConsumeCompletionSummary_AfterRetry_ProducesANewSummaryForTheNewCompletion()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        coordinator.ConsumeCompletionSummary();
        coordinator.Retry(Tolerance);
        foreach (ScoreNote note in notes)
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.NotNull(coordinator.ConsumeCompletionSummary());
    }

    [Fact]
    public void SetIsGrandStaff_ReflectedInNextGeneratedScore()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(2), Tolerance);

        ScoreNote[] notes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Contains(notes, note => note.Staff == Staff.Treble);
        Assert.Contains(notes, note => note.Staff == Staff.Bass);
    }

    [Fact]
    public void Generate_ChordsWithTheSelectedProfileTooNarrow_ThrowsAndKeepsTheCurrentScore()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(6), Tolerance);
        Score current = coordinator.Score!;
        coordinator.SetFingeringProfile(CreateNarrowProfile());

        Assert.Throws<ScoreFingeringGenerationException>(() => coordinator.Generate(new Random(7), Tolerance));

        Assert.Same(current, coordinator.Score);
    }

    [Fact]
    public void RetryMissed_ChordsWithTheSelectedProfileTooNarrow_UsesThatProfileAndKeepsTheCurrentScore()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(6), Tolerance);
        Score current = coordinator.Score!;
        var chordPrompts = current.Measures
            .SelectMany(measure => measure.Notes)
            .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
            .OrderBy(group => group.Key.MeasureIndex)
            .ThenBy(group => group.Key.BeatOffset)
            .Select(group => group.ToArray())
            .ToArray();
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in chordPrompts.SelectMany(chord => chord))
        {
            coordinator.Session.Check(note.Pitch);
        }

        coordinator.SetFingeringProfile(CreateNarrowProfile());

        Assert.Throws<ScoreFingeringGenerationException>(() => coordinator.RetryMissed(Tolerance));
        Assert.Same(current, coordinator.Score);
    }

    [Fact]
    public void RetryMissed_ChordPreset_PreservesChordMembershipInTheCompactedScore()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Chords);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(6), Tolerance);
        var chordPrompts = coordinator.Score!.Measures
            .SelectMany(measure => measure.Notes)
            .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
            .OrderBy(group => group.Key.MeasureIndex)
            .ThenBy(group => group.Key.BeatOffset)
            .Select(group => group.ToArray())
            .ToArray();

        // Miss the first chord (wrong pitch before playing its tones), get the rest right first try.
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote[] chord in chordPrompts)
        {
            foreach (ScoreNote note in chord)
            {
                coordinator.Session.Check(note.Pitch);
            }
        }

        bool didRetryMissed = coordinator.RetryMissed(Tolerance);

        Assert.True(didRetryMissed);
        ScoreNote[] retryNotes = coordinator.Score!.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Equal(3, retryNotes.Length);
        Assert.All(retryNotes, note => Assert.Equal(0, note.MeasureIndex));
        Assert.All(retryNotes, note => Assert.Equal(0d, note.BeatOffset));
        Assert.Equal(
            chordPrompts[0].Select(note => note.Pitch).ToHashSet(),
            retryNotes.Select(note => note.Pitch).ToHashSet());
    }

    [Fact]
    public void Motion_Initially_IsMelodicWithAThirdAsTheIntervalSize()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Equal(SightReadingMotion.Melodic, coordinator.Motion);
        Assert.Equal(SightReadingMotion.Melodic, coordinator.EffectiveMotion);
        Assert.Equal(SightReadingExerciseOptions.DefaultIntervalSteps, coordinator.IntervalSteps);
    }

    [Fact]
    public void SetMotion_PersistsAcrossGenerateRetryAndEnd()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMotion(SightReadingMotion.Intervallic);
        coordinator.SetIntervalSteps(3);

        coordinator.Generate(new Random(1), Tolerance);
        coordinator.Retry(Tolerance);
        coordinator.End();

        Assert.Equal(SightReadingMotion.Intervallic, coordinator.Motion);
        Assert.Equal(3, coordinator.IntervalSteps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void SetIntervalSteps_OutsideTheSupportedSizes_Throws(int steps)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.SetIntervalSteps(steps));
    }

    [Fact]
    public void Generate_DefaultMotion_ComposesAStepwiseLine()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.OneOctave);
        coordinator.SetPromptCountOption(16);
        coordinator.Generate(new Random(5), Tolerance);

        int[] indexes = coordinator.Score!.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch.DiatonicIndex)
            .ToArray();
        Assert.All(indexes.Zip(indexes.Skip(1)), pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 2));
    }

    [Fact]
    public void Generate_IntervallicMotion_UsesTheChosenIntervalSize()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.OneOctave);
        coordinator.SetPromptCountOption(16);
        coordinator.SetMotion(SightReadingMotion.Intervallic);
        coordinator.SetIntervalSteps(3);

        coordinator.Generate(new Random(5), Tolerance);

        int[] indexes = coordinator.Score!.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch.DiatonicIndex)
            .ToArray();
        Assert.All(indexes.Zip(indexes.Skip(1)), pair => Assert.Equal(3, Math.Abs(pair.First - pair.Second)));
    }

    [Theory]
    [InlineData(SightReadingPresetId.Chords, NoteReadingMode.PitchAndOrder)]
    [InlineData(SightReadingPresetId.FiveNote, NoteReadingMode.RhythmOnly)]
    public void IsMotionAvailable_RangesAndModesThatPickTheirOwnNotes_FallBackToRandomOnPurpose(
        SightReadingPresetId presetId,
        NoteReadingMode mode)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(presetId);
        coordinator.SetMode(mode);
        coordinator.SetMotion(SightReadingMotion.Melodic);

        Assert.False(coordinator.IsMotionAvailable);
        Assert.Equal(SightReadingMotion.Random, coordinator.EffectiveMotion);
        Assert.Equal(SightReadingMotion.Melodic, coordinator.Motion);
    }

    [Theory]
    [InlineData(SightReadingPresetId.FiveNote)]
    [InlineData(SightReadingPresetId.LedgerLines)]
    public void IsMotionAvailable_PlainRangeAndLedgerLines_KeepTheChosenMotion(SightReadingPresetId presetId)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(presetId);
        coordinator.SetMotion(SightReadingMotion.Melodic);

        Assert.True(coordinator.IsMotionAvailable);
        Assert.Equal(SightReadingMotion.Melodic, coordinator.EffectiveMotion);
    }

    [Theory]
    [InlineData(SightReadingPresetId.OneOctave, SightReadingMotion.Melodic, "Melodic")]
    [InlineData(SightReadingPresetId.OneOctave, SightReadingMotion.Intervallic, "Intervallic")]
    [InlineData(SightReadingPresetId.OneOctave, SightReadingMotion.Random, "Random")]
    [InlineData(SightReadingPresetId.Chords, SightReadingMotion.Melodic, "Random")]
    public void ConsumeCompletionSummary_RecordsTheMotionThatWasActuallyUsed(
        SightReadingPresetId presetId,
        SightReadingMotion motion,
        string expected)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(presetId);
        coordinator.SetMotion(motion);
        coordinator.SetPromptCountOption(4);
        coordinator.Generate(new Random(11), Tolerance);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        SightReadingSessionSummary? summary = coordinator.ConsumeCompletionSummary();

        Assert.NotNull(summary);
        Assert.Equal(expected, summary.Motion);
    }

    [Theory]
    [InlineData(SightReadingMotion.Melodic)]
    [InlineData(SightReadingMotion.Intervallic)]
    public void GetDrillAvailability_NonRandomMotion_IsUnavailableBecauseADrillFollowsMissRates(SightReadingMotion motion)
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMotion(motion);

        DrillAvailability availability = coordinator.GetDrillAvailability(
            [WeakNote(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)]);

        Assert.False(availability.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(availability.Reason));
    }

    [Fact]
    public void GenerateRecommended_LeavesTheChosenMotionAsTheLearnerSetIt()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMotion(SightReadingMotion.Melodic);

        coordinator.GenerateRecommended(
            new Random(3),
            Tolerance,
            new LevelRecommendation(ExerciseLevelCatalog.Levels[0], null));

        Assert.Equal(SightReadingMotion.Melodic, coordinator.Motion);
    }

    [Fact]
    public void GetDrillAvailability_Accidentals_IsUnavailableBecauseItPicksItsOwnNotes()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Accidentals);

        DrillAvailability availability = coordinator.GetDrillAvailability(
            [WeakNote(new Pitch(NoteLetter.F, 1, 4), Staff.Treble)]);

        Assert.False(availability.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(availability.Reason));
    }

    [Fact]
    public void IsMotionAvailable_Accidentals_FallsBackToRandom()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetPresetId(SightReadingPresetId.Accidentals);
        coordinator.SetMotion(SightReadingMotion.Melodic);

        Assert.False(coordinator.IsMotionAvailable);
        Assert.Equal(SightReadingMotion.Random, coordinator.EffectiveMotion);
    }

    [Fact]
    public void RetryMissed_AccidentalsExerciseWhereEveryPromptWasMissed_NeverPutsAPlainNoteAfterItsAlteredLetterInAMeasure()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
            coordinator.SetPresetId(SightReadingPresetId.Accidentals);
            coordinator.SetPromptCountOption(16);
            coordinator.Generate(new Random(seed), Tolerance);
            foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
            {
                coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
                coordinator.Session.Check(note.Pitch);
            }

            Assert.True(coordinator.RetryMissed(Tolerance));

            foreach (ScoreMeasure measure in coordinator.Score!.Measures)
            {
                var altered = new Dictionary<NoteLetter, int>();
                foreach (ScoreNote note in measure.Notes.OrderBy(candidate => candidate.BeatOffset))
                {
                    if (altered.TryGetValue(note.Pitch.Letter, out int alter))
                    {
                        Assert.Equal(alter, note.Pitch.Alter);
                    }
                    else if (note.Pitch.Alter != 0)
                    {
                        altered[note.Pitch.Letter] = note.Pitch.Alter;
                    }
                }
            }
        }
    }

    [Fact]
    public void HandsTogether_Initially_IsOffAndUnavailableWithoutAGrandStaff()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.False(coordinator.HandsTogether);
        Assert.False(coordinator.IsHandsTogetherAvailable);
        Assert.False(coordinator.EffectiveHandsTogether);
    }

    [Fact]
    public void Generate_HandsTogetherChosenWithoutAGrandStaff_IsIgnoredInsteadOfThrowing()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetHandsTogether(true);

        coordinator.Generate(new Random(2), Tolerance);

        Assert.True(coordinator.HandsTogether);
        Assert.False(coordinator.EffectiveHandsTogether);
        Assert.All(coordinator.Score!.Measures.SelectMany(measure => measure.Notes), note => Assert.Equal(Staff.Treble, note.Staff));
    }

    [Fact]
    public void Generate_HandsTogetherOnAGrandStaff_PutsOneNotePerHandInEveryPrompt()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetHandsTogether(true);
        coordinator.SetPromptCountOption(8);

        coordinator.Generate(new Random(2), Tolerance);

        Assert.True(coordinator.IsHandsTogetherAvailable);
        Assert.Equal(8, coordinator.PromptCount);
        Assert.All(
            coordinator.Session.PromptResults,
            result => Assert.Equal(
                [Staff.Treble, Staff.Bass],
                result.ExpectedSourceNotes.Select(note => note.Staff).Order()));
    }

    [Fact]
    public void IsHandsTogetherAvailable_NeedsASupportedRangeAndAGrandStaffAndNotRhythmOnly()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        Assert.True(coordinator.IsHandsTogetherAvailable);

        foreach (SightReadingPresetId preset in Enum.GetValues<SightReadingPresetId>()
            .Where(preset => preset != SightReadingPresetId.Chords))
        {
            coordinator.SetPresetId(preset);
            Assert.True(coordinator.IsHandsTogetherAvailable, preset.ToString());
        }

        coordinator.SetPresetId(SightReadingPresetId.Chords);
        Assert.False(coordinator.IsHandsTogetherAvailable);

        coordinator.SetPresetId(SightReadingPresetId.FiveNote);
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        Assert.False(coordinator.IsHandsTogetherAvailable);
    }

    [Fact]
    public void Generate_HandsTogetherOnLedgerLinesWithMelodicMotion_PutsTwoDifferentKeysInEveryPrompt()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetPresetId(SightReadingPresetId.LedgerLines);
        coordinator.SetMotion(SightReadingMotion.Melodic);
        coordinator.SetHandsTogether(true);
        coordinator.SetPromptCountOption(16);

        coordinator.Generate(new Random(2), Tolerance);

        Assert.True(coordinator.EffectiveHandsTogether);
        Assert.All(
            coordinator.Session.PromptResults,
            result =>
            {
                Assert.Equal([Staff.Treble, Staff.Bass], result.ExpectedSourceNotes.Select(note => note.Staff).Order());
                Assert.Equal(2, result.ExpectedSourceNotes.Select(note => note.Pitch.MidiNumber).Distinct().Count());
            });
    }

    [Fact]
    public void SetHandsTogether_PersistsAcrossGenerateRetryAndEnd()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetHandsTogether(true);

        coordinator.Generate(new Random(1), Tolerance);
        coordinator.Retry(Tolerance);
        coordinator.End();

        Assert.True(coordinator.HandsTogether);
    }

    [Fact]
    public void RetryMissed_HandsTogether_KeepsBothHandsOfEachMissedPrompt()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetIsGrandStaff(true);
        coordinator.SetHandsTogether(true);
        coordinator.SetPromptCountOption(8);
        coordinator.Generate(new Random(4), Tolerance);
        coordinator.Session.Check(new Pitch(NoteLetter.A, 0, 6));
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }

        Assert.True(coordinator.RetryMissed(Tolerance));

        // Only the first prompt (both hands) was missed.
        Assert.Equal(
            [Staff.Treble, Staff.Bass],
            coordinator.Score!.Measures.SelectMany(measure => measure.Notes).Select(note => note.Staff).Order());
    }

    [Fact]
    public void SetRhythmPreset_BetweenQuarterPulsePresets_KeepsAChosenTempo()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Basic);
        coordinator.SetTempoPulsesPerMinute(90);

        coordinator.SetRhythmPreset(SightReadingRhythmPreset.ThreeFour);
        Assert.Equal(90, coordinator.TempoPulsesPerMinute);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.TwoFour);
        Assert.Equal(90, coordinator.TempoPulsesPerMinute);
    }

    // An exercise keeps the Mode and Pacing it was generated or retried with; the selects only choose what the next
    // Generate, Retry or Retry missed uses (like Staff, Preset, Range and the other durable settings).

    [Fact]
    public void SetModeAndPacing_WaitForMeRunInReview_KeepsPhaseResultsAndMarks()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        CompleteWaitForMeRunWithOneWrongKey(coordinator);
        IReadOnlyList<NoteReadingPromptResult> results = coordinator.PromptResults;
        IReadOnlyDictionary<ScoreNote, ReviewMark> marks = coordinator.BuildReviewMarks()!;
        int firstTryCorrect = coordinator.FirstTryCorrectCount;
        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);

        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Same(results, coordinator.PromptResults);
        Assert.Equal(firstTryCorrect, coordinator.FirstTryCorrectCount);
        Assert.True(coordinator.HasMissedPrompts);
        Assert.NotNull(coordinator.ReviewMistakes);
        Assert.Equal(marks, coordinator.BuildReviewMarks());
        Assert.Null(coordinator.PlayAlongVerdictCounts);
    }

    [Fact]
    public void SetModeAndPacing_PlayAlongRunInReview_KeepsPhaseResultsMarksAndSummary()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));
        IReadOnlyDictionary<ScoreNote, ReviewMark> marks = coordinator.BuildReviewMarks()!;

        coordinator.SetMode(NoteReadingMode.PitchAndOrder);
        coordinator.SetPacing(ExercisePacing.WaitForMe);

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Same(outcome.PromptResults, coordinator.PromptResults);
        Assert.Same(outcome.VerdictCounts, coordinator.PlayAlongVerdictCounts);
        Assert.Equal(TimeSpan.FromSeconds(12), coordinator.ElapsedTime);
        Assert.Equal(marks, coordinator.BuildReviewMarks());
        SightReadingSessionSummary summary = coordinator.ConsumeCompletionSummary()!;
        Assert.Equal("playAlong", summary.Pacing);
        Assert.Equal(NoteReadingMode.PitchAndRhythm, summary.Mode);
        Assert.NotNull(summary.TempoBeatsPerMinute);
    }

    [Fact]
    public void SetPacing_PlayAlongRunInProgress_StillCompletesAsAPlayAlongRun()
    {
        SightReadingExerciseCoordinator coordinator = CreatePlayAlongCoordinator();
        coordinator.Generate(new Random(21), Tolerance);
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 3);

        coordinator.SetPacing(ExercisePacing.WaitForMe);
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);
        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(12));

        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Same(outcome.PromptResults, coordinator.PromptResults);
    }

    [Fact]
    public void SetMode_TimedWaitForMeRunInProgress_KeepsTheRunsClickPolicy()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.Generate(new Random(1), Tolerance);
        Assert.True(coordinator.ShouldClickSound);

        coordinator.SetMode(NoteReadingMode.PitchAndOrder);

        Assert.True(coordinator.ShouldClickSound);
    }

    [Fact]
    public void Session_ModeChangedAfterGenerate_GradesWithTheModeTheExerciseWasGeneratedWith()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetTempoPulsesPerMinute(120);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);

        // The first note fixes beat one wherever it lands; the next one is half a second later when played on time,
        // so 300 ms past that is late when the beat is graded and fine when it is not.
        ScoreNote[] notes = coordinator.Score!.Measures[0].Notes.ToArray();
        coordinator.Session.Check(notes[0].Pitch, TimeSpan.FromSeconds(7));
        NoteReadingSession.CheckResult result = coordinator.Session.Check(
            notes[1].Pitch,
            TimeSpan.FromSeconds(7.8));
        Assert.Equal(Verdict.Late, result.Verdict);
    }

    [Theory]
    [InlineData("Generate")]
    [InlineData("Retry")]
    [InlineData("RetryMissed")]
    public void GenerateRetryAndRetryMissed_AfterModeAndPacingChanged_UseTheNewValues(string transition)
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        CompleteWaitForMeRunWithOneWrongKey(coordinator);
        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        switch (transition)
        {
            case "Generate":
                coordinator.Generate(new Random(21), Tolerance);
                break;
            case "Retry":
                Assert.True(coordinator.Retry(Tolerance));
                break;
            default:
                Assert.True(coordinator.RetryMissed(Tolerance));
                break;
        }

        Assert.Equal(SightReadingExercisePhase.Active, coordinator.Phase);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.EffectivePacing);
        PlayAlongOutcome outcome = MapPlayAlong(coordinator, skippedPromptIndex: 0);
        coordinator.CompletePlayAlong(outcome, TimeSpan.FromSeconds(9));
        Assert.Equal(SightReadingExercisePhase.Review, coordinator.Phase);
        Assert.Same(outcome.PromptResults, coordinator.PromptResults);
    }

    [Fact]
    public void RunModeAndRunPacing_WithoutAnExercise_FollowTheSettings()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.SetMode(NoteReadingMode.RhythmOnly);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        Assert.Equal(NoteReadingMode.RhythmOnly, coordinator.RunMode);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.RunPacing);

        coordinator.Generate(new Random(3), Tolerance);
        coordinator.End();
        coordinator.SetMode(NoteReadingMode.PitchAndOrder);

        Assert.Equal(NoteReadingMode.PitchAndOrder, coordinator.RunMode);
        Assert.Equal(ExercisePacing.WaitForMe, coordinator.RunPacing);
    }

    [Fact]
    public void RunModeAndRunPacing_WithAnExercise_ChangeOnlyWhenItIsGeneratedOrRetried()
    {
        SightReadingExerciseCoordinator coordinator = CreateWaitForMeCoordinator();
        Assert.Equal(NoteReadingMode.PitchAndOrder, coordinator.RunMode);
        Assert.Equal(ExercisePacing.WaitForMe, coordinator.RunPacing);

        coordinator.SetMode(NoteReadingMode.PitchAndRhythm);
        coordinator.SetPacing(ExercisePacing.PlayAlong);

        Assert.Equal(NoteReadingMode.PitchAndOrder, coordinator.RunMode);
        Assert.Equal(ExercisePacing.WaitForMe, coordinator.RunPacing);
        Assert.Equal(NoteReadingMode.PitchAndRhythm, coordinator.Mode);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.EffectivePacing);

        coordinator.Retry(Tolerance);

        Assert.Equal(NoteReadingMode.PitchAndRhythm, coordinator.RunMode);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.RunPacing);

        coordinator.SetMode(NoteReadingMode.PitchAndOrder);

        Assert.Equal(NoteReadingMode.PitchAndRhythm, coordinator.RunMode);
        Assert.Equal(ExercisePacing.PlayAlong, coordinator.RunPacing);

        coordinator.Generate(new Random(21), Tolerance);

        Assert.Equal(NoteReadingMode.PitchAndOrder, coordinator.RunMode);
        Assert.Equal(ExercisePacing.WaitForMe, coordinator.RunPacing);
    }

    private static void CompleteWaitForMeRunWithOneWrongKey(SightReadingExerciseCoordinator coordinator)
    {
        PlayWrongKeys(coordinator, 1);
        foreach (ScoreNote note in coordinator.Score!.Measures.SelectMany(measure => measure.Notes))
        {
            coordinator.Session.Check(note.Pitch);
        }
    }
}
