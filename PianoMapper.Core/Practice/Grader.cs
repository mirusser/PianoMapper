using PianoMapper.Music;

namespace PianoMapper.Practice;

public static class Grader
{
    public static GradingResult Grade(
        IReadOnlyList<ScoreEvent> expectedEvents,
        Tempo tempo,
        IReadOnlyList<PerformedNote> performedNotes,
        TimeSpan practiceAnchor,
        TimeSpan evaluationTime,
        GradingOptions? options = null)
    {
        options ??= new GradingOptions();
        var matchedPerformed = new bool[performedNotes.Count];
        var matches = new PerformedNote?[expectedEvents.Count];

        for (int expectedIndex = 0; expectedIndex < expectedEvents.Count; expectedIndex++)
        {
            int performedIndex = FindNearestCandidate(
                expectedEvents[expectedIndex],
                tempo,
                performedNotes,
                matchedPerformed,
                practiceAnchor,
                options.OnsetTolerance,
                requireMatchingPitch: true);
            if (performedIndex >= 0)
            {
                matchedPerformed[performedIndex] = true;
                matches[expectedIndex] = performedNotes[performedIndex];
            }
        }

        for (int expectedIndex = 0; expectedIndex < expectedEvents.Count; expectedIndex++)
        {
            if (matches[expectedIndex] is not null)
            {
                continue;
            }

            int performedIndex = FindNearestCandidate(
                expectedEvents[expectedIndex],
                tempo,
                performedNotes,
                matchedPerformed,
                practiceAnchor,
                options.OnsetTolerance,
                requireMatchingPitch: false);
            if (performedIndex >= 0)
            {
                matchedPerformed[performedIndex] = true;
                matches[expectedIndex] = performedNotes[performedIndex];
            }
        }

        var gradedEvents = new List<GradedEvent>(expectedEvents.Count + performedNotes.Count);
        for (int index = 0; index < expectedEvents.Count; index++)
        {
            var expected = expectedEvents[index];
            var performed = matches[index];
            var verdict = performed is null
                ? Verdict.Missed
                : Classify(expected, tempo, performed, practiceAnchor, evaluationTime, options);
            gradedEvents.Add(new GradedEvent(expected, performed, verdict));
        }

        for (int index = 0; index < performedNotes.Count; index++)
        {
            if (!matchedPerformed[index])
            {
                gradedEvents.Add(new GradedEvent(null, performedNotes[index], Verdict.Extra));
            }
        }

        var counts = Enum.GetValues<Verdict>().ToDictionary(verdict => verdict, _ => 0);
        foreach (var gradedEvent in gradedEvents)
        {
            counts[gradedEvent.Verdict]++;
        }

        int correctCount = counts[Verdict.Correct];
        double accuracy = expectedEvents.Count == 0 ? 100.0 : 100.0 * correctCount / expectedEvents.Count;
        return new GradingResult(gradedEvents, new GradingSummary(accuracy, counts));
    }

    private static int FindNearestCandidate(
        ScoreEvent expected,
        Tempo tempo,
        IReadOnlyList<PerformedNote> performedNotes,
        IReadOnlyList<bool> matchedPerformed,
        TimeSpan practiceAnchor,
        TimeSpan onsetTolerance,
        bool requireMatchingPitch)
    {
        TimeSpan expectedOnset = practiceAnchor + MusicalTime.BeatsToDuration(expected.OnsetBeats, tempo);
        int nearestIndex = -1;
        TimeSpan nearestDistance = TimeSpan.MaxValue;

        for (int index = 0; index < performedNotes.Count; index++)
        {
            var performed = performedNotes[index];
            if (matchedPerformed[index] ||
                requireMatchingPitch && performed.Pitch.MidiNumber != expected.Pitch.MidiNumber)
            {
                continue;
            }

            TimeSpan distance = (performed.StartTime - expectedOnset).Duration();
            if (distance <= onsetTolerance && distance < nearestDistance)
            {
                nearestIndex = index;
                nearestDistance = distance;
            }
        }

        return nearestIndex;
    }

    private static Verdict Classify(
        ScoreEvent expected,
        Tempo tempo,
        PerformedNote performed,
        TimeSpan practiceAnchor,
        TimeSpan evaluationTime,
        GradingOptions options)
    {
        if (performed.Pitch.MidiNumber != expected.Pitch.MidiNumber)
        {
            return Verdict.WrongPitch;
        }

        TimeSpan expectedOnset = practiceAnchor + MusicalTime.BeatsToDuration(expected.OnsetBeats, tempo);
        TimeSpan onsetDeviation = performed.StartTime - expectedOnset;
        if (onsetDeviation.Duration() > options.OnTimeTolerance && onsetDeviation < TimeSpan.Zero)
        {
            return Verdict.Early;
        }

        if (onsetDeviation > options.OnTimeTolerance)
        {
            return Verdict.Late;
        }

        TimeSpan expectedDuration = MusicalTime.BeatsToDuration(expected.DurationBeats, tempo);
        TimeSpan performedEnd = performed.ReleaseTime ?? evaluationTime;
        double durationRatio = Math.Max(0.0, (performedEnd - performed.StartTime).TotalSeconds) / expectedDuration.TotalSeconds;
        if (durationRatio < options.MinimumDurationRatio)
        {
            return Verdict.TooShort;
        }

        if (durationRatio > options.MaximumDurationRatio)
        {
            return Verdict.TooLong;
        }

        return Verdict.Correct;
    }
}
