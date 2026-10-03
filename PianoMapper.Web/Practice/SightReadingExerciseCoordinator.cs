using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Web.Practice;

internal sealed class SightReadingExerciseCoordinator(NoteReadingSession session, TimeProvider? timeProvider = null)
{
    private readonly NoteReadingSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private bool hasConsumedCurrentCompletionSummary;
    private TimeSpan countInAudioClockOrigin;
    private long countInStartTimestamp;
    private TimeSpan countInDuration;
    private int? chosenTempoPulsesPerMinute;
    private PlayAlongOutcome? playAlongOutcome;
    private TimeSpan playAlongElapsedTime;
    private long? autoNextStartTimestamp;
    private NoteReadingMode runMode;
    private ExercisePacing runPacing;

    internal Staff Staff { get; private set; } = Staff.Treble;

    internal SightReadingPresetId PresetId { get; private set; } = SightReadingPresetId.FiveNote;

    internal int PromptCountOption { get; private set; } = 64;

    internal bool IsGrandStaff { get; private set; }

    /// <summary>
    /// The learner's choice to play both hands on every prompt, a durable setting. It only takes effect where it
    /// applies, see <see cref="EffectiveHandsTogether"/>.
    /// </summary>
    internal bool HandsTogether { get; private set; }

    /// <summary>
    /// Hands together needs a grand staff and the five-note range (the only range whose hands never share a pitch), and
    /// does not apply to rhythm only. The panel disables the option otherwise instead of the composer rejecting it.
    /// </summary>
    internal bool IsHandsTogetherAvailable =>
        IsGrandStaff && PresetId == SightReadingPresetId.FiveNote && !IsPitchSetupIgnored;

    /// <summary>What the next exercise really uses: <see cref="HandsTogether"/>, where it applies.</summary>
    internal bool EffectiveHandsTogether => HandsTogether && IsHandsTogetherAvailable;

    internal SightReadingRhythmPreset RhythmPreset { get; private set; } = SightReadingRhythmPreset.Fixed;

    /// <summary>
    /// The tempo in pulses per minute (a quarter note, or a dotted quarter in 6/8) the next exercise will use: the
    /// learner's choice, or the beginner default for the rhythm preset's pulse unit. A durable setting.
    /// </summary>
    internal int TempoPulsesPerMinute =>
        chosenTempoPulsesPerMinute ?? SightReadingExerciseOptions.GetDefaultTempoPulsesPerMinute(EffectiveRhythmPreset);

    /// <summary>
    /// Rhythm only repeats one centre-line note per staff, so the range, key and grand-staff choices do not apply to it.
    /// The panel disables those controls and the composer would reject them, so Generate leaves them out.
    /// </summary>
    internal bool IsPitchSetupIgnored => Mode == NoteReadingMode.RhythmOnly;

    /// <summary>
    /// Whether the exercise is graded on the beat, which is what gets a one-measure count-in and a click.
    /// </summary>
    internal bool UsesCountIn => Mode.GetGradedAxes().HasFlag(GradedAxes.Onset);

    /// <summary>
    /// A variable rhythm needs single notes on one staff, so a grand staff or chord exercise always plays fixed quarter
    /// notes. This is that fallback made explicit (the panel disables the Rhythm control and says so) instead of
    /// silently ignoring the choice. Rhythm only drops grand staff and chords itself, so it never locks the rhythm.
    /// </summary>
    internal bool IsRhythmPresetLocked =>
        !IsPitchSetupIgnored && (IsGrandStaff || PresetId == SightReadingPresetId.Chords);

    /// <summary>The rhythm the exercise really uses: <see cref="RhythmPreset"/>, unless it is locked to fixed quarter notes.</summary>
    internal SightReadingRhythmPreset EffectiveRhythmPreset =>
        IsRhythmPresetLocked ? SightReadingRhythmPreset.Fixed : RhythmPreset;

    /// <summary>What the next Generate, Retry or Retry missed will grade, a durable setting like <see cref="Staff"/>.</summary>
    internal NoteReadingMode Mode { get; private set; } = NoteReadingMode.PitchAndOrder;

    /// <summary>
    /// The mode the exercise on screen was generated or retried with, which it keeps (running or in review) whatever
    /// <see cref="Mode"/> is changed to meanwhile. Without an exercise it is just <see cref="Mode"/>.
    /// </summary>
    internal NoteReadingMode RunMode => Score is null ? Mode : runMode;

    /// <summary>How the next exercise's notes follow each other: the learner's choice, a durable setting.</summary>
    internal SightReadingMotion Motion { get; private set; } = SightReadingMotion.Random;

    /// <summary>The interval, in diatonic steps, that intervallic motion uses. A durable setting.</summary>
    internal int IntervalSteps { get; private set; } = SightReadingExerciseOptions.DefaultIntervalSteps;

    /// <summary>
    /// Whether the chosen <see cref="Motion"/> can apply: rhythm only repeats one note, and chords and ledger lines pick
    /// their own notes. The panel disables the Pattern control and says so, instead of silently ignoring the choice.
    /// </summary>
    internal bool IsMotionAvailable =>
        !IsPitchSetupIgnored && SightReadingExerciseComposer.SupportsMotion(PresetId);

    /// <summary>The motion the exercise really uses: <see cref="Motion"/>, unless it cannot apply.</summary>
    internal SightReadingMotion EffectiveMotion => IsMotionAvailable ? Motion : SightReadingMotion.Random;

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

    /// <summary>
    /// Same opt-in reveal as <see cref="RevealNoteNamesWhileActive"/>, but for the amber "next key" hint on the
    /// 88-key piano, which would otherwise give away the answer.
    /// </summary>
    internal bool RevealKeysWhileActive { get; private set; }

    /// <summary>
    /// Keeps a click sounding through a timed exercise, not only during the count-in: after the count-in in rhythm
    /// grading, and from the start in Pitch + hold (as a tempo reference). A durable exercise setting like the
    /// reveal flags, and on by default because a beginner needs the pulse; Generate/Retry/RetryMissed/End do not
    /// reset it.
    /// </summary>
    internal bool ClickWhilePlaying { get; private set; } = true;

    /// <summary>
    /// Opt-in coaching in the status line after repeated wrong keys, off by default like every other aid that gives
    /// the answer away (note names, fingering, next-key highlight). A durable setting: Generate/Retry/RetryMissed/End
    /// do not reset it.
    /// </summary>
    internal bool CoachHints { get; private set; }

    /// <summary>
    /// Once an exercise is finished, the page generates the next one after <see cref="AutoNextDelay"/>, so a learner
    /// can keep playing without reaching for the mouse. On by default (it reveals nothing, unlike the answer-giving
    /// aids) and a durable setting: Generate/Retry/RetryMissed/End do not reset it. See <see cref="AutoNextRemaining"/>
    /// for when the countdown runs.
    /// </summary>
    internal bool AutoNext { get; private set; } = true;

    /// <summary>How long a finished exercise stays on screen for review before the next one is generated.</summary>
    internal static readonly TimeSpan AutoNextDelay = TimeSpan.FromSeconds(10);

    /// <summary>Wrong keys on one prompt before a hint says which way to move.</summary>
    internal const int DirectionHintWrongKeyCount = 2;

    /// <summary>Wrong keys on one prompt before a hint names the note.</summary>
    internal const int NameHintWrongKeyCount = 4;

    internal Score? Score { get; private set; }

    internal NoteReadingSession Session => session;

    /// <summary>
    /// The learner's pacing choice, a durable setting. It only takes effect where it applies, see
    /// <see cref="EffectivePacing"/>.
    /// </summary>
    internal ExercisePacing Pacing { get; private set; } = ExercisePacing.WaitForMe;

    /// <summary>
    /// The pacing the next exercise will really run with: play-along needs a beat to follow, so a non-onset-graded
    /// mode always waits for the learner whatever <see cref="Pacing"/> says (and goes back to play-along when an
    /// onset graded mode is chosen again). The exercise on screen keeps its own, see <see cref="RunPacing"/>.
    /// </summary>
    internal ExercisePacing EffectivePacing =>
        Pacing == ExercisePacing.PlayAlong && UsesCountIn ? ExercisePacing.PlayAlong : ExercisePacing.WaitForMe;

    /// <summary>
    /// The pacing the exercise on screen was generated or retried with, which it keeps (running or in review)
    /// whatever <see cref="Pacing"/> or <see cref="Mode"/> are changed to meanwhile. Without an exercise it is just
    /// <see cref="EffectivePacing"/>.
    /// </summary>
    internal ExercisePacing RunPacing => Score is null ? EffectivePacing : runPacing;

    internal SightReadingExercisePhase Phase => Score is null
        ? SightReadingExercisePhase.Inactive
        : IsRunComplete
            ? SightReadingExercisePhase.Review
            : SightReadingExercisePhase.Active;

    private bool IsRunComplete => RunPacing == ExercisePacing.PlayAlong
        ? playAlongOutcome is not null
        : session.IsComplete;

    /// <summary>
    /// One view of the run's results for both pacings, so review, history, retry and the counts below behave the
    /// same whichever engine graded it: the pitch-gated session's prompt results, or the mapped play-along results.
    /// </summary>
    internal IReadOnlyList<NoteReadingPromptResult> PromptResults =>
        RunPacing == ExercisePacing.PlayAlong
            ? playAlongOutcome?.PromptResults ?? []
            : session.PromptResults;

    internal int PromptCount => session.PromptCount;

    /// <summary>
    /// The coaching hint for the prompt the learner is stuck on, or null. Only while a wait-for-me exercise is in
    /// progress with hints on, and only for wrong *keys* (a late note is not a wrong key): after
    /// <see cref="DirectionHintWrongKeyCount"/> it says which way to move from the last wrong key, after
    /// <see cref="NameHintWrongKeyCount"/> it names the note. Reading it has no effect on the run, and the prompt
    /// stays a first-try miss either way.
    /// </summary>
    internal ExerciseCoachHint? GetCoachHint()
    {
        if (!CoachHints || RunPacing != ExercisePacing.WaitForMe || Phase != SightReadingExercisePhase.Active)
        {
            return null;
        }

        NoteReadingPromptResult? current = session.PromptResults.LastOrDefault(result => !result.IsComplete);
        if (current is null || current.WrongPlayedPitches.Length < DirectionHintWrongKeyCount)
        {
            return null;
        }

        Pitch pressed = current.WrongPlayedPitches[^1];
        if (current.WrongPlayedPitches.Length >= NameHintWrongKeyCount)
        {
            return new ExerciseCoachHint(ExerciseCoachHintLevel.Name, current.ExpectedPitches, pressed, null);
        }

        Pitch nearest = current.ExpectedPitches.MinBy(expected => Math.Abs(expected.DiatonicIndex - pressed.DiatonicIndex));
        return new ExerciseCoachHint(
            ExerciseCoachHintLevel.Direction,
            current.ExpectedPitches,
            pressed,
            PitchDistance.Measure(pressed, nearest));
    }

    /// <summary>
    /// The mistakes to look at, once the exercise has reached review. Null before that on purpose: the list names
    /// the expected notes, which an exercise in progress keeps hidden.
    /// </summary>
    internal ExerciseReview? ReviewMistakes =>
        Phase == SightReadingExercisePhase.Review && Score is { } score
            ? ExerciseReviewBuilder.Build(PromptResults, score.TimeSignature)
            : null;

    /// <summary>
    /// Per-verdict counts (including notes that matched nothing) of a finished play-along run, or null for a
    /// pitch-gated run and until a play-along run finishes. The exercise summary lists them.
    /// </summary>
    internal IReadOnlyDictionary<Verdict, int>? PlayAlongVerdictCounts =>
        RunPacing == ExercisePacing.PlayAlong ? playAlongOutcome?.VerdictCounts : null;

    internal int CompletedPromptCount => RunPacing == ExercisePacing.PlayAlong
        ? PromptResults.Count(result => result.IsComplete)
        : session.CompletedPromptCount;

    internal int FirstTryCorrectCount => RunPacing == ExercisePacing.PlayAlong
        ? PromptResults.Count(result => result.IsComplete && result.IsFirstTryCorrect)
        : session.FirstTryCorrectCount;

    internal int WrongAttemptCount => RunPacing == ExercisePacing.PlayAlong
        ? PromptResults.Sum(result => result.WrongAttemptCount)
        : session.WrongAttemptCount;

    internal double FirstTryAccuracyPercent => CompletedPromptCount == 0
        ? 0
        : 100.0 * FirstTryCorrectCount / CompletedPromptCount;

    internal TimeSpan ElapsedTime => RunPacing == ExercisePacing.PlayAlong
        ? playAlongElapsedTime
        : session.ElapsedTime;

    internal bool IsActive => Phase != SightReadingExercisePhase.Inactive;

    internal bool IsCountingIn { get; private set; }

    /// <summary>The persisted name of play-along pacing in history; wait-for-me is stored as no pacing at all.</summary>
    internal const string PlayAlongPacingName = "playAlong";

    /// <summary>
    /// Whether the exercise's click should currently be sounding: always during the count-in, and otherwise only
    /// while a timing-graded exercise is in progress with <see cref="ClickWhilePlaying"/> on. False once it
    /// reaches review or ends.
    /// </summary>
    internal bool ShouldClickSound =>
        Phase == SightReadingExercisePhase.Active &&
        (IsCountingIn || (ClickWhilePlaying && SightReadingLabels.IsTimingGraded(RunMode)));

    internal void SetStaff(Staff staff) => Staff = staff;

    internal void SetPresetId(SightReadingPresetId presetId) => PresetId = presetId;

    internal void SetPromptCountOption(int promptCount) => PromptCountOption = promptCount;

    internal void SetIsGrandStaff(bool isGrandStaff) => IsGrandStaff = isGrandStaff;

    internal void SetHandsTogether(bool handsTogether) => HandsTogether = handsTogether;

    /// <summary>
    /// Switching between a quarter-note and a dotted-quarter pulse makes a chosen number mean something else (60
    /// quarters is not 60 dotted quarters), so a chosen tempo is dropped in favor of the new default then.
    /// </summary>
    internal void SetRhythmPreset(SightReadingRhythmPreset rhythmPreset)
    {
        if (UsesCompoundPulse(rhythmPreset) != UsesCompoundPulse(RhythmPreset))
        {
            chosenTempoPulsesPerMinute = null;
        }

        RhythmPreset = rhythmPreset;
    }

    internal void SetTempoPulsesPerMinute(int pulsesPerMinute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            pulsesPerMinute,
            SightReadingExerciseOptions.MinimumTempoPulsesPerMinute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            pulsesPerMinute,
            SightReadingExerciseOptions.MaximumTempoPulsesPerMinute);
        chosenTempoPulsesPerMinute = pulsesPerMinute;
    }

    private static bool UsesCompoundPulse(SightReadingRhythmPreset rhythmPreset) =>
        rhythmPreset == SightReadingRhythmPreset.Compound;

    internal void SetMode(NoteReadingMode mode) => Mode = mode;

    internal void SetMotion(SightReadingMotion motion) => Motion = motion;

    internal void SetIntervalSteps(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, SightReadingExerciseOptions.MinimumIntervalSteps);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, SightReadingExerciseOptions.MaximumIntervalSteps);
        IntervalSteps = steps;
    }

    internal void SetClickWhilePlaying(bool enabled) => ClickWhilePlaying = enabled;

    internal void SetPacing(ExercisePacing pacing) => Pacing = pacing;

    internal void SetCoachHints(bool enabled) => CoachHints = enabled;

    /// <summary>
    /// Turning it on while an exercise is in review starts the countdown from now, so a review that has been open for
    /// minutes does not jump to the next exercise the moment the box is ticked. Turning it off clears the countdown.
    /// </summary>
    internal void SetAutoNext(bool enabled)
    {
        AutoNext = enabled;
        if (!enabled)
        {
            autoNextStartTimestamp = null;
        }
        else if (autoNextStartTimestamp is null && CanCountDownToNext)
        {
            autoNextStartTimestamp = timeProvider.GetTimestamp();
        }
    }

    /// <summary>
    /// How long until the next exercise is generated, or null when no countdown is running: auto-next is off, the
    /// exercise is not finished, the learner cancelled it for this review, or a play-along run ended without a single
    /// prompt played (nobody was there, and generating run after run would only fill the history with empty runs).
    /// Counts down to <see cref="TimeSpan.Zero"/> and stays there until the page starts the next run, which clears it.
    /// </summary>
    internal TimeSpan? AutoNextRemaining
    {
        get
        {
            if (autoNextStartTimestamp is not { } started)
            {
                return null;
            }

            TimeSpan remaining = AutoNextDelay - timeProvider.GetElapsedTime(started);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Whether the countdown has run out, so the page should generate the next exercise now.</summary>
    internal bool IsAutoNextDue => AutoNextRemaining == TimeSpan.Zero;

    /// <summary>Stops the countdown for the exercise in review. The setting stays on for the next exercise.</summary>
    internal void CancelAutoNext() => autoNextStartTimestamp = null;

    /// <summary>Starts a running countdown over, for a learner who is still studying the review. No effect without one.</summary>
    internal void RestartAutoNext()
    {
        if (autoNextStartTimestamp is not null)
        {
            autoNextStartTimestamp = timeProvider.GetTimestamp();
        }
    }

    private bool CanCountDownToNext =>
        AutoNext &&
        Phase == SightReadingExercisePhase.Review &&
        PromptResults.Any(result => !result.WasMissed);

    internal void SetRevealNoteNamesWhileActive(bool reveal) => RevealNoteNamesWhileActive = reveal;

    internal void SetRevealFingeringWhileActive(bool reveal) => RevealFingeringWhileActive = reveal;

    internal void SetRevealKeysWhileActive(bool reveal) => RevealKeysWhileActive = reveal;

    /// <summary>
    /// Generates a new exercise. <paramref name="mastery"/> is optional local mastery history — typically
    /// <c>SightReadingHistory.ComputeNoteMasteryWeakestFirst()</c>'s result, so callers get the same
    /// "enough attempts to be meaningful" threshold already used for review UI — read fresh on every call (nothing
    /// here is cached across Generate calls), so the very next Generate after a session completes and is saved
    /// already reflects it. Converted to the composer's neutral-note-weight-by-default scheme, keyed by staff, internally;
    /// Core itself never sees <see cref="NoteMastery"/> or any history type. Null or empty reproduces the exact same
    /// balanced generation as before this parameter existed.
    /// </summary>
    internal void Generate(Random random, TimeSpan timingTolerance, IReadOnlyList<NoteMastery>? mastery = null) =>
        Generate(random, timingTolerance, mastery, SightReadingGenerationStrategy.CoverageFirst);

    /// <summary>
    /// Applies a ladder recommendation (the level's staff, grand staff, range, mode, rhythm and length, plus its
    /// recommended tempo for a timed level) and generates that exercise. Only the settings that define a level are
    /// changed: pacing, the reveal and hint options, and the click stay as the learner left them, and every setting
    /// can be changed again afterwards, since the ladder only recommends.
    /// </summary>
    internal void GenerateRecommended(
        Random random,
        TimeSpan timingTolerance,
        LevelRecommendation recommendation,
        IReadOnlyList<NoteMastery>? mastery = null)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        ExerciseLevel level = recommendation.Level;
        SetStaff(level.Staff);
        SetIsGrandStaff(level.IsGrandStaff);
        SetPresetId(level.PresetId);
        SetPromptCountOption(level.PromptCount);
        SetMode(level.Mode);
        SetRhythmPreset(level.RhythmPreset);
        if (level.IsTimed)
        {
            SetTempoPulsesPerMinute(recommendation.TempoPulsesPerMinute ?? level.StartTempoPulsesPerMinute!.Value);
        }

        Generate(random, timingTolerance, mastery);
    }

    /// <summary>
    /// Generates a drill on the learner's weak notes: the same options as <see cref="Generate"/> (so the range, staff and
    /// rhythm are the current ones and no note outside the range is used), but the weak notes in
    /// <paramref name="mastery"/> drive the picks from the first prompt on. An explicit action, never the default, and
    /// the next ordinary <see cref="Generate"/> is coverage-first again.
    /// </summary>
    internal void GenerateWeaknessDrill(Random random, TimeSpan timingTolerance, IReadOnlyList<NoteMastery> mastery)
    {
        ArgumentNullException.ThrowIfNull(mastery);
        Generate(random, timingTolerance, mastery, SightReadingGenerationStrategy.WeaknessFirst);
    }

    /// <summary>
    /// Whether a weak-note drill makes sense right now: the exercise draws single notes (not chords, not rhythm only,
    /// which repeats one note) and at least one of <paramref name="weakNotes"/> lies in the current range on a staff
    /// this exercise uses. A weak note recorded without a staff counts on either staff.
    /// </summary>
    internal DrillAvailability GetDrillAvailability(IReadOnlyList<NoteMastery> weakNotes)
    {
        ArgumentNullException.ThrowIfNull(weakNotes);
        if (IsPitchSetupIgnored)
        {
            return DrillAvailability.Unavailable("Rhythm only repeats one note, so there is no note to drill.");
        }

        if (PresetId == SightReadingPresetId.Chords)
        {
            return DrillAvailability.Unavailable("Chord exercises cannot be drilled note by note.");
        }

        if (PresetId == SightReadingPresetId.Accidentals)
        {
            return DrillAvailability.Unavailable(
                "Accidental exercises choose their notes by their own spelling rules, so they cannot be drilled yet.");
        }

        if (EffectiveMotion != SightReadingMotion.Random)
        {
            return DrillAvailability.Unavailable(
                "A drill picks notes by how often you miss them, so it needs the Random pattern.");
        }

        Staff[] staves = IsGrandStaff ? [Staff.Treble, Staff.Bass] : [Staff];
        bool hasWeakNoteInRange = staves.Any(staff =>
        {
            IReadOnlyList<Pitch> range = SightReadingExerciseComposer.GetRangePitches(staff, PresetId);
            return weakNotes.Any(note => (note.Staff is null || note.Staff == staff) && range.Contains(note.Pitch));
        });
        return hasWeakNoteInRange
            ? DrillAvailability.Available
            : DrillAvailability.Unavailable("No weak notes in this range yet. Play a few more exercises first.");
    }

    private void Generate(
        Random random,
        TimeSpan timingTolerance,
        IReadOnlyList<NoteMastery>? mastery,
        SightReadingGenerationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(random);
        var options = new SightReadingExerciseOptions(
            Staff,
            IsPitchSetupIgnored ? SightReadingPresetId.FiveNote : PresetId,
            PromptCountOption,
            Mode,
            IsGrandStaff && !IsPitchSetupIgnored,
            EffectiveRhythmPreset,
            chosenTempoPulsesPerMinute,
            strategy,
            EffectiveMotion,
            IntervalSteps,
            EffectiveHandsTogether);
        Score composed = SightReadingExerciseComposer.Compose(options, random, BuildNoteWeights(mastery));
        Score = ScoreFingeringGenerator.Generate(composed);
        StartRun(timingTolerance);
        IsCountingIn = false;
    }

    /// <summary>
    /// A note's weight scales linearly from 1.0 (neutral — no data, or a perfect, fast note) with its
    /// <see cref="NoteMastery.WeaknessScore"/>: a note that was never right first time (weakness 1.0) gets 5.0, so
    /// it is favored five times as strongly as a mastered one once <see cref="SightReadingExerciseComposer"/>'s
    /// initial full-palette-coverage pass is done (or from the start, in a weak-note drill).
    /// </summary>
    private const double MaxAdaptiveWeightBonus = 4.0;

    /// <summary>
    /// Weights keyed by (pitch, staff), so a note that is weak on one clef is not boosted on the other. Mastery
    /// recorded without a staff (older history) applies to both staves, but a staff-specific entry wins.
    /// </summary>
    private static IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? BuildNoteWeights(
        IReadOnlyList<NoteMastery>? mastery)
    {
        if (mastery is null || mastery.Count == 0)
        {
            return null;
        }

        var weights = new Dictionary<(Pitch Pitch, Staff Staff), double>();
        foreach (NoteMastery agnostic in mastery.Where(note => note.Staff is null))
        {
            foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
            {
                weights[(agnostic.Pitch, staff)] = ToWeight(agnostic);
            }
        }

        foreach (NoteMastery specific in mastery.Where(note => note.Staff is not null))
        {
            weights[(specific.Pitch, specific.Staff!.Value)] = ToWeight(specific);
        }

        return weights;
    }

    private static double ToWeight(NoteMastery note) => 1.0 + (note.WeaknessScore * MaxAdaptiveWeightBonus);

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
        session.Reset(Score, runMode, timingTolerance, explicitAnchor);
        ResetRunOutcome();
        return true;
    }

    /// <summary>Stops a count-in in progress without starting real grading (e.g. the user clicked End or Retry).</summary>
    internal void CancelCountIn() => IsCountingIn = false;

    internal bool Retry(TimeSpan timingTolerance)
    {
        if (Score is null)
        {
            return false;
        }

        StartRun(timingTolerance);
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
        ResetRunOutcome();
        IsCountingIn = false;
        return true;
    }

    /// <summary>Completed prompts whose pitches were found without a wrong key, whatever their timing.</summary>
    internal int PitchFirstTryCorrectCount =>
        PromptResults.Count(result => result.IsComplete && result.IsPitchFirstTryCorrect);

    /// <summary>Completed prompts with an early, late, too-short or too-long outcome.</summary>
    internal int TimingMistakeCount =>
        PromptResults.Count(result => result.IsComplete && result.HasTimingMistake);

    internal bool HasMissedPrompts =>
        Phase == SightReadingExercisePhase.Review &&
        PromptResults.Any(result => !result.IsFirstTryCorrect);

    internal bool RetryMissed(TimeSpan timingTolerance)
    {
        if (!HasMissedPrompts)
        {
            return false;
        }

        IReadOnlyList<ScoreNote>[] missedPromptGroups = PromptResults
            .Where(result => !result.IsFirstTryCorrect)
            .Select(result => (IReadOnlyList<ScoreNote>)result.ExpectedSourceNotes)
            .ToArray();
        // In an onset-graded mode the rhythm is part of what was missed, so the retry replays whole measures with
        // it; otherwise the missed notes are simply flattened into quarter notes as before.
        Score missedScore = UsesCountIn
            ? SightReadingExerciseComposer.ComposeFromMissedMeasures(
                Score!,
                missedPromptGroups.SelectMany(group => group).ToArray())
            : SightReadingExerciseComposer.ComposeFromMissedPrompts(Score!, missedPromptGroups);
        Score = ScoreFingeringGenerator.Generate(missedScore);
        StartRun(timingTolerance);
        return true;
    }

    /// <summary>
    /// Finishes a play-along run: the exercise moves to review with the run's mapped results, exactly as a
    /// pitch-gated run does when its last prompt completes. Only valid while a play-along exercise is in progress.
    /// </summary>
    internal void CompletePlayAlong(PlayAlongOutcome outcome, TimeSpan elapsedTime)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (Score is null || RunPacing != ExercisePacing.PlayAlong || Phase != SightReadingExercisePhase.Active)
        {
            throw new InvalidOperationException(
                "Play-along can only be completed while a play-along exercise (an onset-graded mode) is in progress.");
        }

        playAlongOutcome = outcome;
        playAlongElapsedTime = elapsedTime;
        hasConsumedCurrentCompletionSummary = false;
    }

    /// <summary>
    /// Starts a run of <see cref="Score"/> with the mode and pacing the settings choose right now. The run keeps both
    /// until the next Generate, Retry or Retry missed, so changing the Mode or Pacing select while it is running or in
    /// review changes nothing about it.
    /// </summary>
    private void StartRun(TimeSpan timingTolerance)
    {
        runMode = Mode;
        runPacing = EffectivePacing;
        session.Reset(Score, runMode, timingTolerance);
        ResetRunOutcome();
    }

    private void ResetRunOutcome()
    {
        playAlongOutcome = null;
        playAlongElapsedTime = TimeSpan.Zero;
        hasConsumedCurrentCompletionSummary = false;
        autoNextStartTimestamp = null;
    }

    /// <summary>
    /// Returns a serializable summary of the just-completed exercise exactly once per completion — the first call
    /// after the session reaches <see cref="SightReadingExercisePhase.Review"/> returns it; every call after that,
    /// until the next Generate/Retry/RetryMissed, returns <see langword="null"/>. Callers (the browser history
    /// store) use this to save a completed session exactly once even if rendering runs the check repeatedly. That
    /// first call is also the moment the exercise is known to be finished, so it starts the
    /// <see cref="AutoNextRemaining"/> countdown.
    /// </summary>
    internal SightReadingSessionSummary? ConsumeCompletionSummary()
    {
        if (Phase != SightReadingExercisePhase.Review || hasConsumedCurrentCompletionSummary)
        {
            return null;
        }

        hasConsumedCurrentCompletionSummary = true;
        if (CanCountDownToNext)
        {
            autoNextStartTimestamp = timeProvider.GetTimestamp();
        }

        return SightReadingSessionSummary.Create(
            timeProvider.GetUtcNow(),
            PresetId.ToString(),
            Staff,
            RunMode,
            ElapsedTime,
            PromptResults) with
        {
            RhythmPreset = EffectiveRhythmPreset.ToString(),
            IsGrandStaff = IsGrandStaff && !IsPitchSetupIgnored,
            TempoBeatsPerMinute = SightReadingLabels.IsTimingGraded(RunMode) ? TempoPulsesPerMinute : null,
            Pacing = RunPacing == ExercisePacing.PlayAlong ? PlayAlongPacingName : null,
            Motion = EffectiveMotion.ToString(),
        };
    }

    /// <summary>
    /// The review mark of every source note of every finished prompt, for drawing on the staff, or null while the
    /// exercise is not in <see cref="SightReadingExercisePhase.Review"/>. Null before review on purpose: a mark says
    /// which notes were wrong, which an exercise in progress keeps hidden. Both pacings read the same
    /// <see cref="PromptResults"/>, and the marks are a channel of their own, separate from the live verdict colors.
    /// </summary>
    internal IReadOnlyDictionary<ScoreNote, ReviewMark>? BuildReviewMarks() =>
        Phase == SightReadingExercisePhase.Review
            ? ExerciseReviewMarks.Build(PromptResults)
            : null;

    /// <summary>
    /// The colors of a finished play-along run's notes: the practice engine's verdicts, except that every note of a prompt
    /// the exercise graded clean is <see cref="Verdict.Correct"/>, the color Wait for me gives a correct note. The engine
    /// judges every release (a key let go early is "too short" even where the mode does not grade holds), but the exercise
    /// grades only what its mode asks for, so the clean prompts are taken from the exercise's own results. Notes that were
    /// not clean keep the engine's colors, and until the exercise has results (the run is going) nothing changes.
    /// </summary>
    internal IReadOnlyDictionary<ScoreNote, Verdict> BuildPlayAlongReviewVerdicts(
        IReadOnlyDictionary<ScoreNote, Verdict> engineVerdicts)
    {
        ArgumentNullException.ThrowIfNull(engineVerdicts);
        if (Phase != SightReadingExercisePhase.Review)
        {
            return engineVerdicts;
        }

        var verdicts = new Dictionary<ScoreNote, Verdict>(engineVerdicts);
        foreach (NoteReadingPromptResult result in PromptResults)
        {
            if (result.IsComplete && ExerciseReviewMarks.Classify(result) == ReviewMark.Clean)
            {
                foreach (ScoreNote note in result.ExpectedSourceNotes)
                {
                    verdicts[note] = Verdict.Correct;
                }
            }
        }

        return verdicts;
    }

    /// <summary>
    /// Maps every source note of every attempted prompt to whether that prompt was completed on the first try.
    /// Chord members share their prompt's outcome. Intended for review rendering once the exercise reaches the
    /// <see cref="SightReadingExercisePhase.Review"/> phase, but also reflects the currently in-progress prompt.
    /// </summary>
    internal IReadOnlyDictionary<ScoreNote, bool> BuildReviewFirstTryMap()
    {
        var map = new Dictionary<ScoreNote, bool>();
        foreach (NoteReadingPromptResult result in PromptResults)
        {
            foreach (ScoreNote note in result.ExpectedSourceNotes)
            {
                map[note] = result.IsFirstTryCorrect;
            }
        }

        return map;
    }
}
