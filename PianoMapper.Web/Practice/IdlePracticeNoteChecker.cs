using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

internal sealed class IdlePracticeNoteChecker
{
    private IReadOnlyList<Step> steps = [];
    private IReadOnlyDictionary<ScoreNote, Verdict> verdicts = new Dictionary<ScoreNote, Verdict>();
    private IReadOnlySet<ScoreNote> expectedNotes = new HashSet<ScoreNote>();
    private readonly HashSet<int> matchedMidiNumbers = [];
    private int stepIndex;

    internal IReadOnlyDictionary<ScoreNote, Verdict> Verdicts => verdicts;

    internal IReadOnlySet<ScoreNote> ExpectedNotes => expectedNotes;

    internal double? CurrentOnsetBeats => stepIndex < steps.Count
        ? steps[stepIndex].OnsetBeats
        : null;

    internal void Reset(Score? score)
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
        UpdateExpectedNotes();
    }

    internal Result Check(Pitch pitch)
    {
        if (stepIndex >= steps.Count)
        {
            return new Result(IsCorrect: false, DidAdvance: false, IsComplete: true);
        }

        Step step = steps[stepIndex];
        ScoreEvent[] pendingEvents = step.Events
            .Where(scoreEvent => !matchedMidiNumbers.Contains(scoreEvent.Pitch.MidiNumber))
            .ToArray();
        ScoreEvent[] matchingEvents = pendingEvents
            .Where(scoreEvent => scoreEvent.Pitch.MidiNumber == pitch.MidiNumber)
            .ToArray();
        var updatedVerdicts = verdicts.ToDictionary();
        foreach (ScoreNote expectedNote in expectedNotes)
        {
            if (updatedVerdicts.GetValueOrDefault(expectedNote) == Verdict.WrongPitch)
            {
                updatedVerdicts.Remove(expectedNote);
            }
        }

        if (matchingEvents.Length == 0)
        {
            foreach (ScoreNote expectedNote in expectedNotes)
            {
                updatedVerdicts[expectedNote] = Verdict.WrongPitch;
            }

            verdicts = updatedVerdicts;
            return new Result(IsCorrect: false, DidAdvance: false, IsComplete: false);
        }

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
            stepIndex++;
            matchedMidiNumbers.Clear();
        }

        verdicts = updatedVerdicts;
        UpdateExpectedNotes();
        return new Result(
            IsCorrect: true,
            DidAdvance: didAdvance,
            IsComplete: stepIndex >= steps.Count);
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

    internal readonly record struct Result(bool IsCorrect, bool DidAdvance, bool IsComplete);

    private sealed record Step(double OnsetBeats, IReadOnlyList<ScoreEvent> Events);
}
