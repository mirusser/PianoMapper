namespace PianoMapper.Music;

public static class ScoreFingeringGenerator
{
    private const int FingerCount = 5;
    private const double Epsilon = 0.000001;
    private const double ChordShapeWeight = 0.35;
    private const double BoundaryFingerWeight = 0.35;
    private const double HandMovementWeight = 0.12;
    private const double ThumbCrossingMovementWeight = 0.01;
    private const double RestTransitionWeight = 0.28;
    private const double PhraseBreakBeats = 1.5;
    private const double StretchCostWeight = 0.5;

    /// <summary>
    /// What one thumb pass (the thumb under a finger going away from it, or a finger over the thumb coming toward it)
    /// costs whatever the distance or the tempo, as a change of hand position. The squared movement term alone is
    /// convex, so it makes several short passes cheaper than one conventional pass and a long scale comes out as
    /// 1 2 1 2 1 2; a fixed cost per pass makes fewer, longer hand positions win.
    /// </summary>
    private const double ThumbPassCost = 0.35;

    /// <summary>The extra cost of a thumb pass under or over the index or little finger instead of the middle or ring finger.</summary>
    private const double AwkwardThumbPassCost = 0.3;

    private static readonly double[] rightHandFingerOffsets = [0, 1.05, 2.05, 3.05, 4.4];
    private static readonly double[] keyOffsetsByPitchClass = [0, 0.55, 1, 1.55, 2, 3, 3.55, 4, 4.55, 5, 5.55, 6];

    public static Score Generate(Score score) => Generate(score, new ScoreFingeringGenerationOptions());

    public static Score Generate(Score score, ScoreFingeringProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Generate(score, new ScoreFingeringGenerationOptions(profile));
    }

    public static Score Generate(Score score, ScoreFingeringGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(options);
        return GenerateAlternatives(score, options).BestScore;
    }

    public static ScoreFingeringGenerationResult GenerateAlternatives(
        Score score,
        ScoreFingeringGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(score);
        options ??= new ScoreFingeringGenerationOptions();
        ValidateHands(score);
        IReadOnlyDictionary<ScoreFingeringNoteAddress, int> locks = ValidateLocks(score, options.Locks);
        IReadOnlyList<HandPath> right = GenerateHand(score, Staff.Treble, options, locks);
        IReadOnlyList<HandPath> left = GenerateHand(score, Staff.Bass, options, locks);
        WholePath[] paths = right
            .SelectMany(rightPath => left.Select(leftPath => Combine(rightPath, leftPath)))
            .OrderBy(path => path.Cost)
            .ThenBy(path => path.Signature, StringComparer.Ordinal)
            .GroupBy(path => path.Signature, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(options.MaximumAlternatives)
            .ToArray();
        ScoreFingeringAlternative[] alternatives = paths
            .Select((path, index) => new ScoreFingeringAlternative(
                Apply(score, path.Fingers),
                path.Cost,
                Explain(path, paths[0], index)))
            .ToArray();
        return new ScoreFingeringGenerationResult(alternatives[0].Score, alternatives);
    }

    private static WholePath Combine(HandPath right, HandPath left)
    {
        var fingers = new Dictionary<ScoreFingeringNoteAddress, int>(right.Fingers);
        foreach ((ScoreFingeringNoteAddress address, int finger) in left.Fingers)
        {
            fingers.Add(address, finger);
        }

        return new WholePath(fingers, right.Cost + left.Cost, right.StretchCost + left.StretchCost, Signature(fingers));
    }

    private static string Explain(WholePath path, WholePath best, int index)
    {
        double stretch = path.StretchCost;
        double other = path.Cost - path.StretchCost;
        if (index == 0)
        {
            return $"Lowest modeled cost ({path.Cost:0.###}): stretch beyond the comfortable reach {stretch:0.###}, " +
                $"hand shape, movement and finger changes {other:0.###}.";
        }

        return $"Modeled cost {path.Cost - best.Cost:+0.###;-0.###;0} against the preferred fingering: " +
            $"stretch beyond the comfortable reach {stretch - best.StretchCost:+0.###;-0.###;0}, " +
            $"hand shape, movement and finger changes {other - (best.Cost - best.StretchCost):+0.###;-0.###;0}.";
    }

    private static IReadOnlyList<HandPath> GenerateHand(
        Score score,
        Staff hand,
        ScoreFingeringGenerationOptions options,
        IReadOnlyDictionary<ScoreFingeringNoteAddress, int> locks)
    {
        IReadOnlyList<PhysicalNote> notes = CreatePhysicalNotes(score, hand, locks);
        if (notes.Count == 0)
        {
            return [new HandPath(new Dictionary<ScoreFingeringNoteAddress, int>(), 0, 0)];
        }

        IReadOnlyList<Event> events = CreateEvents(notes);
        FingeringHandProfile profile = options.Profile.GetHand(hand);
        var states = new List<State> { State.Empty };
        long sequence = 0;
        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            Event current = events[eventIndex];
            var nextByState = new Dictionary<string, List<State>>(StringComparer.Ordinal);
            var candidatesByHeld = new Dictionary<string, CandidateSet>(StringComparer.Ordinal);
            Infeasibility? infeasibility = null;
            foreach (State state in states)
            {
                Active[] held = state.Active.Where(active => active.Note.End > current.Onset + Epsilon).ToArray();
                string heldKey = ActiveKey(held);
                if (!candidatesByHeld.TryGetValue(heldKey, out CandidateSet? candidateSet))
                {
                    candidateSet = CreateCandidates(current, held, hand, profile);
                    candidatesByHeld.Add(heldKey, candidateSet);
                    infeasibility ??= candidateSet.Reason;
                }

                foreach (Candidate candidate in candidateSet.Candidates)
                {
                    double cost = state.Cost + candidate.Cost + (state.Last is null
                        ? BoundaryCost(events, eventIndex, candidate.Attack, isStart: true, hand)
                        : TransitionCost(state.Last, candidate.Attack, score.Tempo, hand));
                    AddState(
                        nextByState,
                        new State(
                            candidate.Active,
                            candidate.Attack,
                            state,
                            candidate.New,
                            cost,
                            state.StretchCost + candidate.StretchCost,
                            sequence++),
                        options.MaximumAlternatives);
                }
            }

            if (nextByState.Count == 0)
            {
                throw Infeasible(
                    infeasibility?.Address ?? current.Pitches[0].Notes[0].Locations[0].Address,
                    hand,
                    infeasibility?.Reason ?? "No assignment satisfies the configured reach limits.");
            }

            states = nextByState.Values.SelectMany(paths => paths).OrderBy(state => state.Sequence).ToList();
        }

        return states
            .Select(state => ToHandPath(notes, state, BoundaryCost(events, events.Count - 1, state.Last!, isStart: false, hand)))
            .OrderBy(path => path.Cost)
            .ThenBy(path => Signature(path.Fingers), StringComparer.Ordinal)
            .GroupBy(path => Signature(path.Fingers), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(options.MaximumAlternatives)
            .ToArray();
    }

    private static HandPath ToHandPath(IReadOnlyList<PhysicalNote> notes, State state, double boundaryCost)
    {
        var fingerByNote = new Dictionary<int, int>();
        for (State? step = state; step is not null; step = step.Parent)
        {
            foreach (Active added in step.Added)
            {
                fingerByNote[added.Note.Id] = added.Finger;
            }
        }

        var fingers = new Dictionary<ScoreFingeringNoteAddress, int>();
        foreach (PhysicalNote note in notes)
        {
            foreach (Location location in note.Locations)
            {
                fingers.Add(location.Address, fingerByNote[note.Id]);
            }
        }

        return new HandPath(fingers, state.Cost + boundaryCost, state.StretchCost);
    }

    /// <summary>
    /// Keeps the cheapest few paths that end in the same held fingers and last attack: everything the rest of the
    /// search depends on is in that state, so a dearer path into it can never become the best or a next-best path.
    /// Equal costs keep the path that was found first, which makes the result deterministic.
    /// </summary>
    private static void AddState(IDictionary<string, List<State>> states, State state, int maximumAlternatives)
    {
        string key = ActiveKey(state.Active) + "|" + AttackSignature(state.Last);
        if (!states.TryGetValue(key, out List<State>? paths))
        {
            paths = [];
            states.Add(key, paths);
        }

        paths.Add(state);
        paths.Sort(static (first, second) => first.Cost != second.Cost
            ? first.Cost.CompareTo(second.Cost)
            : first.Sequence.CompareTo(second.Sequence));
        if (paths.Count > maximumAlternatives)
        {
            paths.RemoveAt(paths.Count - 1);
        }
    }

    private static CandidateSet CreateCandidates(Event current, IReadOnlyList<Active> held, Staff hand, FingeringHandProfile profile)
    {
        // A key that is attacked again while still held is released and re-struck by the finger already on it, the
        // same way a doubled unison shares one finger; the earlier press no longer constrains the others.
        Active[] sustained = held.Where(active => current.Pitches.All(pitch => pitch.Midi != active.Note.Midi)).ToArray();
        int sustainedKeys = sustained.Select(active => active.Note.Midi).Distinct().Count();
        if (sustainedKeys + current.Pitches.Count > FingerCount)
        {
            PitchGroup excess = current.Pitches[Math.Max(0, FingerCount - sustainedKeys)];
            return new CandidateSet(
                [],
                new Infeasibility(
                    excess.Notes[0].Locations[0].Address,
                    $"The {HandSide(hand)} hand needs more than five distinct keys at once."));
        }

        var candidates = new List<Candidate>();
        var selected = new int[current.Pitches.Count];
        Infeasibility? reason = null;
        AddCandidates(current, held, sustained, hand, profile, candidates, selected, 0, ref reason);
        return new CandidateSet(candidates, reason);
    }

    private static void AddCandidates(
        Event current,
        IReadOnlyList<Active> held,
        IReadOnlyList<Active> sustained,
        Staff hand,
        FingeringHandProfile profile,
        ICollection<Candidate> candidates,
        int[] selected,
        int depth,
        ref Infeasibility? reason)
    {
        if (depth == selected.Length)
        {
            Active[] added = current.Pitches.SelectMany((pitch, index) =>
                pitch.Notes.Select(note => new Active(note, selected[index]))).ToArray();
            Active[] active = sustained.Concat(added).ToArray();
            Key[] keys = Keys(active);
            ScoreFingeringNoteAddress firstAttack = current.Pitches[0].Notes[0].Locations[0].Address;
            if (!HasOrderedFingers(keys, hand))
            {
                reason ??= new Infeasibility(firstAttack, "The required fingers cross while notes are held.");
                return;
            }

            if (!TryGetReachCost(keys, profile, out double stretchCost))
            {
                reason ??= new Infeasibility(firstAttack, "The configured reach limit is exceeded while the notes are held.");
                return;
            }

            Attack attack = new(
                current.Onset,
                current.Pitches.Select((pitch, index) => new Key(pitch.Midi, pitch.Position, selected[index])).ToArray(),
                current.Pitches.Max(pitch => pitch.Notes.Max(note => note.End)));
            candidates.Add(new Candidate(active, added, attack, ShapeCost(keys, hand) + stretchCost + KeyColorCost(attack), stretchCost));
            return;
        }

        PitchGroup pitch = current.Pitches[depth];
        int? locked = LockedFinger(pitch);
        int? holdingFinger = held.FirstOrDefault(active => active.Note.Midi == pitch.Midi)?.Finger;
        IEnumerable<int> fingers = holdingFinger is int restruck
            ? [restruck]
            : locked is int fixedFinger ? [fixedFinger] : Enumerable.Range(1, FingerCount);
        foreach (int finger in fingers)
        {
            if (locked is int lockedFinger && lockedFinger != finger)
            {
                reason ??= new Infeasibility(
                    LockAddress(pitch),
                    $"Finger {lockedFinger} is locked here but finger {finger} already holds this key.");
                continue;
            }

            if (selected.Take(depth).Contains(finger) || sustained.Any(active => active.Finger == finger))
            {
                if (locked == finger)
                {
                    reason ??= new Infeasibility(
                        LockAddress(pitch),
                        $"Finger {finger} is already held on another key.");
                }

                continue;
            }

            selected[depth] = finger;
            AddCandidates(current, held, sustained, hand, profile, candidates, selected, depth + 1, ref reason);
        }
    }

    private static ScoreFingeringNoteAddress LockAddress(PitchGroup pitch) =>
        pitch.Notes.FirstOrDefault(note => note.LockAddress is not null)?.LockAddress ??
        pitch.Notes[0].Locations[0].Address;

    private static int? LockedFinger(PitchGroup pitch)
    {
        int? finger = null;
        foreach (PhysicalNote note in pitch.Notes)
        {
            if (note.LockedFinger is not int candidate)
            {
                continue;
            }

            if (finger is int existing && existing != candidate)
            {
                throw new ScoreFingeringGenerationException(note.LockAddress, "Simultaneous unison notes have contradictory fingering locks.");
            }

            finger = candidate;
        }

        return finger;
    }

    private static bool HasOrderedFingers(IReadOnlyList<Key> keys, Staff hand)
    {
        for (int index = 1; index < keys.Count; index++)
        {
            if (hand == Staff.Treble ? keys[index - 1].Finger >= keys[index].Finger : keys[index - 1].Finger <= keys[index].Finger)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetReachCost(IReadOnlyList<Key> keys, FingeringHandProfile profile, out double cost)
    {
        cost = 0;
        for (int first = 0; first < keys.Count; first++)
        {
            for (int second = first + 1; second < keys.Count; second++)
            {
                FingeringReach reach = profile.GetReach(keys[first].Finger, keys[second].Finger);
                double span = keys[second].Position - keys[first].Position;
                if (span > reach.MaximumWhiteKeySpan + Epsilon)
                {
                    return false;
                }

                cost += Math.Pow(Math.Max(0, span - reach.ComfortableWhiteKeySpan), 2) * StretchCostWeight;
            }
        }

        return true;
    }

    private static Key[] Keys(IReadOnlyList<Active> active) => active.GroupBy(entry => entry.Note.Midi)
        .Select(group => new Key(group.Key, group.First().Note.Position, group.First().Finger))
        .OrderBy(key => key.Midi)
        .ToArray();

    private static double ShapeCost(IReadOnlyList<Key> keys, Staff hand)
    {
        double[] positions = keys.Select(key => HandPosition(key.Position, key.Finger, hand)).ToArray();
        double mean = positions.Average();
        return positions.Sum(position => Math.Pow(position - mean, 2)) * ChordShapeWeight +
            keys.Sum(key => key.Finger is 4 ? 0.12 : key.Finger == 5 ? 0.04 : 0);
    }

    private static double KeyColorCost(Attack attack) => attack.Keys.Where(key => key.Finger == 1 && IsBlack(key.Midi))
        .Sum(_ => attack.Keys.Count > 1 ? 0.025 : 0.1);

    private static double TransitionCost(Attack previous, Attack current, Tempo tempo, Staff hand)
    {
        double thumbPass = current.Keys.Max(key => ThumbPassCostOf(Nearest(previous.Keys, key.Position), key, hand));
        double handShift = AverageHandPosition(current.Keys, hand) - AverageHandPosition(previous.Keys, hand);
        double movement = handShift * handShift * (thumbPass > 0 ? ThumbCrossingMovementWeight : HandMovementWeight);
        double availableSeconds = Math.Max(Epsilon, (current.Onset - previous.Onset) * 60 / tempo.BeatsPerMinute);
        double timeWeight = Math.Clamp(0.5 / availableSeconds, 0.5, 3);
        double noteCost = current.Keys.Sum(key => NoteTransitionCost(Nearest(previous.Keys, key.Position), key, hand)) / current.Keys.Count;
        double restWeight = current.Onset - previous.End >= PhraseBreakBeats ? RestTransitionWeight : 1;
        return (((movement + noteCost) * timeWeight) + thumbPass) * restWeight;
    }

    /// <summary>The cost of the thumb passing from <paramref name="previous"/> to <paramref name="current"/>, or 0 when it does not.</summary>
    private static double ThumbPassCostOf(Key previous, Key current, Staff hand)
    {
        if (current.Position == previous.Position ||
            current.Finger == previous.Finger ||
            (current.Finger != 1 && previous.Finger != 1) ||
            Math.Sign(current.Position - previous.Position) == Math.Sign(Effective(current.Finger, hand) - Effective(previous.Finger, hand)))
        {
            return 0;
        }

        return ThumbPassCost + (Math.Max(previous.Finger, current.Finger) is 2 or 5 ? AwkwardThumbPassCost : 0);
    }

    private static double NoteTransitionCost(Key previous, Key current, Staff hand)
    {
        double pitchDelta = current.Position - previous.Position;
        if (Math.Abs(pitchDelta) < Epsilon)
        {
            return previous.Finger == current.Finger ? 0 : 0.8;
        }

        int fingerDelta = Effective(current.Finger, hand) - Effective(previous.Finger, hand);
        if (fingerDelta == 0)
        {
            return 0.45 + (Math.Abs(pitchDelta) * 0.08);
        }

        return Math.Sign(pitchDelta) != Math.Sign(fingerDelta)
            ? previous.Finger == 1 || current.Finger == 1 ? 0.08 : 0.65
            : 0;
    }

    private static double BoundaryCost(IReadOnlyList<Event> events, int index, Attack attack, bool isStart, Staff hand)
    {
        if (events.Count < 2 || attack.Keys.Count != 1)
        {
            return 0;
        }

        int adjacent = isStart ? index + 1 : index - 1;
        double direction = AveragePitch(events[isStart ? adjacent : index]) - AveragePitch(events[isStart ? index : adjacent]);
        if (Math.Abs(direction) < Epsilon)
        {
            return 0;
        }

        int expected = isStart == (direction > 0) ? 1 : FingerCount;
        return Math.Abs(expected - Effective(attack.Keys[0].Finger, hand)) * BoundaryFingerWeight;
    }

    private static IReadOnlyList<Event> CreateEvents(IReadOnlyList<PhysicalNote> notes) => notes
        .OrderBy(note => note.Onset).ThenBy(note => note.Midi).ThenBy(note => note.Id)
        .GroupBy(note => note.Onset)
        .Select(group => new Event(
            group.Key,
            group.GroupBy(note => note.Midi).OrderBy(pitch => pitch.Key)
                .Select(pitch => new PitchGroup(pitch.Key, Position(pitch.Key), pitch.OrderBy(note => note.Id).ToArray()))
                .ToArray()))
        .ToArray();

    private static IReadOnlyList<PhysicalNote> CreatePhysicalNotes(
        Score score,
        Staff hand,
        IReadOnlyDictionary<ScoreFingeringNoteAddress, int> locks)
    {
        Location[] locations = Locations(score, hand);
        var next = new Dictionary<int, int>();
        var targets = new HashSet<int>();
        foreach (Location source in locations.Where(location => location.Note.TiesToNext))
        {
            Location? target = locations.Where(candidate => !targets.Contains(candidate.Id) &&
                    candidate.Midi == source.Midi && Math.Abs(candidate.Onset - source.End) < Epsilon)
                .OrderBy(candidate => candidate.Measure).ThenBy(candidate => candidate.Index).FirstOrDefault();
            if (target is not null)
            {
                next.Add(source.Id, target.Id);
                targets.Add(target.Id);
            }
        }

        var byId = locations.ToDictionary(location => location.Id);
        var physical = new List<PhysicalNote>();
        int id = 0;
        foreach (Location root in locations.Where(location => !targets.Contains(location.Id)))
        {
            var chain = new List<Location> { root };
            Location current = root;
            while (next.TryGetValue(current.Id, out int nextId))
            {
                current = byId[nextId];
                chain.Add(current);
            }

            int? lockedFinger = null;
            ScoreFingeringNoteAddress? lockAddress = null;
            foreach (Location location in chain)
            {
                if (!locks.TryGetValue(location.Address, out int finger))
                {
                    continue;
                }

                if (lockedFinger is int existing && existing != finger)
                {
                    throw new ScoreFingeringGenerationException(location.Address, "A tied note is locked to conflicting fingers.");
                }

                lockedFinger = finger;
                lockAddress = location.Address;
            }

            physical.Add(new PhysicalNote(id++, root.Midi, Position(root.Midi), root.Onset, chain[^1].End, chain, lockedFinger, lockAddress));
        }

        return physical;
    }

    private static Location[] Locations(Score score, Staff hand)
    {
        var result = new List<Location>();
        int id = 0;
        for (int measure = 0; measure < score.Measures.Count; measure++)
        {
            for (int index = 0; index < score.Measures[measure].Notes.Count; index++)
            {
                ScoreNote note = score.Measures[measure].Notes[index];
                if (note.Staff == hand)
                {
                    double onset = ScoreDerivation.GetOnsetBeats(score, note);
                    result.Add(new Location(id++, measure, index, note, onset, onset + MusicalTime.GetBeats(note.NoteValue, score.TimeSignature)));
                }
            }
        }

        return result.ToArray();
    }

    private static IReadOnlyDictionary<ScoreFingeringNoteAddress, int> ValidateLocks(Score score, IReadOnlyList<ScoreFingeringLock> locks)
    {
        var result = new Dictionary<ScoreFingeringNoteAddress, int>();
        foreach (ScoreFingeringLock @lock in locks)
        {
            ScoreFingeringNoteAddress address = @lock.Address;
            if (address.MeasureIndex >= score.Measures.Count || address.NoteIndex >= score.Measures[address.MeasureIndex].Notes.Count)
            {
                throw new ScoreFingeringGenerationException(address,
                    $"The fingering lock at measure {address.MeasureIndex + 1}, note {address.NoteIndex + 1} does not identify a score note.");
            }

            result.Add(address, @lock.FingerNumber);
        }

        return result;
    }

    private static void ValidateHands(Score score)
    {
        for (int measure = 0; measure < score.Measures.Count; measure++)
        {
            for (int index = 0; index < score.Measures[measure].Notes.Count; index++)
            {
                if (score.Measures[measure].Notes[index].Staff is not Staff.Treble and not Staff.Bass)
                {
                    throw new ScoreFingeringGenerationException(new ScoreFingeringNoteAddress(measure, index),
                        "Every score note must have a valid hand assignment.");
                }
            }
        }
    }

    private static Score Apply(Score score, IReadOnlyDictionary<ScoreFingeringNoteAddress, int> fingers) => score with
    {
        Measures = score.Measures.Select((measure, measureIndex) => measure with
        {
            Notes = measure.Notes.Select((note, noteIndex) => note with
            {
                Fingering = note.Fingering is { } existing
                    ? existing with { Number = fingers[new ScoreFingeringNoteAddress(measureIndex, noteIndex)] }
                    : new ScoreFingering(fingers[new ScoreFingeringNoteAddress(measureIndex, noteIndex)]),
            }).ToArray(),
        }).ToArray(),
    };

    private static ScoreFingeringGenerationException Infeasible(ScoreFingeringNoteAddress address, Staff hand, string reason) =>
        new(address, $"Cannot generate {HandSide(hand)}-hand fingering at measure {address.MeasureIndex + 1}, note {address.NoteIndex + 1}: {reason}");

    private static string HandSide(Staff hand) => hand == Staff.Treble ? "right" : "left";
    private static double Position(int midi) => ((int)Math.Floor(midi / 12d) * 7) + keyOffsetsByPitchClass[midi % 12];
    private static bool IsBlack(int midi) => midi % 12 is 1 or 3 or 6 or 8 or 10;
    private static int Effective(int finger, Staff hand) => hand == Staff.Treble ? finger : FingerCount + 1 - finger;
    private static double HandPosition(double position, int finger, Staff hand) => hand == Staff.Treble
        ? position - rightHandFingerOffsets[finger - 1]
        : position + rightHandFingerOffsets[finger - 1];
    private static double AverageHandPosition(IReadOnlyList<Key> keys, Staff hand) => keys.Average(key => HandPosition(key.Position, key.Finger, hand));
    private static Key Nearest(IReadOnlyList<Key> keys, double position) => keys.OrderBy(key => Math.Abs(key.Position - position)).ThenBy(key => key.Midi).First();
    private static double AveragePitch(Event @event) => @event.Pitches.Average(pitch => pitch.Position);
    private static string AttackSignature(Attack? attack) => attack is null ? string.Empty : string.Join(',', attack.Keys.Select(key => $"{key.Midi}:{key.Finger}"));
    private static string ActiveKey(IReadOnlyList<Active> active) => string.Join(',', active.OrderBy(entry => entry.Note.Id).Select(entry => $"{entry.Note.Id}:{entry.Finger}"));
    private static string Signature(IReadOnlyDictionary<ScoreFingeringNoteAddress, int> fingers) => string.Join(',', fingers.OrderBy(pair => pair.Key.MeasureIndex).ThenBy(pair => pair.Key.NoteIndex).Select(pair => $"{pair.Key.MeasureIndex}:{pair.Key.NoteIndex}:{pair.Value}"));

    private sealed record Location(int Id, int Measure, int Index, ScoreNote Note, double Onset, double End)
    {
        public int Midi => Note.Pitch.MidiNumber;
        public ScoreFingeringNoteAddress Address => new(Measure, Index);
    }

    private sealed record PhysicalNote(int Id, int Midi, double Position, double Onset, double End, IReadOnlyList<Location> Locations, int? LockedFinger, ScoreFingeringNoteAddress? LockAddress);
    private sealed record PitchGroup(int Midi, double Position, IReadOnlyList<PhysicalNote> Notes) { public Pitch Pitch => Notes[0].Locations[0].Note.Pitch; }
    private sealed record Event(double Onset, IReadOnlyList<PitchGroup> Pitches);
    private sealed record Active(PhysicalNote Note, int Finger);
    private sealed record Key(int Midi, double Position, int Finger);
    private sealed record Attack(double Onset, IReadOnlyList<Key> Keys, double End);
    private sealed record Candidate(IReadOnlyList<Active> Active, IReadOnlyList<Active> New, Attack Attack, double Cost, double StretchCost);
    private sealed record Infeasibility(ScoreFingeringNoteAddress Address, string Reason);
    private sealed record CandidateSet(IReadOnlyList<Candidate> Candidates, Infeasibility? Reason);
    private sealed record State(IReadOnlyList<Active> Active, Attack? Last, State? Parent, IReadOnlyList<Active> Added, double Cost, double StretchCost, long Sequence) { public static State Empty { get; } = new([], null, null, [], 0, 0, 0); }
    private sealed record HandPath(IReadOnlyDictionary<ScoreFingeringNoteAddress, int> Fingers, double Cost, double StretchCost);
    private sealed record WholePath(IReadOnlyDictionary<ScoreFingeringNoteAddress, int> Fingers, double Cost, double StretchCost, string Signature);
}
