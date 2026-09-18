using PianoMapper.Music;

namespace PianoMapper.Practice;

public sealed class NoteReadingSession
{
    private readonly TimeProvider timeProvider;
    private readonly HashSet<int> matchedMidiNumbers = [];
    private IReadOnlyList<Step> steps = [];
    private IReadOnlyDictionary<ScoreNote, Verdict> verdicts = new Dictionary<ScoreNote, Verdict>();
    private IReadOnlySet<ScoreNote> expectedNotes = new HashSet<ScoreNote>();
    private long startTimestamp;
    private TimeSpan completedElapsedTime;
    private int stepIndex;
    private bool currentPromptHasWrongAttempt;

    public NoteReadingSession(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyDictionary<ScoreNote, Verdict> Verdicts => verdicts;

    public IReadOnlySet<ScoreNote> ExpectedNotes => expectedNotes;

    public double? CurrentOnsetBeats => stepIndex < steps.Count
        ? steps[stepIndex].OnsetBeats
        : null;

    public int PromptCount => steps.Count;

    public int CompletedPromptCount => stepIndex;

    public int FirstTryCorrectCount { get; private set; }

    public int WrongAttemptCount { get; private set; }

    public double FirstTryAccuracyPercent => CompletedPromptCount == 0
        ? 0
        : 100.0 * FirstTryCorrectCount / CompletedPromptCount;

    public bool IsComplete => stepIndex >= PromptCount;

    public TimeSpan ElapsedTime => PromptCount == 0
        ? TimeSpan.Zero
        : IsComplete
            ? completedElapsedTime
            : timeProvider.GetElapsedTime(startTimestamp);

    public void Reset(Score? score)
    {
        steps = score is null
            ? []
            : ScoreDerivation.Flatten(score)
                .GroupBy(scoreEvent => scoreEvent.OnsetBeats)
                .Select(group => new Step(group.Key, group.ToArray()))
                .ToArray();
        verdicts = new Dictionary<ScoreNote, Verdict>();
        matchedMidiNumbers.Clear();
        stepIndex = 0;
        FirstTryCorrectCount = 0;
        WrongAttemptCount = 0;
        currentPromptHasWrongAttempt = false;
        startTimestamp = timeProvider.GetTimestamp();
        completedElapsedTime = TimeSpan.Zero;
        UpdateExpectedNotes();
    }

    public CheckResult Check(Pitch pitch)
    {
        if (stepIndex >= steps.Count)
        {
            return new CheckResult(IsCorrect: false, DidAdvance: false, IsComplete: true);
        }

        Step step = steps[stepIndex];
        ScoreEvent[] pendingEvents = step.Events
            .Where(scoreEvent => !matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
            .ToArray();
        ScoreEvent[] matchingEvents = pendingEvents
            .Where(scoreEvent => scoreEvent.Pitch.MidiNumber == pitch.MidiNumber)
            .ToArray();
        if (matchingEvents.Length == 0)
        {
            WrongAttemptCount++;
            currentPromptHasWrongAttempt = true;
            return new CheckResult(IsCorrect: false, DidAdvance: false, IsComplete: false);
        }

        var updatedVerdicts = verdicts.ToDictionary();
        matchedMidiNumbers.Add(pitch.MidiNumber);
        foreach (ScoreNote matchedNote in matchingEvents.SelectMany(scoreEvent => scoreEvent.SourceNotes))
        {
            updatedVerdicts[matchedNote] = Verdict.Correct;
        }

        bool didAdvance = step.Events
            .Select(scoreEvent => scoreEvent.Pitch.MidiNumber)
            .Distinct()
            .All(matchedMidiNumbers.Contains);
        if (didAdvance)
        {
            if (!currentPromptHasWrongAttempt)
            {
                FirstTryCorrectCount++;
            }

            stepIndex++;
            matchedMidiNumbers.Clear();
            currentPromptHasWrongAttempt = false;
            if (IsComplete)
            {
                completedElapsedTime = timeProvider.GetElapsedTime(startTimestamp);
            }
        }

        verdicts = updatedVerdicts;
        UpdateExpectedNotes();
        return new CheckResult(
            IsCorrect: true,
            DidAdvance: didAdvance,
            IsComplete: IsComplete);
    }

    public void Release(Pitch pitch)
    {
        if (!matchedMidiNumbers.Remove(pitch.MidiNumber) || stepIndex >= steps.Count)
        {
            return;
        }

        var updatedVerdicts = verdicts.ToDictionary();
        foreach (ScoreNote releasedNote in steps[stepIndex].Events
            .Where(scoreEvent => scoreEvent.Pitch.MidiNumber == pitch.MidiNumber)
            .SelectMany(scoreEvent => scoreEvent.SourceNotes))
        {
            updatedVerdicts.Remove(releasedNote);
        }

        verdicts = updatedVerdicts;
        UpdateExpectedNotes();
    }

    public void ReleaseAll()
    {
        if (matchedMidiNumbers.Count == 0 || stepIndex >= steps.Count)
        {
            return;
        }

        var updatedVerdicts = verdicts.ToDictionary();
        foreach (ScoreNote releasedNote in steps[stepIndex].Events
            .Where(scoreEvent => matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
            .SelectMany(scoreEvent => scoreEvent.SourceNotes))
        {
            updatedVerdicts.Remove(releasedNote);
        }

        verdicts = updatedVerdicts;
        matchedMidiNumbers.Clear();
        UpdateExpectedNotes();
    }

    private void UpdateExpectedNotes()
    {
        expectedNotes = stepIndex >= steps.Count
            ? new HashSet<ScoreNote>()
            : steps[stepIndex].Events
                .Where(scoreEvent => !matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
                .Select(scoreEvent => scoreEvent.SourceNotes[0])
                .ToHashSet();
    }

    public readonly record struct CheckResult(bool IsCorrect, bool DidAdvance, bool IsComplete);

    private sealed record Step(double OnsetBeats, IReadOnlyList<ScoreEvent> Events);
}
