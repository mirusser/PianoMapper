using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

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
        neutralCoordinator.SetPromptCountOption(40);
        weightedCoordinator.SetPromptCountOption(40);
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var mastery = new[] { new PitchMastery(weakPitch, CorrectFirstTryCount: 0, AttemptCount: 10) };

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
        coordinator.SetPromptCountOption(40);
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var mastery = new[] { new PitchMastery(weakPitch, CorrectFirstTryCount: 0, AttemptCount: 10) };

        coordinator.Generate(new Random(11), Tolerance);
        int neutralCount = CountOccurrences(coordinator.Score!, weakPitch);

        coordinator.Generate(new Random(11), Tolerance, mastery);
        int weightedCount = CountOccurrences(coordinator.Score!, weakPitch);

        Assert.True(
            weightedCount > neutralCount + 5,
            $"Expected the second Generate call (with mastery) to weight G4 more heavily than the first " +
            $"(without mastery) on the same coordinator instance: neutral={neutralCount}, weighted={weightedCount}.");
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
    public void StartCountIn_WithoutGeneratedScore_Throws()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());

        Assert.Throws<InvalidOperationException>(() => coordinator.StartCountIn(TimeSpan.Zero));
    }

    [Fact]
    public void StartCountIn_AfterGenerate_SetsIsCountingIn()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(1), Tolerance);

        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        Assert.True(coordinator.IsCountingIn);
    }

    [Fact]
    public void CountInTicksDue_BeforeAnyTimeElapses_IsOne()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.Generate(new Random(1), Tolerance);

        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        Assert.Equal(1, coordinator.CountInTicksDue);
    }

    [Fact]
    public void CountInTicksDue_AfterOneBeatElapses_IsTwo()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        // FiveNote/default exercises are 4/4 at 120 BPM: one quarter-note beat is 500 ms.
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal(2, coordinator.CountInTicksDue);
    }

    [Fact]
    public void CountInTicksDue_NeverExceedsTimeSignatureNumerator()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(4, coordinator.CountInTicksDue);
    }

    [Fact]
    public void CountInTicksDue_WhenNotCountingIn_IsZero()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(1), Tolerance);

        Assert.Equal(0, coordinator.CountInTicksDue);
    }

    [Fact]
    public void TryCompleteCountIn_BeforeOneMeasureElapses_ReturnsFalseAndStaysCountingIn()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(1999));

        bool didComplete = coordinator.TryCompleteCountIn(Tolerance);

        Assert.False(didComplete);
        Assert.True(coordinator.IsCountingIn);
    }

    [Fact]
    public void TryCompleteCountIn_AfterOneMeasureElapses_ResetsSessionWithComputedAnchor()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.SetMode(NoteReadingMode.PitchHoldAndRhythm);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        // One 4/4 measure at 120 BPM is exactly 2 seconds; the anchor should land at 5s + 2s = 7s.
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        bool didComplete = coordinator.TryCompleteCountIn(Tolerance);

        Assert.True(didComplete);
        Assert.False(coordinator.IsCountingIn);
        ScoreNote firstNote = coordinator.Score!.Measures[0].Notes[0];
        NoteReadingSession.CheckResult onTimeResult = coordinator.Session.Check(
            firstNote.Pitch,
            TimeSpan.FromSeconds(7));
        Assert.Equal(Verdict.Correct, onTimeResult.Verdict);
    }

    [Fact]
    public void TryCompleteCountIn_InCompoundMeter_UsesEighthNoteBeatUnitNotQuarterNoteTempo()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.SetMode(NoteReadingMode.PitchHoldAndRhythm);
        coordinator.SetRhythmPreset(SightReadingRhythmPreset.Compound);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        // One 6/8 measure at "120" (eighth notes)/minute is 6 * 500 ms = 3 seconds, not the 4/4-style 2 seconds.
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        bool didComplete = coordinator.TryCompleteCountIn(Tolerance);

        Assert.True(didComplete);
        ScoreNote firstNote = coordinator.Score!.Measures[0].Notes[0];
        NoteReadingSession.CheckResult onTimeResult = coordinator.Session.Check(
            firstNote.Pitch,
            TimeSpan.FromSeconds(8));
        Assert.Equal(Verdict.Correct, onTimeResult.Verdict);
    }

    [Fact]
    public void TryCompleteCountIn_WhenNotCountingIn_ReturnsFalse()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(1), Tolerance);

        bool didComplete = coordinator.TryCompleteCountIn(Tolerance);

        Assert.False(didComplete);
    }

    [Fact]
    public void CancelCountIn_StopsCountingInAndFurtherPollingCannotCompleteIt()
    {
        var timeProvider = new FakeTimeProvider();
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession(), timeProvider);
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        coordinator.CancelCountIn();
        timeProvider.Advance(TimeSpan.FromSeconds(10));
        bool didComplete = coordinator.TryCompleteCountIn(Tolerance);

        Assert.False(coordinator.IsCountingIn);
        Assert.False(didComplete);
    }

    [Fact]
    public void Generate_WhileCountingIn_ClearsCountInState()
    {
        var coordinator = new SightReadingExerciseCoordinator(new NoteReadingSession());
        coordinator.Generate(new Random(1), Tolerance);
        coordinator.StartCountIn(TimeSpan.FromSeconds(5));

        coordinator.Generate(new Random(2), Tolerance);

        Assert.False(coordinator.IsCountingIn);
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
}
