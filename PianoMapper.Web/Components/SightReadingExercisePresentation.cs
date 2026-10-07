using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Web.Components;

/// <summary>The complete render state for the note-reading exercise presentation module.</summary>
public sealed record SightReadingExercisePresentation
{
    public required Staff SelectedStaff { get; init; }

    public required SightReadingPresetId PresetId { get; init; }

    public required int PromptCountOption { get; init; }

    /// <summary>The motion the exercise will really use (random when the range or mode picks its own notes).</summary>
    public required SightReadingMotion Motion { get; init; }

    public required int IntervalSteps { get; init; }

    public required bool IsMotionAvailable { get; init; }

    public required bool IsGrandStaff { get; init; }

    /// <summary>Whether the next exercise plays both hands on every prompt (false where it cannot apply).</summary>
    public required bool IsHandsTogether { get; init; }

    public required bool IsHandsTogetherAvailable { get; init; }

    public required SightReadingRhythmPreset RhythmPreset { get; init; }

    /// <summary>What the Mode select chooses for the next exercise.</summary>
    public required NoteReadingMode Mode { get; init; }

    /// <summary>The mode the exercise on screen was generated or retried with, which its result lines follow.</summary>
    public required NoteReadingMode RunMode { get; init; }

    public required int TempoPulsesPerMinute { get; init; }

    public required bool IsRhythmPresetLocked { get; init; }

    public required ExercisePacing Pacing { get; init; }

    public required bool IsPacingAvailable { get; init; }

    public required bool IsPitchSetupIgnored { get; init; }

    public required bool RevealNoteNamesWhileActive { get; init; }

    public required bool RevealFingeringWhileActive { get; init; }

    public required bool RevealKeysWhileActive { get; init; }

    public required bool ClickWhilePlaying { get; init; }

    /// <summary>
    /// False while a timing-graded exercise is running, because changing the audible click then could make it disagree
    /// with the run's fixed grading anchor.
    /// </summary>
    public required bool CanChangeClickWhilePlaying { get; init; }

    public required bool CoachHints { get; init; }

    public required bool AutoNext { get; init; }

    /// <summary>
    /// Whole seconds until the next exercise starts by itself (rounded up, so the countdown reads 10 down to 1), or
    /// null when no countdown is running.
    /// </summary>
    public required int? AutoNextSecondsRemaining { get; init; }

    /// <summary>
    /// How many pages of measures the exercise spans. The score shows two pages at once, so past two the panel offers
    /// paging through the review.
    /// </summary>
    public required int ScorePageCount { get; init; }

    public required bool CanShowPreviousMeasures { get; init; }

    public required bool CanShowNextMeasures { get; init; }

    public required bool IsClickRunning { get; init; }

    public required ExerciseReview? ReviewMistakes { get; init; }

    public required IReadOnlyDictionary<Verdict, int>? PlayAlongVerdictCounts { get; init; }

    public required TempoFeedbackTracker TempoFeedback { get; init; }

    public required bool IsActive { get; init; }

    public required bool IsComplete { get; init; }

    public required bool HasMissedPrompts { get; init; }

    /// <summary>True when the current range has notes the learner misses often, which a drill needs.</summary>
    public required bool CanDrillWeakNotes { get; init; }

    /// <summary>The ladder's next step in words, shown beside "Start recommended exercise".</summary>
    public required string RecommendedExerciseDescription { get; init; }

    /// <summary>Why the weak-note drill is unavailable; null when it is available.</summary>
    public required string? DrillUnavailableReason { get; init; }

    public required bool IsMidiInputConnected { get; init; }

    public required bool IsComputerPianoEnabled { get; init; }

    public required int PromptCount { get; init; }

    public required int CompletedPromptCount { get; init; }

    public required int FirstTryCorrectCount { get; init; }

    public required int WrongAttemptCount { get; init; }

    public required int PitchFirstTryCorrectCount { get; init; }

    public required int TimingMistakeCount { get; init; }

    public required double FirstTryAccuracyPercent { get; init; }

    public required TimeSpan ElapsedTime { get; init; }
}
