using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

internal sealed class SightReadingExerciseCoordinator(NoteReadingSession session, TimeProvider? timeProvider = null)
{
    private readonly NoteReadingSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private bool hasConsumedCurrentCompletionSummary;
    private TimeSpan countInAudioClockOrigin;
    private long countInStartTimestamp;
    private TimeSpan countInDuration;

    internal Staff Staff { get; private set; } = Staff.Treble;

    internal SightReadingPresetId PresetId { get; private set; } = SightReadingPresetId.FiveNote;

    internal int PromptCountOption { get; private set; } = 8;

    internal bool IsGrandStaff { get; private set; }

    internal SightReadingRhythmPreset RhythmPreset { get; private set; } = SightReadingRhythmPreset.Fixed;

    internal NoteReadingMode Mode { get; private set; } = NoteReadingMode.PitchAndOrder;

    /// <summary>
    /// Opt-in "training wheels": when true, note names stay visible even while the exercise is
    /// <see cref="SightReadingExercisePhase.Active"/>, instead of only being revealed once it reaches
    /// <see cref="SightReadingExercisePhase.Review"/>. Defaults to false, matching the pre-existing
    /// hidden-while-active behavior. A durable exercise setting like <see cref="Staff"/>/<see cref="PresetId"/>/
    /// <see cref="Mode"/> — Generate/Retry/RetryMissed/End do not reset it.
    /// </summary>
    internal bool RevealNoteNamesWhileActive { get; private set; }

    /// <summary>Same opt-in reveal as <see cref="RevealNoteNamesWhileActive"/>, but for fingering suggestions.</summary>
    internal bool RevealFingeringWhileActive { get; private set; }

    internal Score? Score { get; private set; }

    internal NoteReadingSession Session => session;

    internal SightReadingExercisePhase Phase => Score is null
        ? SightReadingExercisePhase.Inactive
        : session.IsComplete
            ? SightReadingExercisePhase.Review
            : SightReadingExercisePhase.Active;

    internal bool IsActive => Phase != SightReadingExercisePhase.Inactive;

    internal bool IsCountingIn { get; private set; }

    internal void SetStaff(Staff staff) => Staff = staff;

    internal void SetPresetId(SightReadingPresetId presetId) => PresetId = presetId;

    internal void SetPromptCountOption(int promptCount) => PromptCountOption = promptCount;

    internal void SetIsGrandStaff(bool isGrandStaff) => IsGrandStaff = isGrandStaff;

    internal void SetRhythmPreset(SightReadingRhythmPreset rhythmPreset) => RhythmPreset = rhythmPreset;

    internal void SetMode(NoteReadingMode mode) => Mode = mode;

    internal void SetRevealNoteNamesWhileActive(bool reveal) => RevealNoteNamesWhileActive = reveal;

    internal void SetRevealFingeringWhileActive(bool reveal) => RevealFingeringWhileActive = reveal;

    /// <summary>
    /// Generates a new exercise. <paramref name="mastery"/> is optional local mastery history — typically
    /// <c>SightReadingHistory.ComputeMasteryWeakestFirst()</c>'s result, so callers get the same
    /// "enough attempts to be meaningful" threshold already used for review UI — read fresh on every call (nothing
    /// here is cached across Generate calls), so the very next Generate after a session completes and is saved
    /// already reflects it. Converted to the composer's neutral-pitch-weight-by-default scheme internally; Core
    /// itself never sees <see cref="PitchMastery"/> or any history type. Null or empty reproduces the exact same
    /// balanced generation as before this parameter existed.
    /// </summary>
    internal void Generate(Random random, TimeSpan timingTolerance, IReadOnlyList<PitchMastery>? mastery = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        var options = new SightReadingExerciseOptions(
            Staff,
            PresetId,
            PromptCountOption,
            Mode,
            IsGrandStaff,
            RhythmPreset);
        Score composed = SightReadingExerciseComposer.Compose(options, random, BuildPitchWeights(mastery));
        Score = ScoreFingeringGenerator.Generate(composed);
        session.Reset(Score, Mode, timingTolerance);
        hasConsumedCurrentCompletionSummary = false;
        IsCountingIn = false;
    }

    /// <summary>
    /// A pitch's weight scales linearly from 1.0 (neutral — every pitch with no data, or perfect 100% first-try
    /// accuracy) up to 5.0 (weakest possible — 0% accuracy), so a completely-missed pitch is favored five times as
    /// strongly as a perfectly-mastered one once <see cref="SightReadingExerciseComposer"/>'s initial
    /// full-palette-coverage pass is done.
    /// </summary>
    private const double MaxAdaptiveWeightBonus = 4.0;

    private static IReadOnlyDictionary<Pitch, double>? BuildPitchWeights(IReadOnlyList<PitchMastery>? mastery)
    {
        if (mastery is null || mastery.Count == 0)
        {
            return null;
        }

        return mastery.ToDictionary(
            pitchMastery => pitchMastery.Pitch,
            pitchMastery => 1.0 + ((100.0 - pitchMastery.AccuracyPercent) / 100.0 * MaxAdaptiveWeightBonus));
    }

    /// <summary>
    /// Starts a one-measure count-in anchored to <paramref name="currentAudioClockTime"/> (from
    /// <c>AudioSession.GetCurrentTimeAsync</c>). While counting in, the session is intentionally *not* yet reset for
    /// real grading — the caller must not route note input to it — so a note played during the count-in can't
    /// accidentally seed the lazy rhythm anchor. Call <see cref="TryCompleteCountIn"/> once the measure has elapsed.
    /// </summary>
    internal void StartCountIn(TimeSpan currentAudioClockTime)
    {
        if (Score is null)
        {
            throw new InvalidOperationException("Generate an exercise before starting a count-in.");
        }

        countInAudioClockOrigin = currentAudioClockTime;
        countInStartTimestamp = timeProvider.GetTimestamp();
        countInDuration = MusicalTime.BeatsToDuration(Score.TimeSignature.Numerator, Score.Tempo);
        IsCountingIn = true;
    }

    /// <summary>
    /// Which count-in beat (1-based, clamped to the time signature's numerator) is currently due, for status text
    /// like "3… 2… 1…". Mirrors <c>PracticeSession.CountInTicksDue</c>. Derives elapsed time from
    /// <see cref="TimeProvider"/> rather than re-reading the audio clock on every poll, the same technique
    /// <c>PracticeSession</c> already uses.
    /// </summary>
    internal int CountInTicksDue
    {
        get
        {
            if (!IsCountingIn || Score is not { } score)
            {
                return 0;
            }

            double elapsedBeats = MusicalTime.DurationToBeats(
                timeProvider.GetElapsedTime(countInStartTimestamp),
                score.Tempo);
            return Math.Clamp((int)Math.Floor(elapsedBeats) + 1, 0, score.TimeSignature.Numerator);
        }
    }

    /// <summary>
    /// Once a full measure has elapsed since <see cref="StartCountIn"/>, resets the session for real grading with
    /// the count-in's end as the explicit rhythm anchor (see Task 15's <c>NoteReadingSession.Reset</c> overload) and
    /// returns <see langword="true"/>. Returns <see langword="false"/> without side effects if not currently
    /// counting in, or if the measure hasn't elapsed yet — safe to poll repeatedly from a ticker.
    /// </summary>
    internal bool TryCompleteCountIn(TimeSpan timingTolerance)
    {
        if (!IsCountingIn || timeProvider.GetElapsedTime(countInStartTimestamp) < countInDuration)
        {
            return false;
        }

        IsCountingIn = false;
        TimeSpan explicitAnchor = countInAudioClockOrigin + countInDuration;
        session.Reset(Score, Mode, timingTolerance, explicitAnchor);
        hasConsumedCurrentCompletionSummary = false;
        return true;
    }

    /// <summary>Stops a count-in in progress without starting real grading (e.g. the user clicked End or Retry).</summary>
    internal void CancelCountIn() => IsCountingIn = false;

    internal bool Retry(TimeSpan timingTolerance)
    {
        if (Score is not { } score)
        {
            return false;
        }

        session.Reset(score, Mode, timingTolerance);
        hasConsumedCurrentCompletionSummary = false;
        IsCountingIn = false;
        return true;
    }

    internal bool End()
    {
        if (Score is null)
        {
            return false;
        }

        Score = null;
        session.Reset(null);
        IsCountingIn = false;
        return true;
    }

    internal bool HasMissedPrompts =>
        Phase == SightReadingExercisePhase.Review &&
        session.PromptResults.Any(result => !result.IsFirstTryCorrect);

    internal bool RetryMissed(TimeSpan timingTolerance)
    {
        if (!HasMissedPrompts)
        {
            return false;
        }

        IReadOnlyList<ScoreNote>[] missedPromptGroups = session.PromptResults
            .Where(result => !result.IsFirstTryCorrect)
            .Select(result => (IReadOnlyList<ScoreNote>)result.ExpectedSourceNotes)
            .ToArray();
        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(Score!, missedPromptGroups);
        Score = ScoreFingeringGenerator.Generate(missedScore);
        session.Reset(Score, Mode, timingTolerance);
        hasConsumedCurrentCompletionSummary = false;
        return true;
    }

    /// <summary>
    /// Returns a serializable summary of the just-completed exercise exactly once per completion — the first call
    /// after the session reaches <see cref="SightReadingExercisePhase.Review"/> returns it; every call after that,
    /// until the next Generate/Retry/RetryMissed, returns <see langword="null"/>. Callers (the browser history
    /// store) use this to save a completed session exactly once even if rendering runs the check repeatedly.
    /// </summary>
    internal SightReadingSessionSummary? ConsumeCompletionSummary()
    {
        if (Phase != SightReadingExercisePhase.Review || hasConsumedCurrentCompletionSummary)
        {
            return null;
        }

        hasConsumedCurrentCompletionSummary = true;
        return SightReadingSessionSummary.Create(
            timeProvider.GetUtcNow(),
            PresetId.ToString(),
            Staff,
            Mode,
            session.ElapsedTime,
            session.PromptResults);
    }

    /// <summary>
    /// Maps every source note of every attempted prompt to whether that prompt was completed on the first try.
    /// Chord members share their prompt's outcome. Intended for review rendering once the exercise reaches the
    /// <see cref="SightReadingExercisePhase.Review"/> phase, but also reflects the currently in-progress prompt.
    /// </summary>
    internal IReadOnlyDictionary<ScoreNote, bool> BuildReviewFirstTryMap()
    {
        var map = new Dictionary<ScoreNote, bool>();
        foreach (NoteReadingPromptResult result in session.PromptResults)
        {
            foreach (ScoreNote note in result.ExpectedSourceNotes)
            {
                map[note] = result.IsFirstTryCorrect;
            }
        }

        return map;
    }
}
