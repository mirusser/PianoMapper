namespace PianoMapper.Music;

public static class ScoreFingeringGenerator
{
    private const int FingerCount = 5;
    private const double ChordShapeWeight = 4;
    private const double BoundaryFingerWeight = 0.8;
    private const double HandMovementWeight = 0.45;
    private const double ThumbCrossingMovementWeight = 0.08;
    private const double RestTransitionWeight = 0.2;
    private const double PhraseBreakBeats = 1;
    private const double SamePitchFingerChangeCost = 2;
    private const double SameFingerMoveCost = 0.45;
    private const double ThumbCrossingCost = 0.15;
    private const double OtherFingerCrossingCost = 5;
    private static readonly double[] RightHandFingerOffsets = [0, 1.05, 2.05, 3.05, 4.4];
    private static readonly double[] KeyOffsetsByPitchClass = [0, 0.55, 1, 1.55, 2, 3, 3.55, 4, 4.55, 5, 5.55, 6];

    public static Score Generate(Score score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var generatedFingerings = new Dictionary<(int MeasureIndex, int NoteIndex), int>();
        GenerateHand(score, Staff.Treble, generatedFingerings);
        GenerateHand(score, Staff.Bass, generatedFingerings);

        ScoreMeasure[] measures = score.Measures
            .Select((measure, measureIndex) => measure with
            {
                Notes = measure.Notes
                    .Select((note, noteIndex) => ApplyFingering(
                        note,
                        generatedFingerings[(measureIndex, noteIndex)]))
                    .ToArray(),
            })
            .ToArray();
        return score with { Measures = measures };
    }

    private static void GenerateHand(
        Score score,
        Staff hand,
        IDictionary<(int MeasureIndex, int NoteIndex), int> generatedFingerings)
    {
        IReadOnlyList<NoteGroup> groups = CreateGroups(score, hand);
        if (groups.Count == 0)
        {
            return;
        }

        var candidatesByGroup = groups
            .Select(group => CreateCandidates(group, hand))
            .ToArray();
        var pathLayers = new PathNode[groups.Count][];
        pathLayers[0] = candidatesByGroup[0]
            .Select((candidate, candidateIndex) => new PathNode(
                candidate.Cost + GetBoundaryCost(
                    groups,
                    candidatesByGroup,
                    groupIndex: 0,
                    candidateIndex,
                    isStart: true,
                    hand),
                PreviousCandidateIndex: -1))
            .ToArray();

        for (int groupIndex = 1; groupIndex < groups.Count; groupIndex++)
        {
            IReadOnlyList<FingeringCandidate> previousCandidates = candidatesByGroup[groupIndex - 1];
            IReadOnlyList<FingeringCandidate> candidates = candidatesByGroup[groupIndex];
            pathLayers[groupIndex] = new PathNode[candidates.Count];
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                double bestCost = double.PositiveInfinity;
                int bestPreviousIndex = -1;
                for (int previousIndex = 0; previousIndex < previousCandidates.Count; previousIndex++)
                {
                    double cost = pathLayers[groupIndex - 1][previousIndex].Cost +
                        candidates[candidateIndex].Cost +
                        GetTransitionCost(
                            groups[groupIndex - 1],
                            previousCandidates[previousIndex],
                            groups[groupIndex],
                            candidates[candidateIndex],
                            hand);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestPreviousIndex = previousIndex;
                    }
                }

                pathLayers[groupIndex][candidateIndex] = new PathNode(bestCost, bestPreviousIndex);
            }
        }

        int lastGroupIndex = groups.Count - 1;
        int selectedCandidateIndex = Enumerable.Range(0, candidatesByGroup[lastGroupIndex].Count)
            .MinBy(candidateIndex =>
                pathLayers[lastGroupIndex][candidateIndex].Cost +
                GetBoundaryCost(
                    groups,
                    candidatesByGroup,
                    lastGroupIndex,
                    candidateIndex,
                    isStart: false,
                    hand));

        for (int groupIndex = lastGroupIndex; groupIndex >= 0; groupIndex--)
        {
            NoteGroup group = groups[groupIndex];
            FingeringCandidate candidate = candidatesByGroup[groupIndex][selectedCandidateIndex];
            for (int pitchIndex = 0; pitchIndex < group.Pitches.Count; pitchIndex++)
            {
                foreach (NoteLocation location in group.Pitches[pitchIndex].Locations)
                {
                    generatedFingerings[(location.MeasureIndex, location.NoteIndex)] = candidate.Fingers[pitchIndex];
                }
            }

            selectedCandidateIndex = pathLayers[groupIndex][selectedCandidateIndex].PreviousCandidateIndex;
        }
    }

    private static IReadOnlyList<NoteGroup> CreateGroups(Score score, Staff hand)
    {
        var notes = new List<NoteLocation>();
        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            ScoreMeasure measure = score.Measures[measureIndex];
            for (int noteIndex = 0; noteIndex < measure.Notes.Count; noteIndex++)
            {
                ScoreNote note = measure.Notes[noteIndex];
                if (note.Staff is not Staff.Treble and not Staff.Bass)
                {
                    throw new InvalidOperationException("Every score note must have a valid hand assignment.");
                }

                if (note.Staff != hand)
                {
                    continue;
                }

                double onsetBeats = (note.MeasureIndex * score.TimeSignature.Numerator) + note.BeatOffset;
                double durationBeats = MusicalTime.GetBeats(note.NoteValue, score.TimeSignature);
                notes.Add(new NoteLocation(measureIndex, noteIndex, note, onsetBeats, onsetBeats + durationBeats));
            }
        }

        return notes
            .OrderBy(note => note.OnsetBeats)
            .ThenBy(note => note.Note.Pitch.MidiNumber)
            .ThenBy(note => note.MeasureIndex)
            .ThenBy(note => note.NoteIndex)
            .GroupBy(note => note.OnsetBeats)
            .Select(group => CreateGroup(group, hand))
            .ToArray();
    }

    private static NoteGroup CreateGroup(IEnumerable<NoteLocation> notes, Staff hand)
    {
        NoteLocation[] locations = notes.ToArray();
        PitchGroup[] pitches = locations
            .GroupBy(location => location.Note.Pitch.MidiNumber)
            .OrderBy(group => group.Key)
            .Select(group => new PitchGroup(
                GetKeyPosition(group.Key),
                group.ToArray()))
            .ToArray();
        if (pitches.Length > FingerCount)
        {
            string handName = hand == Staff.Treble ? "right hand" : "left hand";
            throw new InvalidOperationException(
                $"Cannot generate fingering for a {handName} chord with more than five distinct keys.");
        }

        return new NoteGroup(
            locations[0].OnsetBeats,
            locations.Max(location => location.EndBeats),
            pitches);
    }

    private static IReadOnlyList<FingeringCandidate> CreateCandidates(NoteGroup group, Staff hand)
    {
        var candidates = new List<FingeringCandidate>();
        var selectedFingers = new int[group.Pitches.Count];
        AddCandidates(group, hand, candidates, selectedFingers, depth: 0, nextFinger: 1);
        return candidates;
    }

    private static void AddCandidates(
        NoteGroup group,
        Staff hand,
        ICollection<FingeringCandidate> candidates,
        int[] selectedFingers,
        int depth,
        int nextFinger)
    {
        if (depth == selectedFingers.Length)
        {
            int[] fingers = hand == Staff.Treble
                ? selectedFingers.ToArray()
                : selectedFingers.Reverse().ToArray();
            candidates.Add(new FingeringCandidate(fingers, GetChordCost(group, fingers, hand)));
            return;
        }

        int remainingFingers = selectedFingers.Length - depth;
        int lastFinger = FingerCount - remainingFingers + 1;
        for (int finger = nextFinger; finger <= lastFinger; finger++)
        {
            selectedFingers[depth] = finger;
            AddCandidates(group, hand, candidates, selectedFingers, depth + 1, finger + 1);
        }
    }

    private static double GetChordCost(NoteGroup group, IReadOnlyList<int> fingers, Staff hand)
    {
        double[] handPositions = Enumerable.Range(0, group.Pitches.Count)
            .Select(index => GetHandPosition(group.Pitches[index].KeyPosition, fingers[index], hand))
            .ToArray();
        double averagePosition = handPositions.Average();
        double shapeCost = handPositions.Sum(position => Math.Pow(position - averagePosition, 2)) * ChordShapeWeight;
        double fingerCost = fingers.Sum(finger => finger switch
        {
            4 => 0.12,
            5 => 0.04,
            _ => 0,
        });
        return shapeCost + fingerCost;
    }

    private static double GetTransitionCost(
        NoteGroup previousGroup,
        FingeringCandidate previous,
        NoteGroup currentGroup,
        FingeringCandidate current,
        Staff hand)
    {
        bool hasThumbCrossing = HasThumbCrossing(previousGroup, previous, currentGroup, current, hand);
        double movementWeight = hasThumbCrossing
            ? ThumbCrossingMovementWeight
            : HandMovementWeight;
        double previousPosition = GetAverageHandPosition(previousGroup, previous, hand);
        double currentPosition = GetAverageHandPosition(currentGroup, current, hand);
        double movementCost = Math.Pow(currentPosition - previousPosition, 2) * movementWeight;
        double noteTransitionCost = GetNoteTransitionCost(previousGroup, previous, currentGroup, current, hand);
        double restGap = Math.Max(0, currentGroup.OnsetBeats - previousGroup.EndBeats);
        double continuityWeight = restGap >= PhraseBreakBeats ? RestTransitionWeight : 1;
        return (movementCost + noteTransitionCost) * continuityWeight;
    }

    private static double GetNoteTransitionCost(
        NoteGroup previousGroup,
        FingeringCandidate previous,
        NoteGroup currentGroup,
        FingeringCandidate current,
        Staff hand)
    {
        double totalCost = 0;
        for (int currentIndex = 0; currentIndex < currentGroup.Pitches.Count; currentIndex++)
        {
            int previousIndex = FindNearestPitchIndex(
                previousGroup,
                currentGroup.Pitches[currentIndex].KeyPosition);
            double pitchDelta = currentGroup.Pitches[currentIndex].KeyPosition -
                previousGroup.Pitches[previousIndex].KeyPosition;
            int previousFinger = previous.Fingers[previousIndex];
            int currentFinger = current.Fingers[currentIndex];
            if (Math.Abs(pitchDelta) < double.Epsilon)
            {
                totalCost += previousFinger == currentFinger ? 0 : SamePitchFingerChangeCost;
                continue;
            }

            int effectivePreviousFinger = GetEffectiveFinger(previousFinger, hand);
            int effectiveCurrentFinger = GetEffectiveFinger(currentFinger, hand);
            int fingerDelta = effectiveCurrentFinger - effectivePreviousFinger;
            if (fingerDelta == 0)
            {
                totalCost += SameFingerMoveCost + (Math.Abs(pitchDelta) * 0.08);
                continue;
            }

            if (Math.Sign(pitchDelta) != Math.Sign(fingerDelta))
            {
                totalCost += previousFinger == 1 || currentFinger == 1
                    ? ThumbCrossingCost
                    : OtherFingerCrossingCost;
            }
        }

        return totalCost / currentGroup.Pitches.Count;
    }

    private static bool HasThumbCrossing(
        NoteGroup previousGroup,
        FingeringCandidate previous,
        NoteGroup currentGroup,
        FingeringCandidate current,
        Staff hand)
    {
        for (int currentIndex = 0; currentIndex < currentGroup.Pitches.Count; currentIndex++)
        {
            int previousIndex = FindNearestPitchIndex(
                previousGroup,
                currentGroup.Pitches[currentIndex].KeyPosition);
            double pitchDelta = currentGroup.Pitches[currentIndex].KeyPosition -
                previousGroup.Pitches[previousIndex].KeyPosition;
            int fingerDelta = GetEffectiveFinger(current.Fingers[currentIndex], hand) -
                GetEffectiveFinger(previous.Fingers[previousIndex], hand);
            if (pitchDelta != 0 &&
                fingerDelta != 0 &&
                Math.Sign(pitchDelta) != Math.Sign(fingerDelta) &&
                (previous.Fingers[previousIndex] == 1 || current.Fingers[currentIndex] == 1))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindNearestPitchIndex(NoteGroup group, double keyPosition)
    {
        int nearestIndex = 0;
        double nearestDistance = double.PositiveInfinity;
        for (int index = 0; index < group.Pitches.Count; index++)
        {
            double distance = Math.Abs(group.Pitches[index].KeyPosition - keyPosition);
            if (distance < nearestDistance)
            {
                nearestIndex = index;
                nearestDistance = distance;
            }
        }

        return nearestIndex;
    }

    private static double GetBoundaryCost(
        IReadOnlyList<NoteGroup> groups,
        IReadOnlyList<FingeringCandidate>[] candidatesByGroup,
        int groupIndex,
        int candidateIndex,
        bool isStart,
        Staff hand)
    {
        if (groups.Count < 2 || groups[groupIndex].Pitches.Count != 1)
        {
            return 0;
        }

        int adjacentGroupIndex = isStart ? groupIndex + 1 : groupIndex - 1;
        double direction = GetAveragePitchPosition(groups[isStart ? adjacentGroupIndex : groupIndex]) -
            GetAveragePitchPosition(groups[isStart ? groupIndex : adjacentGroupIndex]);
        if (direction == 0)
        {
            return 0;
        }

        int expectedEffectiveFinger = isStart == (direction > 0) ? 1 : FingerCount;
        int actualEffectiveFinger = GetEffectiveFinger(
            candidatesByGroup[groupIndex][candidateIndex].Fingers[0],
            hand);
        return Math.Abs(expectedEffectiveFinger - actualEffectiveFinger) * BoundaryFingerWeight;
    }

    private static double GetAveragePitchPosition(NoteGroup group) =>
        group.Pitches.Average(pitch => pitch.KeyPosition);

    private static double GetAverageHandPosition(
        NoteGroup group,
        FingeringCandidate candidate,
        Staff hand) =>
        Enumerable.Range(0, group.Pitches.Count)
            .Average(index => GetHandPosition(
                group.Pitches[index].KeyPosition,
                candidate.Fingers[index],
                hand));

    private static double GetHandPosition(double keyPosition, int finger, Staff hand)
    {
        double fingerOffset = RightHandFingerOffsets[finger - 1];
        return hand == Staff.Treble
            ? keyPosition - fingerOffset
            : keyPosition + fingerOffset;
    }

    private static int GetEffectiveFinger(int finger, Staff hand) =>
        hand == Staff.Treble ? finger : FingerCount + 1 - finger;

    private static double GetKeyPosition(int midiNumber)
    {
        int octave = (int)Math.Floor(midiNumber / 12d);
        int pitchClass = midiNumber - (octave * 12);
        return (octave * 7) + KeyOffsetsByPitchClass[pitchClass];
    }

    private static ScoreNote ApplyFingering(ScoreNote note, int fingerNumber) =>
        note with
        {
            Fingering = note.Fingering is { } existing
                ? existing with { Number = fingerNumber }
                : new ScoreFingering(fingerNumber),
        };

    private sealed record NoteLocation(
        int MeasureIndex,
        int NoteIndex,
        ScoreNote Note,
        double OnsetBeats,
        double EndBeats);

    private sealed record PitchGroup(
        double KeyPosition,
        IReadOnlyList<NoteLocation> Locations);

    private sealed record NoteGroup(
        double OnsetBeats,
        double EndBeats,
        IReadOnlyList<PitchGroup> Pitches);

    private sealed record FingeringCandidate(
        IReadOnlyList<int> Fingers,
        double Cost);

    private readonly record struct PathNode(
        double Cost,
        int PreviousCandidateIndex);
}
