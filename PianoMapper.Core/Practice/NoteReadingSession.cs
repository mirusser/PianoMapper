using System.Collections.Immutable;
using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed class NoteReadingSession
{
    private const double BeatComparisonTolerance = 1e-9;
    private static readonly TimeSpan DefaultTimingTolerance = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// How far a release may stray from the written duration, as a fraction of that duration. Players routinely lift
    /// a key slightly early, so judging a release as tightly as an onset (a fixed +/-60 ms) flags normal playing.
    /// The release window is <c>max(onset tolerance, 25% of the written duration)</c>: long notes get a
    /// proportionally forgiving window, and very short notes never get less than the onset tolerance.
    /// </summary>
    private const double ReleaseToleranceRatio = 0.25;

    private readonly TimeProvider timeProvider;
    private readonly HashSet<int> matchedMidiNumbers = [];
    private readonly Dictionary<int, HoldAttempt> activeHolds = [];
    private readonly Dictionary<int, int> matchedEventMidiByPressedKey = [];
    private IReadOnlyList<Step> steps = [];
    private readonly Dictionary<ScoreNote, Verdict> mutableVerdicts = [];
    private IReadOnlyDictionary<ScoreNote, Verdict> verdicts = new Dictionary<ScoreNote, Verdict>();
    private IReadOnlySet<ScoreNote> expectedNotes = new HashSet<ScoreNote>();
    private IReadOnlyList<NoteReadingPromptResult> promptResults = [];
    private Tempo? tempo;
    private NoteReadingMode mode = NoteReadingMode.PitchAndOrder;
    private GradedAxes gradedAxes = GradedAxes.Pitch;
    private TimeSpan timingTolerance = DefaultTimingTolerance;
    private TimeSpan? rhythmAnchor;
    private long startTimestamp;
    private long? firstAttemptTimestamp;
    private TimeSpan completedElapsedTime;
    private int stepIndex;
    private int completedPromptCount;
    private TimeSpan? lastAttackCompletedAt;

    public NoteReadingSession(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyDictionary<ScoreNote, Verdict> Verdicts => verdicts;

    public IReadOnlySet<ScoreNote> ExpectedNotes => expectedNotes;

    public IReadOnlyList<NoteReadingPromptResult> PromptResults => promptResults;

    public double? CurrentOnsetBeats => stepIndex < steps.Count
        ? steps[stepIndex].OnsetBeats
        : null;

    public int PromptCount => steps.Count;

    public int CompletedPromptCount => completedPromptCount;

    public int FirstTryCorrectCount { get; private set; }

    public int WrongAttemptCount { get; private set; }

    /// <summary>Wrong-key attempts. Together with <see cref="TimingMistakeCount"/> this partitions <see cref="WrongAttemptCount"/>.</summary>
    public int PitchMistakeCount { get; private set; }

    /// <summary>Right-key attempts graded early, late, too short or too long.</summary>
    public int TimingMistakeCount { get; private set; }

    public double FirstTryAccuracyPercent => CompletedPromptCount == 0
        ? 0
        : 100.0 * FirstTryCorrectCount / CompletedPromptCount;

    public bool IsComplete => stepIndex >= PromptCount && completedPromptCount >= PromptCount;

    public TimeSpan ElapsedTime => PromptCount == 0
        ? TimeSpan.Zero
        : IsComplete
            ? completedElapsedTime
            : GetFairElapsedTime();

    public void Reset(Score? score) =>
        Reset(score, NoteReadingMode.PitchAndOrder, DefaultTimingTolerance);

    /// <param name="explicitRhythmAnchor">
    /// The audio-clock time, in the same time domain as the <c>eventTime</c> passed to <see cref="Check(Pitch, TimeSpan)"/>,
    /// that beat zero of the score should be graded against (e.g. the instant a count-in ends). When supplied, the
    /// very first prompt's onset is graded as early/on-time/late against it, exactly like every later prompt. When
    /// omitted (the default), the existing lazy behavior is unchanged: the anchor is derived from the first played
    /// event's own time, so that first prompt always grades as on-time.
    /// </param>
    public void Reset(
        Score? score,
        NoteReadingMode noteReadingMode,
        TimeSpan noteTimingTolerance,
        TimeSpan? explicitRhythmAnchor = null)
    {
        if (noteTimingTolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(noteTimingTolerance));
        }

        if (!Enum.IsDefined(noteReadingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(noteReadingMode));
        }

        mode = noteReadingMode;
        gradedAxes = noteReadingMode.GetGradedAxes();
        timingTolerance = noteTimingTolerance;
        tempo = score?.Tempo;
        rhythmAnchor = explicitRhythmAnchor;
        steps = BuildSteps(score);
        mutableVerdicts.Clear();
        PublishVerdicts();
        matchedMidiNumbers.Clear();
        matchedEventMidiByPressedKey.Clear();
        activeHolds.Clear();
        stepIndex = 0;
        completedPromptCount = 0;
        FirstTryCorrectCount = 0;
        WrongAttemptCount = 0;
        PitchMistakeCount = 0;
        TimingMistakeCount = 0;
        lastAttackCompletedAt = null;
        startTimestamp = timeProvider.GetTimestamp();
        firstAttemptTimestamp = null;
        completedElapsedTime = TimeSpan.Zero;
        UpdateExpectedNotes();
        PublishPromptResults();
    }

    public CheckResult Check(Pitch pitch) =>
        Check(pitch, GetSessionElapsedTime());

    public CheckResult Check(Pitch pitch, TimeSpan eventTime)
    {
        firstAttemptTimestamp ??= timeProvider.GetTimestamp();

        if (stepIndex >= steps.Count)
        {
            if (RequiresHoldValidation && activeHolds.Count > 0)
            {
                Step latestPendingStep = activeHolds.Values
                    .MaxBy(hold => hold.Step.OnsetBeats)!
                    .Step;
                RecordPitchMistake(latestPendingStep, pitch);
                PublishPromptResults();
                return new CheckResult(IsCorrect: false, DidAdvance: false, IsComplete: false)
                {
                    Verdict = PianoMapper.Practice.Verdict.WrongPitch,
                };
            }

            return new CheckResult(IsCorrect: false, DidAdvance: false, IsComplete: IsComplete);
        }

        Step step = steps[stepIndex];
        ScoreEvent[] pendingEvents = step.Events
            .Where(scoreEvent => !matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
            .ToArray();
        // Without a pitch axis (rhythm only) any key matches the next pending event of the step.
        ScoreEvent[] matchingEvents = GradesPitch
            ? pendingEvents.Where(scoreEvent => scoreEvent.Pitch.MidiNumber == pitch.MidiNumber).ToArray()
            : pendingEvents.Take(1).ToArray();
        if (matchingEvents.Length == 0 ||
            RequiresHoldValidation && activeHolds.ContainsKey(pitch.MidiNumber))
        {
            RecordPitchMistake(step, pitch);
            PublishPromptResults();
            return new CheckResult(IsCorrect: false, DidAdvance: false, IsComplete: IsComplete)
            {
                Verdict = PianoMapper.Practice.Verdict.WrongPitch,
            };
        }

        step.IsAttempted = true;
        RecordFirstCorrectAttack(step, eventTime);
        int matchedEventMidi = matchingEvents[0].Pitch.MidiNumber;
        matchedMidiNumbers.Add(matchedEventMidi);
        if (!GradesPitch)
        {
            matchedEventMidiByPressedKey[pitch.MidiNumber] = matchedEventMidi;
        }
        foreach (ScoreNote matchedNote in GetSourceNotes(matchingEvents))
        {
            mutableVerdicts.Remove(matchedNote);
        }

        (Verdict onsetVerdict, TimeSpan onsetDeviation) = ClassifyOnset(step, eventTime);
        if (GradesOnset)
        {
            step.RecordOnset(onsetVerdict, onsetDeviation);
        }

        if (onsetVerdict != Verdict.Correct)
        {
            RecordTimingMistake(step);
            SetVerdict(matchingEvents, onsetVerdict);
        }

        if (RequiresHoldValidation)
        {
            activeHolds.Add(
                pitch.MidiNumber,
                new HoldAttempt(
                    step,
                    matchingEvents,
                    eventTime,
                    onsetVerdict,
                    onsetVerdict != Verdict.Correct));
            step.PendingHoldCount++;
        }
        else
        {
            SetVerdict(matchingEvents, onsetVerdict);
        }

        bool didAdvance = step.Events
            .Select(scoreEvent => scoreEvent.Pitch.MidiNumber)
            .Distinct()
            .All(matchedMidiNumbers.Contains);
        if (didAdvance)
        {
            step.AttackComplete = true;
            lastAttackCompletedAt = eventTime;
            stepIndex++;
            matchedMidiNumbers.Clear();
            matchedEventMidiByPressedKey.Clear();
            TryFinalizeStep(step);
        }

        PublishVerdicts();
        UpdateExpectedNotes();
        PublishPromptResults();
        return new CheckResult(
            IsCorrect: true,
            DidAdvance: didAdvance,
            IsComplete: IsComplete)
        {
            Verdict = onsetVerdict,
        };
    }

    public void Release(Pitch pitch) =>
        _ = Release(pitch, GetSessionElapsedTime());

    public ReleaseResult Release(Pitch pitch, TimeSpan eventTime)
    {
        if (!RequiresHoldValidation)
        {
            return ReleasePitchAndOrder(pitch);
        }

        if (!activeHolds.Remove(pitch.MidiNumber, out HoldAttempt? hold))
        {
            return new ReleaseResult(WasTracked: false, Verdict: null, IsComplete: IsComplete);
        }

        hold.Step.PendingHoldCount--;
        Verdict releaseVerdict = Verdict.Correct;
        Verdict durationVerdict = Verdict.Correct;
        bool hasDurationMistake = false;
        foreach (ScoreEvent scoreEvent in hold.Events)
        {
            Verdict eventDurationVerdict = ClassifyDuration(hold, scoreEvent, eventTime);
            hold.Step.RecordDuration(eventDurationVerdict);
            Verdict eventVerdict = eventDurationVerdict != Verdict.Correct
                ? eventDurationVerdict
                : hold.OnsetVerdict;
            SetVerdict(scoreEvent.SourceNotes, eventVerdict);
            if (eventVerdict != Verdict.Correct)
            {
                releaseVerdict = eventVerdict;
            }

            if (eventDurationVerdict is Verdict.TooShort or Verdict.TooLong)
            {
                durationVerdict = eventDurationVerdict;
                hasDurationMistake = true;
            }
        }

        if (hasDurationMistake && !hold.WrongAttemptRecorded)
        {
            RecordTimingMistake(hold.Step);
        }

        if (!hold.Step.AttackComplete)
        {
            matchedMidiNumbers.Remove(pitch.MidiNumber);
            foreach (ScoreNote sourceNote in GetSourceNotes(hold.Events))
            {
                mutableVerdicts.Remove(sourceNote);
            }
        }

        TryFinalizeStep(hold.Step);
        PublishVerdicts();
        UpdateExpectedNotes();
        PublishPromptResults();
        return new ReleaseResult(WasTracked: true, releaseVerdict, IsComplete)
        {
            OnsetVerdict = hold.OnsetVerdict,
            DurationVerdict = durationVerdict,
        };
    }

    public void ReleaseAll() =>
        ReleaseAll(GetSessionElapsedTime());

    public void ReleaseAll(TimeSpan eventTime)
    {
        if (!RequiresHoldValidation)
        {
            ReleaseAllPitchAndOrder();
            return;
        }

        foreach (int midiNumber in activeHolds.Keys.ToArray())
        {
            ReleaseByMidiNumber(midiNumber, eventTime);
        }
    }

    private void UpdateExpectedNotes()
    {
        HashSet<ScoreNote> updatedExpectedNotes = stepIndex >= steps.Count
            ? []
            : steps[stepIndex].Events
                .Where(scoreEvent => !matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
                .Select(scoreEvent => scoreEvent.SourceNotes[0])
                .ToHashSet();
        if (RequiresHoldValidation)
        {
            updatedExpectedNotes.UnionWith(activeHolds.Values.SelectMany(hold => GetSourceNotes(hold.Events)));
        }

        expectedNotes = updatedExpectedNotes;
    }

    private bool RequiresHoldValidation => gradedAxes.HasFlag(GradedAxes.Duration);

    private bool GradesOnset => gradedAxes.HasFlag(GradedAxes.Onset);

    private bool GradesPitch => gradedAxes.HasFlag(GradedAxes.Pitch);

    private TimeSpan GetSessionElapsedTime() => timeProvider.GetElapsedTime(startTimestamp);

    private TimeSpan GetFairElapsedTime() =>
        firstAttemptTimestamp is null ? TimeSpan.Zero : timeProvider.GetElapsedTime(firstAttemptTimestamp.Value);

    private (Verdict Verdict, TimeSpan Deviation) ClassifyOnset(Step step, TimeSpan eventTime)
    {
        if (!GradesOnset || tempo is not { } scoreTempo)
        {
            return (Verdict.Correct, TimeSpan.Zero);
        }

        rhythmAnchor ??= eventTime - MusicalTime.BeatsToDuration(step.OnsetBeats, scoreTempo);
        TimeSpan expectedOnset = rhythmAnchor.Value + MusicalTime.BeatsToDuration(step.OnsetBeats, scoreTempo);
        TimeSpan deviation = eventTime - expectedOnset;
        if (deviation < -timingTolerance)
        {
            return (Verdict.Early, deviation);
        }

        return (deviation > timingTolerance ? Verdict.Late : Verdict.Correct, deviation);
    }

    private void RecordFirstCorrectAttack(Step step, TimeSpan eventTime)
    {
        if (step.FirstCorrectAttackAt is not null)
        {
            return;
        }

        step.FirstCorrectAttackAt = eventTime;
        if (!GradesOnset && lastAttackCompletedAt is { } previousAttackCompletedAt)
        {
            step.ResponseTime = eventTime - previousAttackCompletedAt;
        }
    }

    private Verdict ClassifyDuration(HoldAttempt hold, ScoreEvent scoreEvent, TimeSpan eventTime)
    {
        if (tempo is not { } scoreTempo)
        {
            return Verdict.Correct;
        }

        TimeSpan performedDuration = eventTime - hold.StartTime;
        TimeSpan expectedDuration = MusicalTime.BeatsToDuration(scoreEvent.DurationBeats, scoreTempo);
        TimeSpan releaseTolerance = TimeSpan.FromTicks(Math.Max(
            timingTolerance.Ticks,
            (long)(expectedDuration.Ticks * ReleaseToleranceRatio)));
        if (performedDuration + releaseTolerance < expectedDuration)
        {
            return Verdict.TooShort;
        }

        return performedDuration - releaseTolerance > expectedDuration
            ? Verdict.TooLong
            : Verdict.Correct;
    }

    private ReleaseResult ReleasePitchAndOrder(Pitch pitch)
    {
        int releasedEventMidi = pitch.MidiNumber;
        if (!GradesPitch && !matchedEventMidiByPressedKey.Remove(pitch.MidiNumber, out releasedEventMidi))
        {
            return new ReleaseResult(WasTracked: false, Verdict: null, IsComplete: IsComplete);
        }

        if (!matchedMidiNumbers.Remove(releasedEventMidi) || stepIndex >= steps.Count)
        {
            return new ReleaseResult(WasTracked: false, Verdict: null, IsComplete: IsComplete);
        }

        foreach (ScoreNote releasedNote in GetSourceNotes(
            steps[stepIndex].Events.Where(scoreEvent => scoreEvent.Pitch.MidiNumber == releasedEventMidi)))
        {
            mutableVerdicts.Remove(releasedNote);
        }

        PublishVerdicts();
        UpdateExpectedNotes();
        PublishPromptResults();
        return new ReleaseResult(WasTracked: true, Verdict: null, IsComplete: IsComplete);
    }

    private void ReleaseAllPitchAndOrder()
    {
        if (matchedMidiNumbers.Count == 0 || stepIndex >= steps.Count)
        {
            return;
        }

        foreach (ScoreNote releasedNote in GetSourceNotes(
            steps[stepIndex].Events.Where(scoreEvent => matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))))
        {
            mutableVerdicts.Remove(releasedNote);
        }

        matchedMidiNumbers.Clear();
        matchedEventMidiByPressedKey.Clear();
        PublishVerdicts();
        UpdateExpectedNotes();
        PublishPromptResults();
    }

    private void ReleaseByMidiNumber(int midiNumber, TimeSpan eventTime)
    {
        if (activeHolds.TryGetValue(midiNumber, out HoldAttempt? hold))
        {
            Release(hold.Events[0].Pitch, eventTime);
        }
    }

    private void TryFinalizeStep(Step step)
    {
        if (step.IsFinalized || !step.AttackComplete || step.PendingHoldCount > 0)
        {
            return;
        }

        step.IsFinalized = true;
        step.CompletedAt = GetFairElapsedTime();
        completedPromptCount++;
        if (!step.HasWrongAttempt)
        {
            FirstTryCorrectCount++;
        }

        if (IsComplete)
        {
            completedElapsedTime = GetFairElapsedTime();
        }
    }

    private void RecordPitchMistake(Step step, Pitch wrongPitch)
    {
        PitchMistakeCount++;
        step.HasPitchMistake = true;
        step.WrongPlayedPitches.Add(wrongPitch);
        RecordWrongAttempt(step);
    }

    private void RecordTimingMistake(Step step)
    {
        TimingMistakeCount++;
        RecordWrongAttempt(step);
    }

    private void RecordWrongAttempt(Step step)
    {
        WrongAttemptCount++;
        step.HasWrongAttempt = true;
        step.IsAttempted = true;
        step.WrongAttemptCount++;
    }

    private void SetVerdict(IEnumerable<ScoreEvent> scoreEvents, Verdict verdict) =>
        SetVerdict(GetSourceNotes(scoreEvents), verdict);

    private void SetVerdict(IEnumerable<ScoreNote> sourceNotes, Verdict verdict)
    {
        foreach (ScoreNote sourceNote in sourceNotes)
        {
            mutableVerdicts[sourceNote] = verdict;
        }
    }

    private void PublishVerdicts() =>
        verdicts = mutableVerdicts.ToDictionary();

    private void PublishPromptResults() =>
        promptResults = BuildPromptResults();

    private IReadOnlyList<NoteReadingPromptResult> BuildPromptResults()
    {
        var results = new List<NoteReadingPromptResult>();
        for (int index = 0; index < steps.Count; index++)
        {
            Step step = steps[index];
            if (!step.IsAttempted)
            {
                continue;
            }

            results.Add(new NoteReadingPromptResult(
                PromptIndex: index,
                OnsetBeats: step.OnsetBeats,
                ExpectedSourceNotes: GetSourceNotes(step.Events).ToImmutableArray(),
                ExpectedPitches: step.Events.Select(scoreEvent => scoreEvent.Pitch).ToImmutableArray(),
                WrongPlayedPitches: step.WrongPlayedPitches.ToImmutableArray(),
                WrongAttemptCount: step.WrongAttemptCount,
                IsFirstTryCorrect: !step.HasWrongAttempt,
                IsComplete: step.IsFinalized,
                CompletedAt: step.CompletedAt)
            {
                IsPitchFirstTryCorrect = !step.HasPitchMistake,
                OnsetVerdict = step.OnsetVerdict,
                DurationVerdict = step.DurationVerdict,
                OnsetDeviation = step.OnsetDeviation,
                ResponseTime = step.ResponseTime,
            });
        }

        return results;
    }

    private IReadOnlyList<Step> BuildSteps(Score? score)
    {
        if (score is null || mode == NoteReadingMode.Off)
        {
            return [];
        }

        IReadOnlyList<ScoreEvent> scoreEvents = ScoreDerivation.Flatten(score);
        if (RequiresHoldValidation)
        {
            scoreEvents = CollapseOverlappingPitches(scoreEvents);
        }

        return ScoreDerivation.GroupByOnset(scoreEvents)
            .Select(group => new Step(group[0].OnsetBeats, group))
            .ToArray();
    }

    private static IReadOnlyList<ScoreEvent> CollapseOverlappingPitches(
        IReadOnlyList<ScoreEvent> scoreEvents) =>
        scoreEvents
            .GroupBy(scoreEvent => scoreEvent.Pitch.MidiNumber)
            .SelectMany(MergeOverlappingPitchEvents)
            .OrderBy(scoreEvent => scoreEvent.OnsetBeats)
            .ThenBy(scoreEvent => scoreEvent.Pitch.MidiNumber)
            .ToArray();

    private static IEnumerable<ScoreEvent> MergeOverlappingPitchEvents(
        IEnumerable<ScoreEvent> pitchEvents)
    {
        ScoreEvent? current = null;
        foreach (ScoreEvent next in pitchEvents.OrderBy(scoreEvent => scoreEvent.OnsetBeats))
        {
            if (current is null)
            {
                current = next;
                continue;
            }

            double currentEnd = current.OnsetBeats + current.DurationBeats;
            if (next.OnsetBeats < currentEnd - BeatComparisonTolerance)
            {
                double mergedEnd = Math.Max(
                    currentEnd,
                    next.OnsetBeats + next.DurationBeats);
                current = new ScoreEvent(
                    current.Pitch,
                    current.OnsetBeats,
                    mergedEnd - current.OnsetBeats,
                    current.Staff,
                    current.SourceNotes.Concat(next.SourceNotes).ToArray());
                continue;
            }

            yield return current;
            current = next;
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    private static IEnumerable<ScoreNote> GetSourceNotes(IEnumerable<ScoreEvent> scoreEvents) =>
        scoreEvents.SelectMany(scoreEvent => scoreEvent.SourceNotes);

    public readonly record struct CheckResult(
        bool IsCorrect,
        bool DidAdvance,
        bool IsComplete)
    {
        public Verdict? Verdict { get; init; }
    }

    public readonly record struct ReleaseResult(bool WasTracked, Verdict? Verdict, bool IsComplete)
    {
        public Verdict? OnsetVerdict { get; init; }

        public Verdict? DurationVerdict { get; init; }
    }

    private sealed class Step(double onsetBeats, IReadOnlyList<ScoreEvent> events)
    {
        public double OnsetBeats { get; } = onsetBeats;

        public IReadOnlyList<ScoreEvent> Events { get; } = events;

        public bool AttackComplete { get; set; }

        public bool HasWrongAttempt { get; set; }

        public bool HasPitchMistake { get; set; }

        public Verdict? OnsetVerdict { get; private set; }

        public TimeSpan? OnsetDeviation { get; private set; }

        public Verdict? DurationVerdict { get; private set; }

        public TimeSpan? FirstCorrectAttackAt { get; set; }

        public TimeSpan? ResponseTime { get; set; }

        /// <summary>Keeps the onset with the largest deviation, so a chord reports its worst key.</summary>
        public void RecordOnset(Verdict verdict, TimeSpan deviation)
        {
            if (OnsetDeviation is { } current && current.Duration() >= deviation.Duration())
            {
                return;
            }

            OnsetVerdict = verdict;
            OnsetDeviation = deviation;
        }

        /// <summary>Keeps the first duration mistake, otherwise <see cref="Verdict.Correct"/>.</summary>
        public void RecordDuration(Verdict verdict)
        {
            if (DurationVerdict is null or Verdict.Correct)
            {
                DurationVerdict = verdict;
            }
        }

        public int PendingHoldCount { get; set; }

        public bool IsFinalized { get; set; }

        public bool IsAttempted { get; set; }

        public int WrongAttemptCount { get; set; }

        public List<Pitch> WrongPlayedPitches { get; } = [];

        public TimeSpan? CompletedAt { get; set; }
    }

    private sealed record HoldAttempt(
        Step Step,
        IReadOnlyList<ScoreEvent> Events,
        TimeSpan StartTime,
        Verdict OnsetVerdict,
        bool WrongAttemptRecorded);
}
