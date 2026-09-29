namespace PianoMapper.Music;

public static class SightReadingExerciseComposer
{
    private const int BeatsPerMeasure = 4;
    private const int DefaultTempoBeatsPerMinute = 120;

    /// <summary>Naturals, steps and thirds only: the tightest curriculum stage.</summary>
    private const int MaxDiatonicLeapStepsAndThirds = 2;

    /// <summary>Steps, thirds, and occasional wider skips up to a fifth.</summary>
    private const int MaxDiatonicLeapWithOccasionalSkips = 4;

    /// <summary>Wide enough to cross from the ledger extremes back to the staff in one prompt.</summary>
    private const int MaxDiatonicLeapForLedgerLines = 7;

    private static readonly NoteValue HalfNote = new(2);
    private static readonly NoteValue QuarterNote = new(4);
    private static readonly NoteValue DottedQuarterNote = new(4, dots: 1);
    private static readonly NoteValue EighthNote = new(8);

    /// <summary>Circle-of-fifths sharp order: the letter that gets sharped first, second, and so on.</summary>
    private static readonly NoteLetter[] SharpKeyOrder =
        [NoteLetter.F, NoteLetter.C, NoteLetter.G, NoteLetter.D, NoteLetter.A, NoteLetter.E, NoteLetter.B];

    /// <summary>Circle-of-fifths flat order: the letter that gets flatted first, second, and so on.</summary>
    private static readonly NoteLetter[] FlatKeyOrder =
        [NoteLetter.B, NoteLetter.E, NoteLetter.A, NoteLetter.D, NoteLetter.G, NoteLetter.C, NoteLetter.F];

    /// <summary>
    /// Composes a new exercise score. <paramref name="pitchWeights"/> is an optional, explicit per-pitch adaptive
    /// weight (typically derived from local mastery history by the caller — this method never reads any history or
    /// other external state itself): a pitch missing from the dictionary, or the dictionary being null or empty,
    /// gets the neutral weight of 1.0, which reproduces the exact same balanced selection as if no weights were
    /// given at all. A pitch weighted above 1.0 is treated as needing more repetition (e.g. a weak/low-mastery
    /// pitch) and is favored for later, adaptive picks — but every pitch in the palette is still guaranteed to
    /// appear once before any pitch repeats, regardless of weighting; see <see cref="ComposePitches"/>. Not applied
    /// to <see cref="SightReadingPresetId.Chords"/>, which selects whole triads rather than individual pitches.
    /// </summary>
    public static Score Compose(
        SightReadingExerciseOptions options,
        Random random,
        IReadOnlyDictionary<Pitch, double>? pitchWeights = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PromptCount);

        bool usesVariableRhythm = options.RhythmPreset != SightReadingRhythmPreset.Fixed &&
            options.PresetId != SightReadingPresetId.Chords &&
            !options.IsGrandStaff;
        if (usesVariableRhythm)
        {
            return ComposeVariableRhythmScore(options, random, pitchWeights);
        }

        if (options.PromptCount % BeatsPerMeasure != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.PromptCount,
                "Prompt count must be a multiple of the fixed measure length.");
        }

        int measureCount = options.PromptCount / BeatsPerMeasure;
        (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths) composition =
            options.PresetId == SightReadingPresetId.Chords
                ? ComposeChordPrompts(options, random)
                : ComposeSingleNotePrompts(options, random, pitchWeights);

        var measureNotes = new List<ScoreNote>[measureCount];
        for (int index = 0; index < measureNotes.Length; index++)
        {
            measureNotes[index] = [];
        }

        for (int promptIndex = 0; promptIndex < composition.Prompts.Count; promptIndex++)
        {
            int measureIndex = promptIndex / BeatsPerMeasure;
            int beatIndex = promptIndex % BeatsPerMeasure;
            foreach ((Pitch pitch, Staff staff) in composition.Prompts[promptIndex])
            {
                measureNotes[measureIndex].Add(new ScoreNote(pitch, QuarterNote, measureIndex, beatIndex, staff));
            }
        }

        ScoreMeasure[] measures = measureNotes
            .Select(notes => new ScoreMeasure(notes.ToArray(), []))
            .ToArray();
        string staffLabel = options.IsGrandStaff ? "Grand staff" : GetStaffName(options.Staff);
        return new Score(
            $"{staffLabel} {GetPresetName(options.PresetId)} note reading",
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            new Tempo(DefaultTempoBeatsPerMinute),
            composition.KeyFifths,
            measures);
    }

    private static (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths)
        ComposeSingleNotePrompts(
            SightReadingExerciseOptions options,
            Random random,
            IReadOnlyDictionary<Pitch, double>? pitchWeights)
    {
        if (!options.IsGrandStaff)
        {
            PresetPalette palette = BuildPresetPalette(options.Staff, options.PresetId);
            IReadOnlyList<IReadOnlyList<(Pitch, Staff)>> prompts =
                ComposePitches(palette, options.PromptCount, random, pitchWeights)
                    .Select(pitch => (IReadOnlyList<(Pitch, Staff)>)[(pitch, options.Staff)])
                    .ToArray();
            return (prompts, palette.KeyFifths);
        }

        PresetPalette treble = BuildPresetPalette(Staff.Treble, options.PresetId);
        PresetPalette bass = BuildPresetPalette(Staff.Bass, options.PresetId);
        int treblePromptCount = (options.PromptCount + 1) / 2;
        int bassPromptCount = options.PromptCount / 2;
        Pitch[] treblePitches = ComposePitches(treble, treblePromptCount, random, pitchWeights);
        Pitch[] bassPitches = ComposePitches(bass, bassPromptCount, random, pitchWeights);

        var combined = new IReadOnlyList<(Pitch, Staff)>[options.PromptCount];
        for (int index = 0; index < options.PromptCount; index++)
        {
            combined[index] = index % 2 == 0
                ? [(treblePitches[index / 2], Staff.Treble)]
                : [(bassPitches[index / 2], Staff.Bass)];
        }

        return (combined, treble.KeyFifths);
    }

    private static readonly int[][] TriadScaleDegreeOffsets = [[0, 2, 4], [3, 5, 7], [4, 6, 8]];

    private static (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths)
        ComposeChordPrompts(SightReadingExerciseOptions options, Random random)
    {
        Staff staff = options.Staff;
        Pitch[] scale = BuildNaturalRange(NoteLetter.C, GetStaffBaseOctave(staff), count: 15);
        Pitch[][] triads = TriadScaleDegreeOffsets
            .Select(offsets => offsets.Select(offset => scale[offset]).ToArray())
            .ToArray();

        var usageCounts = new[] { 0, 0, 0 };
        var prompts = new IReadOnlyList<(Pitch, Staff)>[options.PromptCount];
        int? previousTriadIndex = null;
        for (int index = 0; index < options.PromptCount; index++)
        {
            int[] candidateIndexes = Enumerable.Range(0, triads.Length)
                .Where(triadIndex => previousTriadIndex is null || triadIndex != previousTriadIndex.Value)
                .ToArray();
            int minimumUsage = candidateIndexes.Min(triadIndex => usageCounts[triadIndex]);
            int[] leastUsed = candidateIndexes.Where(triadIndex => usageCounts[triadIndex] == minimumUsage).ToArray();
            int chosenTriadIndex = leastUsed[random.Next(leastUsed.Length)];

            usageCounts[chosenTriadIndex]++;
            prompts[index] = triads[chosenTriadIndex].Select(pitch => (pitch, staff)).ToArray();
            previousTriadIndex = chosenTriadIndex;
        }

        return (prompts, KeyFifths: 0);
    }

    // Each pattern is one measure's worth of note/rest events, in order, with beat totals verified by tests
    // against MusicalTime.GetBeats. Beam states are written explicitly per pattern rather than derived, since
    // deriving Begin/Continue/End from adjacency is exactly the kind of rendering-adjacent logic this repo has
    // repeatedly gotten subtly wrong (see .agents/lessons.md) — explicit is safer than clever here.
    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> BasicRhythmPatterns =
    [
        [new(false, QuarterNote), new(false, QuarterNote), new(false, QuarterNote), new(false, QuarterNote)],
        [new(false, HalfNote), new(false, QuarterNote), new(false, QuarterNote)],
        [new(false, QuarterNote), new(false, QuarterNote), new(false, HalfNote)],
        [new(false, HalfNote), new(false, HalfNote)],
        [
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End),
            new(false, QuarterNote), new(false, QuarterNote), new(false, QuarterNote),
        ],
        [
            new(false, QuarterNote), new(false, QuarterNote),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End),
            new(false, QuarterNote),
        ],
        [new(false, QuarterNote), new(true, QuarterNote), new(false, QuarterNote), new(false, QuarterNote)],
    ];

    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> CompoundRhythmPatterns =
    [
        [new(false, DottedQuarterNote), new(false, DottedQuarterNote)],
        [
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.Continue),
            new(false, EighthNote, BeamState.End), new(false, DottedQuarterNote),
        ],
        [
            new(false, DottedQuarterNote),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.Continue),
            new(false, EighthNote, BeamState.End),
        ],
        [
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.Continue),
            new(false, EighthNote, BeamState.End),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.Continue),
            new(false, EighthNote, BeamState.End),
        ],
    ];

    /// <summary>
    /// Builds measures from a fixed catalog of rhythmic patterns (4/4 for <see cref="SightReadingRhythmPreset.Basic"/>,
    /// 6/8 for <see cref="SightReadingRhythmPreset.Compound"/>), adding whole measures (never a partial one, so
    /// every measure always has the meter's exact beat total) until at least <see cref="SightReadingExerciseOptions.PromptCount"/>
    /// note prompts exist — rests don't count as prompts, so the final note count can exceed the request slightly.
    /// Not supported together with <see cref="SightReadingPresetId.Chords"/> or grand staff (the caller routes
    /// around this method in both cases): splitting or aligning chords/dual-staff measures across variable-length
    /// rhythmic patterns is a materially different problem this task doesn't attempt to solve blind.
    /// </summary>
    private static Score ComposeVariableRhythmScore(
        SightReadingExerciseOptions options,
        Random random,
        IReadOnlyDictionary<Pitch, double>? pitchWeights)
    {
        TimeSignature timeSignature = options.RhythmPreset == SightReadingRhythmPreset.Compound
            ? new TimeSignature(6, EighthNote)
            : new TimeSignature(BeatsPerMeasure, QuarterNote);
        IReadOnlyList<IReadOnlyList<RhythmEvent>> patternCatalog =
            options.RhythmPreset == SightReadingRhythmPreset.Compound
                ? CompoundRhythmPatterns
                : BasicRhythmPatterns;

        var measurePatterns = new List<IReadOnlyList<RhythmEvent>>();
        var patternUsageCounts = new int[patternCatalog.Count];
        int noteCount = 0;
        int? previousPatternIndex = null;
        while (noteCount < options.PromptCount)
        {
            int[] candidateIndexes = Enumerable.Range(0, patternCatalog.Count)
                .Where(patternIndex => previousPatternIndex is null || patternIndex != previousPatternIndex.Value)
                .ToArray();
            int minimumUsage = candidateIndexes.Min(patternIndex => patternUsageCounts[patternIndex]);
            int[] leastUsed = candidateIndexes
                .Where(patternIndex => patternUsageCounts[patternIndex] == minimumUsage)
                .ToArray();
            int chosenIndex = leastUsed[random.Next(leastUsed.Length)];

            patternUsageCounts[chosenIndex]++;
            measurePatterns.Add(patternCatalog[chosenIndex]);
            noteCount += patternCatalog[chosenIndex].Count(rhythmEvent => !rhythmEvent.IsRest);
            previousPatternIndex = chosenIndex;
        }

        PresetPalette palette = BuildPresetPalette(options.Staff, options.PresetId);
        Pitch[] pitches = ComposePitches(palette, noteCount, random, pitchWeights);

        var measures = new ScoreMeasure[measurePatterns.Count];
        int pitchIndex = 0;
        for (int measureIndex = 0; measureIndex < measurePatterns.Count; measureIndex++)
        {
            var notes = new List<ScoreNote>();
            var rests = new List<ScoreRest>();
            double beatOffset = 0;
            foreach (RhythmEvent rhythmEvent in measurePatterns[measureIndex])
            {
                if (rhythmEvent.IsRest)
                {
                    rests.Add(new ScoreRest(rhythmEvent.Value, measureIndex, beatOffset, options.Staff));
                }
                else
                {
                    notes.Add(new ScoreNote(
                        pitches[pitchIndex],
                        rhythmEvent.Value,
                        measureIndex,
                        beatOffset,
                        options.Staff,
                        BeamState: rhythmEvent.Beam));
                    pitchIndex++;
                }

                beatOffset += MusicalTime.GetBeats(rhythmEvent.Value, timeSignature);
            }

            measures[measureIndex] = new ScoreMeasure(notes.ToArray(), rests.ToArray());
        }

        string rhythmLabel = options.RhythmPreset == SightReadingRhythmPreset.Compound
            ? "compound rhythm"
            : "basic rhythm";
        return new Score(
            $"{GetStaffName(options.Staff)} {GetPresetName(options.PresetId)} {rhythmLabel} note reading",
            timeSignature,
            new Tempo(DefaultTempoBeatsPerMinute),
            palette.KeyFifths,
            measures);
    }

    /// <summary>
    /// Compacts a set of missed prompt note groups (each group is one prompt's expected source notes, in original
    /// onset order) into a new, valid score of sequential fixed quarter notes. Chord membership is preserved: every
    /// note in a group shares the group's new onset. The resulting score is otherwise unrelated to
    /// <paramref name="originalScore"/> except for its time signature, tempo, and key.
    /// </summary>
    public static Score ComposeFromMissedPrompts(
        Score originalScore,
        IReadOnlyList<IReadOnlyList<ScoreNote>> missedPromptGroups)
    {
        ArgumentNullException.ThrowIfNull(originalScore);
        ArgumentNullException.ThrowIfNull(missedPromptGroups);
        if (missedPromptGroups.Count == 0)
        {
            throw new ArgumentException("At least one missed prompt is required.", nameof(missedPromptGroups));
        }

        int measureCount = ((missedPromptGroups.Count - 1) / BeatsPerMeasure) + 1;
        var measureNotes = new List<ScoreNote>[measureCount];
        for (int index = 0; index < measureNotes.Length; index++)
        {
            measureNotes[index] = [];
        }

        for (int promptIndex = 0; promptIndex < missedPromptGroups.Count; promptIndex++)
        {
            int measureIndex = promptIndex / BeatsPerMeasure;
            int beatIndex = promptIndex % BeatsPerMeasure;
            foreach (ScoreNote sourceNote in missedPromptGroups[promptIndex])
            {
                measureNotes[measureIndex].Add(new ScoreNote(
                    sourceNote.Pitch,
                    QuarterNote,
                    measureIndex,
                    beatIndex,
                    sourceNote.Staff));
            }
        }

        ScoreMeasure[] measures = measureNotes
            .Select(notes => new ScoreMeasure(notes.ToArray(), []))
            .ToArray();
        return new Score(
            $"{originalScore.Title} (missed notes)",
            originalScore.TimeSignature,
            originalScore.Tempo,
            originalScore.KeyFifths,
            measures);
    }

    private static PresetPalette BuildPresetPalette(Staff staff, SightReadingPresetId presetId) => presetId switch
    {
        SightReadingPresetId.FiveNote => new PresetPalette(
            BuildNaturalRange(NoteLetter.C, GetStaffBaseOctave(staff), count: 5),
            MaxDiatonicLeapStepsAndThirds,
            RequiredGroups: [],
            KeyFifths: 0),
        SightReadingPresetId.OneOctave => new PresetPalette(
            BuildNaturalRange(NoteLetter.C, GetStaffBaseOctave(staff), count: 8),
            MaxDiatonicLeapWithOccasionalSkips,
            RequiredGroups: [],
            KeyFifths: 0),
        SightReadingPresetId.LedgerLines => BuildLedgerLinesPalette(staff),
        SightReadingPresetId.GMajor => BuildKeySignaturePalette(NoteLetter.G, keyFifths: 1, staff),
        SightReadingPresetId.FMajor => BuildKeySignaturePalette(NoteLetter.F, keyFifths: -1, staff),
        _ => throw new ArgumentOutOfRangeException(nameof(presetId), presetId, "Unsupported preset."),
    };

    private static PresetPalette BuildLedgerLinesPalette(Staff staff)
    {
        (NoteLetter startLetter, int startOctave, Pitch staffBottom, Pitch staffTop) = staff switch
        {
            Staff.Treble => (NoteLetter.A, 3, new Pitch(NoteLetter.E, 0, 4), new Pitch(NoteLetter.F, 0, 5)),
            Staff.Bass => (NoteLetter.C, 2, new Pitch(NoteLetter.G, 0, 2), new Pitch(NoteLetter.A, 0, 3)),
            _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
        };
        Pitch[] palette = BuildNaturalRange(startLetter, startOctave, count: 17);
        Pitch[] belowStaff = palette.Where(pitch => pitch.DiatonicIndex < staffBottom.DiatonicIndex).ToArray();
        Pitch[] aboveStaff = palette.Where(pitch => pitch.DiatonicIndex > staffTop.DiatonicIndex).ToArray();
        return new PresetPalette(palette, MaxDiatonicLeapForLedgerLines, [belowStaff, aboveStaff], KeyFifths: 0);
    }

    private static PresetPalette BuildKeySignaturePalette(NoteLetter tonicLetter, int keyFifths, Staff staff)
    {
        int startOctave = staff switch
        {
            Staff.Treble => 4,
            Staff.Bass => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
        };
        Pitch[] naturalRange = BuildNaturalRange(tonicLetter, startOctave, count: 8);
        Pitch[] scale = naturalRange
            .Select(pitch => new Pitch(
                pitch.Letter,
                GetKeySignatureAlter(pitch.Letter, keyFifths),
                pitch.Octave))
            .ToArray();
        return new PresetPalette(scale, MaxDiatonicLeapWithOccasionalSkips, RequiredGroups: [], keyFifths);
    }

    /// <summary>
    /// The alteration a key signature with <paramref name="keyFifths"/> sharps (positive) or flats (negative)
    /// implies for <paramref name="letter"/>, so a note that matches its key signature can be spelled without a
    /// redundant written accidental (the existing rendering policy in <c>GrandStaffSceneBuilder.GetScoreAccidentalGlyph</c>
    /// already suppresses an accidental exactly when the written <see cref="Pitch.Alter"/> matches this).
    /// </summary>
    private static int GetKeySignatureAlter(NoteLetter letter, int keyFifths)
    {
        NoteLetter[] order = keyFifths >= 0 ? SharpKeyOrder : FlatKeyOrder;
        int index = Array.IndexOf(order, letter);
        return index >= 0 && index < Math.Abs(keyFifths) ? Math.Sign(keyFifths) : 0;
    }

    private static int GetStaffBaseOctave(Staff staff) => staff switch
    {
        Staff.Treble => 4,
        Staff.Bass => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
    };

    private static Pitch[] BuildNaturalRange(NoteLetter startLetter, int startOctave, int count)
    {
        var pitches = new Pitch[count];
        int letterIndex = (int)startLetter;
        int octave = startOctave;
        for (int index = 0; index < count; index++)
        {
            pitches[index] = new Pitch((NoteLetter)letterIndex, 0, octave);
            letterIndex++;
            if (letterIndex == 7)
            {
                letterIndex = 0;
                octave++;
            }
        }

        return pitches;
    }

    /// <summary>
    /// Builds a prompt sequence that never repeats a pitch immediately, never leaps more than
    /// <see cref="PresetPalette.MaxDiatonicLeap"/> diatonic steps between consecutive prompts, guarantees at least
    /// one pitch from each of <see cref="PresetPalette.RequiredGroups"/> appears (e.g. one ledger prompt below the
    /// staff and one above), and otherwise favors whichever valid pitch has appeared least often so far — or, when
    /// <paramref name="pitchWeights"/> is given, least often *relative to its weight* (see
    /// <see cref="ChooseWeightedLeastUsedCandidate"/>). Required coverage is guaranteed by construction (a forced
    /// pick once too few slots remain to fit it later), not by retrying, so this always terminates in exactly
    /// <paramref name="pitchCount"/> steps.
    /// </summary>
    private static Pitch[] ComposePitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<Pitch, double>? pitchWeights = null)
    {
        IReadOnlyList<Pitch> palette = presetPalette.Pitches;
        var usageCounts = palette.ToDictionary(pitch => pitch, _ => 0);
        var unmetGroups = new List<IReadOnlyList<Pitch>>(presetPalette.RequiredGroups);
        var selected = new List<Pitch>(pitchCount);
        Pitch? previous = null;

        for (int index = 0; index < pitchCount; index++)
        {
            int remainingSlotsAfterThis = pitchCount - index - 1;
            Pitch[] candidates = GetLeapConstrainedCandidates(palette, previous, presetPalette.MaxDiatonicLeap);
            Pitch chosen = unmetGroups.Count > 0 && remainingSlotsAfterThis < unmetGroups.Count
                ? ChooseForcedRequiredPitch(unmetGroups[0], candidates, random)
                : pitchWeights is null
                    ? ChooseLeastUsedCandidate(candidates, usageCounts, random)
                    : ChooseWeightedLeastUsedCandidate(candidates, usageCounts, pitchWeights, random);

            unmetGroups.RemoveAll(group => group.Contains(chosen));
            usageCounts[chosen]++;
            selected.Add(chosen);
            previous = chosen;
        }

        return selected.ToArray();
    }

    private static Pitch[] GetLeapConstrainedCandidates(IReadOnlyList<Pitch> palette, Pitch? previous, int maxLeap)
    {
        Pitch[] candidates = palette
            .Where(pitch => previous is null ||
                (pitch != previous.Value && Math.Abs(pitch.DiatonicIndex - previous.Value.DiatonicIndex) <= maxLeap))
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : palette.Where(pitch => previous is null || pitch != previous.Value).ToArray();
    }

    private static Pitch ChooseForcedRequiredPitch(IReadOnlyList<Pitch> group, Pitch[] candidates, Random random)
    {
        foreach (Pitch candidate in candidates)
        {
            if (group.Contains(candidate))
            {
                return candidate;
            }
        }

        return group[random.Next(group.Count)];
    }

    private static Pitch ChooseLeastUsedCandidate(
        Pitch[] candidates,
        IReadOnlyDictionary<Pitch, int> usageCounts,
        Random random)
    {
        int minimumUsage = candidates.Min(candidate => usageCounts[candidate]);
        Pitch[] leastUsed = candidates.Where(candidate => usageCounts[candidate] == minimumUsage).ToArray();
        return leastUsed[random.Next(leastUsed.Length)];
    }

    /// <summary>
    /// Same least-used selection as <see cref="ChooseLeastUsedCandidate"/>, but usage is divided by each
    /// candidate's weight (missing from <paramref name="pitchWeights"/> defaults to the neutral weight of 1.0)
    /// before comparing. Every candidate starts at a weighted usage of exactly 0 regardless of weight, so the very
    /// first appearance of each pitch is unaffected by weighting (the coverage guarantee holds); only once a
    /// candidate has been used at least once does a higher weight make it look less-used than an equally-raw-used,
    /// lower-weighted candidate, biasing subsequent picks toward it. A small epsilon avoids floating-point division
    /// making two candidates that are mathematically tied fail an exact equality check.
    /// </summary>
    private static Pitch ChooseWeightedLeastUsedCandidate(
        Pitch[] candidates,
        IReadOnlyDictionary<Pitch, int> usageCounts,
        IReadOnlyDictionary<Pitch, double> pitchWeights,
        Random random)
    {
        double GetWeightedUsage(Pitch candidate) =>
            usageCounts[candidate] / pitchWeights.GetValueOrDefault(candidate, 1.0);

        double minimumWeightedUsage = candidates.Min(GetWeightedUsage);
        Pitch[] leastUsed = candidates
            .Where(candidate => GetWeightedUsage(candidate) <= minimumWeightedUsage + WeightedUsageTieEpsilon)
            .ToArray();
        return leastUsed[random.Next(leastUsed.Length)];
    }

    private const double WeightedUsageTieEpsilon = 1e-6;

    private static string GetStaffName(Staff staff) => staff switch
    {
        Staff.Treble => "Treble",
        Staff.Bass => "Bass",
        _ => throw new ArgumentOutOfRangeException(nameof(staff), staff, "Unsupported staff."),
    };

    private static string GetPresetName(SightReadingPresetId presetId) => presetId switch
    {
        SightReadingPresetId.FiveNote => "five-note",
        SightReadingPresetId.OneOctave => "one-octave",
        SightReadingPresetId.LedgerLines => "ledger-lines",
        SightReadingPresetId.GMajor => "G major",
        SightReadingPresetId.FMajor => "F major",
        SightReadingPresetId.Chords => "chords",
        _ => throw new ArgumentOutOfRangeException(nameof(presetId), presetId, "Unsupported preset."),
    };

    /// <summary>
    /// A preset's pitch palette, its maximum allowed diatonic leap between consecutive prompts, any groups of
    /// pitches that must each contribute at least one appearance (e.g. ledger lines requires one prompt below the
    /// staff and one above), and the key signature its pitches are spelled against.
    /// </summary>
    private sealed record PresetPalette(
        IReadOnlyList<Pitch> Pitches,
        int MaxDiatonicLeap,
        IReadOnlyList<IReadOnlyList<Pitch>> RequiredGroups,
        int KeyFifths);

    /// <summary>One event (a note to be pitched, or a rest) within a rhythm pattern's fixed measure template.</summary>
    private sealed record RhythmEvent(bool IsRest, NoteValue Value, BeamState Beam = BeamState.None);
}
