using PianoMapper.Practice;

namespace PianoMapper.Music;

public static class SightReadingExerciseComposer
{
    private const int BeatsPerMeasure = 4;

    /// <summary>A 6/8 pulse is a dotted quarter, which is three of the score's eighth-note beats.</summary>
    private const int EighthNoteBeatsPerCompoundPulse = 3;

    /// <summary>Naturals, steps and thirds only: the tightest curriculum stage.</summary>
    private const int MaxDiatonicLeapStepsAndThirds = 2;

    /// <summary>Steps, thirds, and occasional wider skips up to a fifth.</summary>
    private const int MaxDiatonicLeapWithOccasionalSkips = 4;

    /// <summary>Wide enough to cross from the ledger extremes back to the staff in one prompt.</summary>
    private const int MaxDiatonicLeapForLedgerLines = 7;

    /// <summary>Rhythm-only exercises repeat each staff's middle-line pitch, so reading pitch never competes with rhythm.</summary>
    private static readonly Pitch TrebleCentreLinePitch = new(NoteLetter.B, 0, 4);

    private static readonly Pitch BassCentreLinePitch = new(NoteLetter.D, 0, 3);

    private static readonly NoteValue WholeNote = new(1);
    private static readonly NoteValue DottedHalfNote = new(2, dots: 1);
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
    /// Composes a new exercise score. <paramref name="noteWeights"/> is an optional, explicit adaptive weight per
    /// (pitch, staff) — keyed by staff so a note that is weak on one clef is not boosted on the other (typically
    /// derived from local mastery history by the caller — this method never reads any history or other external
    /// state itself): a note missing from the dictionary, or the dictionary being null or empty, gets the neutral
    /// weight of 1.0, which reproduces the exact same balanced selection as if no weights were given at all. A note
    /// weighted above 1.0 is treated as needing more repetition (e.g. a weak/low-mastery note). With the default
    /// <see cref="SightReadingGenerationStrategy.CoverageFirst"/> it is favored only for later, adaptive picks, since
    /// every pitch in the palette is still guaranteed to appear once before any pitch repeats; with
    /// <see cref="SightReadingGenerationStrategy.WeaknessFirst"/> (an explicit drill) weights drive every pick; see
    /// <see cref="ComposePitches"/>. Not applied to <see cref="SightReadingPresetId.Chords"/>, which selects whole
    /// triads rather than individual pitches.
    /// </summary>
    public static Score Compose(
        SightReadingExerciseOptions options,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PromptCount);
        if (options.TempoPulsesPerMinute is { } pulses and (
            < SightReadingExerciseOptions.MinimumTempoPulsesPerMinute or
            > SightReadingExerciseOptions.MaximumTempoPulsesPerMinute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                pulses,
                "Tempo must be between " +
                $"{SightReadingExerciseOptions.MinimumTempoPulsesPerMinute} and " +
                $"{SightReadingExerciseOptions.MaximumTempoPulsesPerMinute} pulses per minute.");
        }

        if (options.Motion == SightReadingMotion.Intervallic &&
            options.IntervalSteps is < SightReadingExerciseOptions.MinimumIntervalSteps or
                > SightReadingExerciseOptions.MaximumIntervalSteps)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.IntervalSteps,
                "The interval must be between " +
                $"{SightReadingExerciseOptions.MinimumIntervalSteps} and " +
                $"{SightReadingExerciseOptions.MaximumIntervalSteps} diatonic steps.");
        }

        bool isRhythmOnly = options.Mode == NoteReadingMode.RhythmOnly;
        if (isRhythmOnly)
        {
            ValidateRhythmOnly(options);
        }

        if (options.IsHandsTogether)
        {
            ValidateHandsTogether(options);
        }

        bool usesVariableRhythm = options.RhythmPreset != SightReadingRhythmPreset.Fixed &&
            options.PresetId != SightReadingPresetId.Chords &&
            !options.IsGrandStaff;
        if (usesVariableRhythm)
        {
            return ComposeVariableRhythmScore(options, random, noteWeights);
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
            isRhythmOnly
                ? ComposeCentreLinePrompts(options)
                : options.PresetId == SightReadingPresetId.Chords
                    ? ComposeChordPrompts(options, random)
                    : ComposeSingleNotePrompts(options, random, noteWeights);

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
                measureNotes[measureIndex].Add(new ScoreNote(
                    pitch,
                    QuarterNote,
                    measureIndex,
                    beatIndex,
                    staff,
                    Accidental: GetPrintedAccidental(pitch, composition.KeyFifths)));
            }
        }

        ScoreMeasure[] measures = measureNotes
            .Select(notes => new ScoreMeasure(notes.ToArray(), []))
            .ToArray();
        string staffLabel = options.IsGrandStaff ? "Grand staff" : GetStaffName(options.Staff);
        return new Score(
            isRhythmOnly
                ? $"{staffLabel} rhythm-only note reading"
                : $"{staffLabel} {GetPresetName(options.PresetId)} note reading",
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            ResolveTempo(options, isCompound: false),
            composition.KeyFifths,
            measures);
    }

    /// <summary>
    /// Converts the learner-facing pulse tempo to the score's written-beat unit: one pulse is one quarter note in
    /// 4/4 and one dotted quarter (three eighth-note beats) in 6/8. Never touches <see cref="Random"/>.
    /// </summary>
    private static Tempo ResolveTempo(SightReadingExerciseOptions options, bool isCompound)
    {
        int pulses = options.TempoPulsesPerMinute ??
            SightReadingExerciseOptions.GetDefaultTempoPulsesPerMinute(
                isCompound ? SightReadingRhythmPreset.Compound : SightReadingRhythmPreset.Fixed);
        return new Tempo(isCompound ? pulses * EighthNoteBeatsPerCompoundPulse : pulses);
    }

    /// <summary>
    /// The pitches a single-note exercise of this staff and range draws from (what a weak-note drill is limited to).
    /// Not meaningful for <see cref="SightReadingPresetId.Chords"/>, which selects whole triads.
    /// </summary>
    public static IReadOnlyList<Pitch> GetRangePitches(Staff staff, SightReadingPresetId presetId) =>
        BuildPresetPalette(staff, presetId).Pitches;

    private static void ValidateRhythmOnly(SightReadingExerciseOptions options)
    {
        if (options.IsGrandStaff)
        {
            throw new ArgumentException(
                "Rhythm-only exercises repeat one note on one staff; a grand staff is not supported.",
                nameof(options));
        }

        if (options.PresetId == SightReadingPresetId.Chords)
        {
            throw new ArgumentException(
                "Rhythm-only exercises repeat one note on one staff; chords are not supported.",
                nameof(options));
        }
    }

    private static void ValidateHandsTogether(SightReadingExerciseOptions options)
    {
        if (!options.IsGrandStaff)
        {
            throw new ArgumentException("Hands together needs a grand staff.", nameof(options));
        }

        if (options.PresetId != SightReadingPresetId.FiveNote)
        {
            throw new ArgumentException(
                "Hands together is only supported for the five-note range, where the two hands never share a pitch.",
                nameof(options));
        }
    }

    private static Pitch GetCentreLinePitch(Staff staff) =>
        staff == Staff.Treble ? TrebleCentreLinePitch : BassCentreLinePitch;

    private static (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths)
        ComposeCentreLinePrompts(SightReadingExerciseOptions options)
    {
        Pitch pitch = GetCentreLinePitch(options.Staff);
        IReadOnlyList<IReadOnlyList<(Pitch, Staff)>> prompts = Enumerable
            .Range(0, options.PromptCount)
            .Select(_ => (IReadOnlyList<(Pitch, Staff)>)[(pitch, options.Staff)])
            .ToArray();
        return (prompts, KeyFifths: 0);
    }

    private static (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths)
        ComposeSingleNotePrompts(
            SightReadingExerciseOptions options,
            Random random,
            IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights)
    {
        int measureCount = options.PromptCount / BeatsPerMeasure;
        if (!options.IsGrandStaff)
        {
            PresetPalette palette = BuildPresetPalette(options.Staff, options.PresetId);
            int[] measureSizes = Enumerable.Repeat(BeatsPerMeasure, measureCount).ToArray();
            IReadOnlyList<IReadOnlyList<(Pitch, Staff)>> prompts =
                ComposePitches(palette, options.PromptCount, random, noteWeights, options.Staff, options, measureSizes)
                    .Select(pitch => (IReadOnlyList<(Pitch, Staff)>)[(pitch, options.Staff)])
                    .ToArray();
            return (prompts, palette.KeyFifths);
        }

        PresetPalette treble = BuildPresetPalette(Staff.Treble, options.PresetId);
        PresetPalette bass = BuildPresetPalette(Staff.Bass, options.PresetId);
        int treblePromptCount = (options.PromptCount + 1) / 2;
        int bassPromptCount = options.PromptCount / 2;
        if (options.IsHandsTogether)
        {
            // Both hands play on every prompt, each following the five-note rules on its own staff.
            int[] handMeasureSizes = Enumerable.Repeat(BeatsPerMeasure, measureCount).ToArray();
            Pitch[] trebleHand = ComposePitches(
                treble,
                options.PromptCount,
                random,
                noteWeights,
                Staff.Treble,
                options,
                handMeasureSizes);
            Pitch[] bassHand = ComposePitches(
                bass,
                options.PromptCount,
                random,
                noteWeights,
                Staff.Bass,
                options,
                handMeasureSizes);
            IReadOnlyList<IReadOnlyList<(Pitch, Staff)>> together = Enumerable
                .Range(0, options.PromptCount)
                .Select(index => (IReadOnlyList<(Pitch, Staff)>)[(trebleHand[index], Staff.Treble), (bassHand[index], Staff.Bass)])
                .ToArray();
            return (together, treble.KeyFifths);
        }

        // The two staves alternate within a measure, so each staff has half of a measure's prompts.
        int[] staffMeasureSizes = Enumerable.Repeat(BeatsPerMeasure / 2, measureCount).ToArray();
        Pitch[] treblePitches = ComposePitches(
            treble,
            treblePromptCount,
            random,
            noteWeights,
            Staff.Treble,
            options,
            staffMeasureSizes);
        Pitch[] bassPitches = ComposePitches(
            bass,
            bassPromptCount,
            random,
            noteWeights,
            Staff.Bass,
            options,
            staffMeasureSizes);

        var combined = new IReadOnlyList<(Pitch, Staff)>[options.PromptCount];
        for (int index = 0; index < options.PromptCount; index++)
        {
            combined[index] = index % 2 == 0
                ? [(treblePitches[index / 2], Staff.Treble)]
                : [(bassPitches[index / 2], Staff.Bass)];
        }

        return (combined, treble.KeyFifths);
    }

    /// <summary>
    /// The nine chord voicings, as offsets into the staff's C major scale counted from its base C (so 0, 2, 4 on the
    /// treble staff is C4, E4, G4): root position, first inversion and second inversion of the I, IV and V triads, in
    /// close position, each with its lowest note in the octave above the base C. The root positions are the ones the
    /// preset always had. A voicing that would need more than <see cref="MaximumChordLedgerLines"/> ledger lines on the
    /// staff is voiced an octave lower, see <see cref="FitChordVoicingToStaff"/>.
    /// </summary>
    private static readonly int[][] ChordVoicingScaleOffsets =
    [
        [0, 2, 4], [2, 4, 7], [4, 7, 9],
        [3, 5, 7], [5, 7, 10], [0, 3, 5],
        [4, 6, 8], [6, 8, 11], [1, 4, 6],
    ];

    /// <summary>
    /// The most ledger lines a generated chord note may need on its staff, the limit the key presets keep too. On the bass
    /// staff first-inversion V (B3 D4 G4) reached its third.
    /// </summary>
    private const int MaximumChordLedgerLines = 2;

    private static Pitch[] FitChordVoicingToStaff(Pitch[] voicing, Staff staff) =>
        voicing.Max(pitch => CountLedgerLines(pitch, staff)) > MaximumChordLedgerLines
            ? voicing.Select(pitch => new Pitch(pitch.Letter, pitch.Alter, pitch.Octave - 1)).ToArray()
            : voicing;

    /// <summary>
    /// Ledger lines a notehead needs on the staff: its lines run from E4 to F5 (treble) or G2 to A3 (bass), and every second
    /// step beyond them is one more.
    /// </summary>
    private static int CountLedgerLines(Pitch pitch, Staff staff)
    {
        (int bottomLine, int topLine) = staff == Staff.Treble
            ? (new Pitch(NoteLetter.E, 0, 4).DiatonicIndex, new Pitch(NoteLetter.F, 0, 5).DiatonicIndex)
            : (new Pitch(NoteLetter.G, 0, 2).DiatonicIndex, new Pitch(NoteLetter.A, 0, 3).DiatonicIndex);
        return pitch.DiatonicIndex < bottomLine
            ? (bottomLine - pitch.DiatonicIndex) / 2
            : pitch.DiatonicIndex > topLine
                ? (pitch.DiatonicIndex - topLine) / 2
                : 0;
    }

    private static (IReadOnlyList<IReadOnlyList<(Pitch Pitch, Staff Staff)>> Prompts, int KeyFifths)
        ComposeChordPrompts(SightReadingExerciseOptions options, Random random)
    {
        Staff staff = options.Staff;
        Pitch[] scale = BuildNaturalRange(NoteLetter.C, GetStaffBaseOctave(staff), count: 15);
        Pitch[][] triads = ChordVoicingScaleOffsets
            .Select(offsets => FitChordVoicingToStaff(offsets.Select(offset => scale[offset]).ToArray(), staff))
            .ToArray();

        var usageCounts = new int[triads.Length];
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
        // Quarter + eighth groupings fill one group of three each, so the 3 + 3 grouping is kept; the lone eighth is
        // flagged, never beamed to a quarter.
        [new(false, QuarterNote), new(false, EighthNote), new(false, QuarterNote), new(false, EighthNote)],
        [new(false, QuarterNote), new(false, EighthNote), new(false, DottedQuarterNote)],
        [new(false, DottedQuarterNote), new(false, QuarterNote), new(false, EighthNote)],
        // An eighth rest then a beamed pair of eighths fills the first group.
        [
            new(true, EighthNote),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End),
            new(false, DottedQuarterNote),
        ],
        // One long note held across both groups.
        [new(false, DottedHalfNote)],
    ];

    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> ThreeFourRhythmPatterns =
    [
        [new(false, QuarterNote), new(false, QuarterNote), new(false, QuarterNote)],
        [new(false, HalfNote), new(false, QuarterNote)],
        [new(false, QuarterNote), new(false, HalfNote)],
        [new(false, DottedHalfNote)],
        [
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End),
            new(false, QuarterNote), new(false, QuarterNote),
        ],
        [
            new(false, QuarterNote),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End),
            new(false, QuarterNote),
        ],
        [new(false, QuarterNote), new(true, QuarterNote), new(false, QuarterNote)],
    ];

    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> TwoFourRhythmPatterns =
    [
        [new(false, QuarterNote), new(false, QuarterNote)],
        [new(false, HalfNote)],
        [new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End), new(false, QuarterNote)],
        [new(false, QuarterNote), new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End)],
        [new(false, QuarterNote), new(true, QuarterNote)],
    ];

    // Syncopation: off-beat eighths, ties across the middle of the bar, and ties across the barline. A tie is written
    // as TiesToNext on the first note and IsTiedContinuation on the note that finishes it; a pattern that ends tied can
    // only be followed by one that starts with a continuation (and the reverse), see ComposeVariableRhythmScore.
    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> SyncopatedRhythmPatterns =
    [
        [
            new(false, QuarterNote), new(false, EighthNote), new(false, QuarterNote),
            new(false, EighthNote), new(false, QuarterNote),
        ],
        [
            new(false, EighthNote), new(false, QuarterNote), new(false, EighthNote),
            new(false, QuarterNote), new(false, QuarterNote),
        ],
        [
            new(false, QuarterNote), new(false, QuarterNote, TiesToNext: true),
            new(false, QuarterNote, IsTiedContinuation: true), new(false, QuarterNote),
        ],
        [
            new(false, QuarterNote), new(false, EighthNote, BeamState.Begin),
            new(false, EighthNote, BeamState.End, TiesToNext: true),
            new(false, QuarterNote, IsTiedContinuation: true), new(false, QuarterNote),
        ],
        [
            new(false, QuarterNote), new(false, QuarterNote), new(false, QuarterNote),
            new(false, EighthNote, BeamState.Begin), new(false, EighthNote, BeamState.End, TiesToNext: true),
        ],
        [new(false, HalfNote), new(false, QuarterNote), new(false, QuarterNote, TiesToNext: true)],
        [new(false, QuarterNote, IsTiedContinuation: true), new(false, QuarterNote), new(false, HalfNote)],
        [new(false, HalfNote, IsTiedContinuation: true), new(false, QuarterNote), new(false, QuarterNote)],
    ];

    private static readonly IReadOnlyList<IReadOnlyList<RhythmEvent>> ExtendedRhythmPatterns =
    [
        [new(false, DottedHalfNote), new(false, QuarterNote)],
        [new(false, QuarterNote), new(false, DottedHalfNote)],
        [new(false, WholeNote)],
        [new(false, DottedQuarterNote), new(false, EighthNote), new(false, HalfNote)],
        [new(false, QuarterNote), new(true, EighthNote), new(false, EighthNote), new(false, HalfNote)],
    ];

    /// <summary>
    /// What a variable <see cref="SightReadingRhythmPreset"/> composes with: its time signature, its measure patterns,
    /// the wording of the score title, and whether a pulse is a dotted quarter (see <see cref="ResolveTempo"/>).
    /// </summary>
    private sealed record RhythmCatalog(
        TimeSignature TimeSignature,
        IReadOnlyList<IReadOnlyList<RhythmEvent>> Patterns,
        string Label,
        bool IsCompoundPulse);

    private static RhythmCatalog GetRhythmCatalog(SightReadingRhythmPreset rhythmPreset) => rhythmPreset switch
    {
        SightReadingRhythmPreset.Basic => new RhythmCatalog(
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            BasicRhythmPatterns,
            "basic rhythm",
            IsCompoundPulse: false),
        SightReadingRhythmPreset.Compound => new RhythmCatalog(
            new TimeSignature(6, EighthNote),
            CompoundRhythmPatterns,
            "compound rhythm",
            IsCompoundPulse: true),
        SightReadingRhythmPreset.Extended => new RhythmCatalog(
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            ExtendedRhythmPatterns,
            "extended rhythm",
            IsCompoundPulse: false),
        SightReadingRhythmPreset.ThreeFour => new RhythmCatalog(
            new TimeSignature(3, QuarterNote),
            ThreeFourRhythmPatterns,
            "three-four rhythm",
            IsCompoundPulse: false),
        SightReadingRhythmPreset.TwoFour => new RhythmCatalog(
            new TimeSignature(2, QuarterNote),
            TwoFourRhythmPatterns,
            "two-four rhythm",
            IsCompoundPulse: false),
        SightReadingRhythmPreset.Syncopated => new RhythmCatalog(
            new TimeSignature(BeatsPerMeasure, QuarterNote),
            SyncopatedRhythmPatterns,
            "syncopated rhythm",
            IsCompoundPulse: false),
        _ => throw new ArgumentOutOfRangeException(nameof(rhythmPreset), rhythmPreset, "Not a variable rhythm."),
    };

    /// <summary>
    /// Builds measures from a fixed catalog of rhythmic patterns (see <see cref="GetRhythmCatalog"/>: 4/4 for
    /// <see cref="SightReadingRhythmPreset.Basic"/> and <see cref="SightReadingRhythmPreset.Extended"/>, 6/8 for
    /// <see cref="SightReadingRhythmPreset.Compound"/>, and 3/4 and 2/4), adding whole measures (never a partial one, so
    /// every measure always has the meter's exact beat total) until at least <see cref="SightReadingExerciseOptions.PromptCount"/>
    /// note prompts exist — rests don't count as prompts, so the final note count can exceed the request slightly.
    /// Not supported together with <see cref="SightReadingPresetId.Chords"/> or grand staff (the caller routes
    /// around this method in both cases): splitting or aligning chords/dual-staff measures across variable-length
    /// rhythmic patterns is a materially different problem this task doesn't attempt to solve blind.
    /// </summary>
    private static Score ComposeVariableRhythmScore(
        SightReadingExerciseOptions options,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights)
    {
        RhythmCatalog catalog = GetRhythmCatalog(options.RhythmPreset);
        TimeSignature timeSignature = catalog.TimeSignature;
        IReadOnlyList<IReadOnlyList<RhythmEvent>> patternCatalog = catalog.Patterns;

        var measurePatterns = new List<IReadOnlyList<RhythmEvent>>();
        var patternUsageCounts = new int[patternCatalog.Count];
        int noteCount = 0;
        int? previousPatternIndex = null;
        // A tie across the barline pairs two measures: the pattern that ends tied must be followed by one that starts
        // with the continuation (and no other pattern may start with one), and the exercise never stops mid-tie. For
        // catalogs without ties this changes nothing: every pattern is always a candidate, as before.
        bool previousEndsTied = false;
        while (noteCount < options.PromptCount || previousEndsTied)
        {
            int[] candidateIndexes = Enumerable.Range(0, patternCatalog.Count)
                .Where(patternIndex => (previousPatternIndex is null || patternIndex != previousPatternIndex.Value) &&
                    StartsWithTiedContinuation(patternCatalog[patternIndex]) == previousEndsTied)
                .ToArray();
            int minimumUsage = candidateIndexes.Min(patternIndex => patternUsageCounts[patternIndex]);
            int[] leastUsed = candidateIndexes
                .Where(patternIndex => patternUsageCounts[patternIndex] == minimumUsage)
                .ToArray();
            int chosenIndex = leastUsed[random.Next(leastUsed.Length)];

            patternUsageCounts[chosenIndex]++;
            measurePatterns.Add(patternCatalog[chosenIndex]);
            noteCount += patternCatalog[chosenIndex].Count(rhythmEvent => !rhythmEvent.IsRest && !rhythmEvent.IsTiedContinuation);
            previousPatternIndex = chosenIndex;
            previousEndsTied = patternCatalog[chosenIndex][^1] is { IsRest: false, TiesToNext: true };
        }

        bool isRhythmOnly = options.Mode == NoteReadingMode.RhythmOnly;
        Pitch[] pitches;
        int keyFifths;
        if (isRhythmOnly)
        {
            pitches = Enumerable.Repeat(GetCentreLinePitch(options.Staff), noteCount).ToArray();
            keyFifths = 0;
        }
        else
        {
            PresetPalette palette = BuildPresetPalette(options.Staff, options.PresetId);
            // A tied continuation takes its predecessor's pitch, so it is neither a pitch to pick nor a prompt.
            int[] measureSizes = measurePatterns
                .Select(pattern => pattern.Count(rhythmEvent => !rhythmEvent.IsRest && !rhythmEvent.IsTiedContinuation))
                .ToArray();
            bool[] measuresStartingWithTie = measurePatterns.Select(StartsWithTiedContinuation).ToArray();
            pitches = ComposePitches(
                palette,
                noteCount,
                random,
                noteWeights,
                options.Staff,
                options,
                measureSizes,
                measuresStartingWithTie);
            keyFifths = palette.KeyFifths;
        }

        var measures = new ScoreMeasure[measurePatterns.Count];
        int pitchIndex = 0;
        Pitch? previousPitch = null;
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
                    Pitch pitch = rhythmEvent.IsTiedContinuation ? previousPitch!.Value : pitches[pitchIndex++];
                    notes.Add(new ScoreNote(
                        pitch,
                        rhythmEvent.Value,
                        measureIndex,
                        beatOffset,
                        options.Staff,
                        TiesToNext: rhythmEvent.TiesToNext,
                        BeamState: rhythmEvent.Beam,
                        Accidental: GetPrintedAccidental(pitch, keyFifths)));
                    previousPitch = pitch;
                }

                beatOffset += MusicalTime.GetBeats(rhythmEvent.Value, timeSignature);
            }

            measures[measureIndex] = new ScoreMeasure(notes.ToArray(), rests.ToArray());
        }

        string rhythmLabel = catalog.Label;
        string presetLabel = isRhythmOnly ? "rhythm-only" : GetPresetName(options.PresetId);
        return new Score(
            $"{GetStaffName(options.Staff)} {presetLabel} {rhythmLabel} note reading",
            timeSignature,
            ResolveTempo(options, catalog.IsCompoundPulse),
            keyFifths,
            measures);
    }

    /// <summary>
    /// Builds a retry score for rhythm-graded exercises: every measure that contains a missed note is replayed whole
    /// and in its original order, so the rhythm that was missed (note values, rests, beams, chord members and the
    /// key) comes back with it, unlike <see cref="ComposeFromMissedPrompts"/>, which flattens prompts into quarter
    /// notes. Measure indexes are renumbered from zero; beat offsets are measure-relative and so stay valid. A tie is
    /// kept only when the measure that finishes it is replayed as well. The time signature and tempo are those of
    /// <paramref name="originalScore"/>.
    /// </summary>
    public static Score ComposeFromMissedMeasures(
        Score originalScore,
        IReadOnlyCollection<ScoreNote> missedNotes)
    {
        ArgumentNullException.ThrowIfNull(originalScore);
        ArgumentNullException.ThrowIfNull(missedNotes);
        if (missedNotes.Count == 0)
        {
            throw new ArgumentException("At least one missed note is required.", nameof(missedNotes));
        }

        int[] missedMeasureIndexes = missedNotes
            .Select(note => note.MeasureIndex)
            .Distinct()
            .Order()
            .ToArray();
        if (missedMeasureIndexes.Any(index => index < 0 || index >= originalScore.Measures.Count))
        {
            throw new ArgumentException("A missed note is not part of the score.", nameof(missedNotes));
        }

        // A tie only survives into the replay if the measure that finishes it is replayed too, so no curve is left
        // hanging from a note whose continuation is not there.
        bool ContinuationIsReplayed(ScoreNote note) =>
            ScoreDerivation.FindTieContinuation(originalScore, note) is { } continuation &&
            missedMeasureIndexes.Contains(continuation.MeasureIndex);
        ScoreMeasure[] measures = missedMeasureIndexes
            .Select((sourceIndex, replayIndex) =>
            {
                ScoreMeasure source = originalScore.Measures[sourceIndex];
                return new ScoreMeasure(
                    source.Notes
                        .Select(note => note with
                        {
                            MeasureIndex = replayIndex,
                            TiesToNext = note.TiesToNext && ContinuationIsReplayed(note),
                        })
                        .ToArray(),
                    source.Rests.Select(rest => rest with { MeasureIndex = replayIndex }).ToArray());
            })
            .ToArray();
        return originalScore with { Measures = measures };
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

        // Prompts fill measures four beats at a time. A prompt whose spelling would be misread in the measure so far
        // (a plain note after the same letter was altered, see MeasureSpelling) starts the next measure instead, so
        // the measure may end short, like the last one always could.
        var measureNotes = new List<List<ScoreNote>> { new() };
        var spelling = new MeasureSpelling(originalScore.KeyFifths);
        int beatIndex = 0;
        foreach (IReadOnlyList<ScoreNote> group in missedPromptGroups)
        {
            if (beatIndex > 0 &&
                (beatIndex == BeatsPerMeasure || group.Any(note => !spelling.Allows(note.Staff, note.Pitch))))
            {
                measureNotes.Add([]);
                spelling.StartMeasure();
                beatIndex = 0;
            }

            int measureIndex = measureNotes.Count - 1;
            // A tied pair is one prompt whose two source notes share a pitch; it comes back as one note.
            foreach (ScoreNote sourceNote in group.DistinctBy(note => (note.Pitch, note.Staff)))
            {
                spelling.Record(sourceNote.Staff, sourceNote.Pitch);
                measureNotes[measureIndex].Add(new ScoreNote(
                    sourceNote.Pitch,
                    QuarterNote,
                    measureIndex,
                    beatIndex,
                    sourceNote.Staff,
                    Accidental: sourceNote.Accidental));
            }

            beatIndex++;
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
        SightReadingPresetId.DMajor => BuildKeySignaturePalette(NoteLetter.D, keyFifths: 2, staff),
        SightReadingPresetId.BFlatMajor => BuildKeySignaturePalette(NoteLetter.B, keyFifths: -2, staff),
        // Natural minor has the key signature of its relative major (none for A minor), so it is spelled all naturals.
        SightReadingPresetId.AMinor => BuildKeySignaturePalette(NoteLetter.A, keyFifths: 0, staff),
        SightReadingPresetId.Accidentals => BuildAccidentalsPalette(staff),
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

    /// <summary>
    /// One octave of C major plus a C sharp, E flat, F sharp and B flat, so a note can be altered or plain; every note
    /// sits within the staff plus one ledger line.
    /// </summary>
    private static PresetPalette BuildAccidentalsPalette(Staff staff)
    {
        int octave = GetStaffBaseOctave(staff);
        Pitch[] altered =
        [
            new(NoteLetter.C, 1, octave),
            new(NoteLetter.E, -1, octave),
            new(NoteLetter.F, 1, octave),
            new(NoteLetter.B, -1, octave),
        ];
        Pitch[] palette = BuildNaturalRange(NoteLetter.C, octave, count: 8)
            .Concat(altered)
            .OrderBy(pitch => pitch.DiatonicIndex)
            .ThenBy(pitch => pitch.Alter)
            .ToArray();
        return new PresetPalette(
            palette,
            MaxDiatonicLeapWithOccasionalSkips,
            RequiredGroups: [],
            KeyFifths: 0,
            AlteredPitches: altered);
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

    /// <summary>
    /// The accidental printed beside a note, or null when its alteration is the key signature's own. Stating it on the
    /// note, as an imported score does, is what makes the notation spacing leave room for it; the renderer would draw
    /// the same glyph from the pitch alone.
    /// </summary>
    private static ScoreAccidental? GetPrintedAccidental(Pitch pitch, int keyFifths) =>
        pitch.Alter == GetKeySignatureAlter(pitch.Letter, keyFifths)
            ? null
            : pitch.Alter switch
            {
                0 => ScoreAccidental.Natural,
                1 => ScoreAccidental.Sharp,
                -1 => ScoreAccidental.Flat,
                2 => ScoreAccidental.DoubleSharp,
                -2 => ScoreAccidental.DoubleFlat,
                _ => null,
            };

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
    /// Picks the pitch sequence for one staff by the exercise's <see cref="SightReadingExerciseOptions.Motion"/>.
    /// Presets with required groups (ledger lines) choose their notes themselves and always use
    /// <see cref="SightReadingMotion.Random"/>, because a run of steps cannot be relied on to reach both ends of
    /// the range; see <see cref="SupportsMotion"/>. <paramref name="measureSizes"/> is how many of the returned
    /// pitches fall in each measure of this staff and <paramref name="measuresStartingWithTie"/> which measures begin
    /// with the continuation of a tie (only the accidentals preset needs the measure boundaries).
    /// </summary>
    private static Pitch[] ComposePitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights,
        Staff staff,
        SightReadingExerciseOptions options,
        IReadOnlyList<int> measureSizes,
        IReadOnlyList<bool>? measuresStartingWithTie = null)
    {
        if (presetPalette.AlteredPitches is not null)
        {
            return ComposeAccidentalPitches(
                presetPalette,
                pitchCount,
                random,
                noteWeights,
                staff,
                measureSizes,
                measuresStartingWithTie);
        }

        if (presetPalette.RequiredGroups.Count == 0)
        {
            switch (options.Motion)
            {
                case SightReadingMotion.Melodic:
                    return ComposeMelodicPitches(presetPalette, pitchCount, random);
                case SightReadingMotion.Intervallic:
                    return ComposeIntervallicPitches(presetPalette, pitchCount, random, options.IntervalSteps);
            }
        }

        return ComposeRandomPitches(presetPalette, pitchCount, random, noteWeights, staff, options.Strategy);
    }

    /// <summary>
    /// Whether a range follows the exercise's <see cref="SightReadingExerciseOptions.Motion"/>: every range of plain
    /// notes does. Chords (whole triads), ledger lines (each end of the range must appear) and accidentals (sharps and
    /// flats with per-measure spelling rules) pick their own notes.
    /// </summary>
    public static bool SupportsMotion(SightReadingPresetId presetId) =>
        presetId != SightReadingPresetId.Chords &&
        BuildPresetPalette(Staff.Treble, presetId) is { RequiredGroups.Count: 0, AlteredPitches: null };

    /// <summary>The chance that a melodic note simply repeats the one before.</summary>
    private const double MelodicRepeatChance = 0.25;

    /// <summary>The chance that a melodic run turns around instead of keeping its direction.</summary>
    private const double MelodicTurnChance = 0.25;

    /// <summary>The chance that a melodic move is a skip of a third instead of a step.</summary>
    private const double MelodicSkipChance = 0.2;

    private const int MelodicSkipSteps = 2;

    /// <summary>
    /// A melodic line over the palette: from a random start it keeps its direction most of the time, repeats a note
    /// about a quarter of the time, now and then skips a third, and turns around at either end of the palette.
    /// Never reads mastery weights (they shape <see cref="SightReadingMotion.Random"/> picks only).
    /// </summary>
    private static Pitch[] ComposeMelodicPitches(PresetPalette presetPalette, int pitchCount, Random random)
    {
        IReadOnlyList<Pitch> palette = presetPalette.Pitches;
        var selected = new Pitch[pitchCount];
        int position = random.Next(palette.Count);
        int direction = random.Next(2) == 0 ? -1 : 1;
        selected[0] = palette[position];
        for (int index = 1; index < pitchCount; index++)
        {
            if (random.NextDouble() >= MelodicRepeatChance)
            {
                if (random.NextDouble() < MelodicTurnChance)
                {
                    direction = -direction;
                }

                int size = random.NextDouble() < MelodicSkipChance ? MelodicSkipSteps : 1;
                if (position + (direction * size) < 0 || position + (direction * size) >= palette.Count)
                {
                    direction = -direction;
                }

                position += direction * size;
            }

            selected[index] = palette[position];
        }

        return selected;
    }

    /// <summary>
    /// A line whose every move is exactly <paramref name="intervalSteps"/> diatonic steps up or down, picking the
    /// direction at random among those that stay inside the palette. It starts on a note that has such a move, and
    /// the way it came is always a legal way back, so it never gets stuck.
    /// </summary>
    private static Pitch[] ComposeIntervallicPitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        int intervalSteps)
    {
        IReadOnlyList<Pitch> palette = presetPalette.Pitches;
        int[] GetTargets(int position) => new[] { position - intervalSteps, position + intervalSteps }
            .Where(target => target >= 0 && target < palette.Count)
            .ToArray();

        int[] starts = Enumerable.Range(0, palette.Count).Where(position => GetTargets(position).Length > 0).ToArray();
        int current = starts[random.Next(starts.Length)];
        var selected = new Pitch[pitchCount];
        selected[0] = palette[current];
        for (int index = 1; index < pitchCount; index++)
        {
            int[] targets = GetTargets(current);
            current = targets[random.Next(targets.Length)];
            selected[index] = palette[current];
        }

        return selected;
    }

    /// <summary>
    /// Builds a prompt sequence that never repeats a pitch immediately, never leaps more than
    /// <see cref="PresetPalette.MaxDiatonicLeap"/> diatonic steps between consecutive prompts, guarantees at least
    /// one pitch from each of <see cref="PresetPalette.RequiredGroups"/> appears (e.g. one ledger prompt below the
    /// staff and one above), and otherwise favors whichever valid pitch has appeared least often so far — or, when
    /// <paramref name="noteWeights"/> is given, least often *relative to its weight* (see
    /// <see cref="ChooseWeightedLeastUsedCandidate"/>). Required coverage is guaranteed by construction (a forced
    /// pick once too few slots remain to fit it later), not by retrying, so this always terminates in exactly
    /// <paramref name="pitchCount"/> steps. <see cref="SightReadingGenerationStrategy.WeaknessFirst"/> is handled by
    /// <see cref="ComposeWeaknessFirstPitches"/>.
    /// </summary>
    private static Pitch[] ComposeRandomPitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights,
        Staff staff,
        SightReadingGenerationStrategy strategy = SightReadingGenerationStrategy.CoverageFirst)
    {
        if (strategy == SightReadingGenerationStrategy.WeaknessFirst)
        {
            return ComposeWeaknessFirstPitches(presetPalette, pitchCount, random, noteWeights, staff);
        }

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
                : noteWeights is null
                    ? ChooseLeastUsedCandidate(candidates, usageCounts, random)
                    : ChooseWeightedLeastUsedCandidate(candidates, usageCounts, noteWeights, staff, random);

            unmetGroups.RemoveAll(group => group.Contains(chosen));
            usageCounts[chosen]++;
            selected.Add(chosen);
            previous = chosen;
        }

        return selected.ToArray();
    }

    /// <summary>The accidentals preset guarantees this many sharps and this many flats (fewer in very short sequences).</summary>
    private const int MinimumAccidentalsOfEachKind = 2;

    /// <summary>One required sharp and one required flat per this many notes, up to <see cref="MinimumAccidentalsOfEachKind"/>.</summary>
    private const int NotesPerRequiredAccidental = 4;

    /// <summary>
    /// A sequence that satisfies the spelling rules by construction can still miss the sharp/flat minimum, so it is
    /// redrawn; each draw has well over a one in ten chance, which makes running out of attempts practically
    /// impossible.
    /// </summary>
    private const int MaximumAccidentalAttempts = 200;

    /// <summary>
    /// The accidentals preset's notes for one staff. Every draw obeys the staff-position rules (no immediate repeat of
    /// a staff position, the range's leap limit) and the measure-spelling rule of
    /// <see cref="MeasureSpelling"/>, and is kept only if it holds at least two sharps and two flats (one of each when
    /// there are fewer than eight notes). Selection is the coverage-first least-used pick, weighted by mastery where
    /// weights are given; the weak-note drill does not apply to this preset (the coordinator says so).
    /// </summary>
    private static Pitch[] ComposeAccidentalPitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights,
        Staff staff,
        IReadOnlyList<int> measureSizes,
        IReadOnlyList<bool>? measuresStartingWithTie)
    {
        int requiredOfEachKind = Math.Min(MinimumAccidentalsOfEachKind, pitchCount / NotesPerRequiredAccidental);
        int[] measureOfNote = measureSizes
            .SelectMany((size, measure) => Enumerable.Repeat(measure, size))
            .ToArray();
        Pitch[] sequence = [];
        for (int attempt = 0; attempt < MaximumAccidentalAttempts; attempt++)
        {
            sequence = DrawAccidentalSequence(
                presetPalette,
                pitchCount,
                random,
                noteWeights,
                staff,
                measureOfNote,
                measuresStartingWithTie);
            if (sequence.Count(pitch => pitch.Alter > 0) >= requiredOfEachKind &&
                sequence.Count(pitch => pitch.Alter < 0) >= requiredOfEachKind)
            {
                return sequence;
            }
        }

        // Practically unreachable; the last draw still obeys every rule except the sharp/flat minimum.
        return sequence;
    }

    private static Pitch[] DrawAccidentalSequence(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights,
        Staff staff,
        int[] measureOfNote,
        IReadOnlyList<bool>? measuresStartingWithTie)
    {
        IReadOnlyList<Pitch> palette = presetPalette.Pitches;
        var usageCounts = palette.ToDictionary(pitch => pitch, _ => 0);
        var spelling = new MeasureSpelling(presetPalette.KeyFifths);
        var selected = new Pitch[pitchCount];
        for (int index = 0; index < pitchCount; index++)
        {
            if (index > 0 && MeasureOf(measureOfNote, index) != MeasureOf(measureOfNote, index - 1))
            {
                spelling.StartMeasure();
                // A measure that opens with the end of a tie shows the tied note (and its accidental) first.
                int measure = MeasureOf(measureOfNote, index);
                if (measuresStartingWithTie is not null && measure < measuresStartingWithTie.Count &&
                    measuresStartingWithTie[measure])
                {
                    spelling.Record(staff, selected[index - 1]);
                }
            }

            Pitch? previous = index == 0 ? null : selected[index - 1];
            // A staff position never repeats immediately, even in another form (C then C sharp).
            Pitch[] candidates = palette
                .Where(pitch => spelling.Allows(staff, pitch) &&
                    (previous is null ||
                        (pitch.DiatonicIndex != previous.Value.DiatonicIndex &&
                            IsLegalStep(previous.Value, pitch, presetPalette.MaxDiatonicLeap))))
                .ToArray();
            Pitch chosen = noteWeights is null
                ? ChooseLeastUsedCandidate(candidates, usageCounts, random)
                : ChooseWeightedLeastUsedCandidate(candidates, usageCounts, noteWeights, staff, random);
            spelling.Record(staff, chosen);
            usageCounts[chosen]++;
            selected[index] = chosen;
        }

        return selected;
    }

    private static int MeasureOf(int[] measureOfNote, int index) =>
        index < measureOfNote.Length ? measureOfNote[index] : measureOfNote.Length == 0 ? 0 : measureOfNote[^1];

    /// <summary>
    /// What a reader of one measure takes each letter to mean. The renderer prints an accidental only for a note that
    /// differs from the key signature and never a cautionary natural, so once a letter has been altered in a measure
    /// (on a staff), a plain or differently altered note of that letter would be misread: it must keep the
    /// alteration until the barline.
    /// </summary>
    private sealed class MeasureSpelling(int keyFifths)
    {
        private readonly Dictionary<(Staff Staff, NoteLetter Letter), int> alteredLetters = [];

        public bool Allows(Staff staff, Pitch pitch) =>
            !alteredLetters.TryGetValue((staff, pitch.Letter), out int alter) || alter == pitch.Alter;

        public void Record(Staff staff, Pitch pitch)
        {
            if (pitch.Alter != GetKeySignatureAlter(pitch.Letter, keyFifths))
            {
                alteredLetters[(staff, pitch.Letter)] = pitch.Alter;
            }
        }

        public void StartMeasure() => alteredLetters.Clear();
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
    /// candidate's weight (missing from <paramref name="noteWeights"/> defaults to the neutral weight of 1.0)
    /// before comparing. Every candidate starts at a weighted usage of exactly 0 regardless of weight, so the very
    /// first appearance of each pitch is unaffected by weighting (the coverage guarantee holds); only once a
    /// candidate has been used at least once does a higher weight make it look less-used than an equally-raw-used,
    /// lower-weighted candidate, biasing subsequent picks toward it. A small epsilon avoids floating-point division
    /// making two candidates that are mathematically tied fail an exact equality check.
    /// </summary>
    private static Pitch ChooseWeightedLeastUsedCandidate(
        Pitch[] candidates,
        IReadOnlyDictionary<Pitch, int> usageCounts,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double> noteWeights,
        Staff staff,
        Random random)
    {
        double GetWeightedUsage(Pitch candidate) =>
            usageCounts[candidate] / noteWeights.GetValueOrDefault((candidate, staff), 1.0);

        double minimumWeightedUsage = candidates.Min(GetWeightedUsage);
        Pitch[] leastUsed = candidates
            .Where(candidate => GetWeightedUsage(candidate) <= minimumWeightedUsage + WeightedUsageTieEpsilon)
            .ToArray();
        return leastUsed[random.Next(leastUsed.Length)];
    }

    private const double WeightedUsageTieEpsilon = 1e-6;

    private const double NeutralNoteWeight = 1.0;

    /// <summary>
    /// A drill samples in proportion to weight raised to this power. Coverage-first already gives weights a modest pull
    /// on later picks; a drill that merely matched it would not be worth asking for, so it sharpens the contrast
    /// (a note weighted 5 is drawn 25 times as often as a neutral one) while the no-repeat and leap rules, and the
    /// guaranteed appearances of the weakest three, keep the exercise playable.
    /// </summary>
    private const double DrillWeightEmphasis = 2.0;

    /// <summary>The weakest this many notes of the range are guaranteed to be drilled.</summary>
    private const int DrilledWeakNoteCount = 3;

    /// <summary>How many times each drilled weak note appears at least, when the exercise is long enough.</summary>
    private const int MinimumDrilledAppearances = 2;

    /// <summary>
    /// Sampling plus repair can, rarely, run out of room under the leap and no-repeat rules; after this many whole
    /// attempts the drill falls back to the always-valid coverage-first sequence rather than loop.
    /// </summary>
    private const int MaximumWeaknessFirstAttempts = 200;

    /// <summary>
    /// The weak-note drill. Each prompt is drawn at random in proportion to its note's weight, over the pitches the
    /// leap and no-immediate-repeat rules allow after the previous one, with no one-of-each coverage pass first.
    /// The weakest <see cref="DrilledWeakNoteCount"/> notes (those weighted above neutral, heaviest first) are then
    /// guaranteed at least <see cref="MinimumDrilledAppearances"/> appearances by swapping other prompts for them
    /// where both neighbours stay legal; ranges that must include certain pitches (ledger lines) keep that rule.
    /// Only the preset's own palette is ever used.
    /// </summary>
    private static Pitch[] ComposeWeaknessFirstPitches(
        PresetPalette presetPalette,
        int pitchCount,
        Random random,
        IReadOnlyDictionary<(Pitch Pitch, Staff Staff), double>? noteWeights,
        Staff staff)
    {
        IReadOnlyList<Pitch> palette = presetPalette.Pitches;
        double WeightOf(Pitch pitch) =>
            Math.Max(0.01, noteWeights?.GetValueOrDefault((pitch, staff), NeutralNoteWeight) ?? NeutralNoteWeight);
        double SamplingWeightOf(Pitch pitch) => Math.Pow(WeightOf(pitch), DrillWeightEmphasis);
        Pitch[] drilled = noteWeights is null
            ? []
            : palette
                .Where(pitch => WeightOf(pitch) > NeutralNoteWeight)
                .OrderByDescending(WeightOf)
                .ThenBy(pitch => pitch.MidiNumber)
                .Take(Math.Min(DrilledWeakNoteCount, pitchCount / MinimumDrilledAppearances))
                .ToArray();

        for (int attempt = 0; attempt < MaximumWeaknessFirstAttempts; attempt++)
        {
            var sequence = new Pitch[pitchCount];
            Pitch? previous = null;
            for (int index = 0; index < pitchCount; index++)
            {
                Pitch[] candidates = GetLeapConstrainedCandidates(palette, previous, presetPalette.MaxDiatonicLeap);
                sequence[index] = ChooseByWeight(candidates, SamplingWeightOf, random);
                previous = sequence[index];
            }

            foreach (Pitch weak in drilled)
            {
                RaiseAppearances(sequence, weak, drilled, presetPalette.MaxDiatonicLeap, random);
            }

            if (drilled.All(weak => sequence.Count(pitch => pitch == weak) >= MinimumDrilledAppearances) &&
                presetPalette.RequiredGroups.All(group => sequence.Any(group.Contains)))
            {
                return sequence;
            }
        }

        return ComposeRandomPitches(presetPalette, pitchCount, random, noteWeights, staff);
    }

    private static Pitch ChooseByWeight(Pitch[] candidates, Func<Pitch, double> weightOf, Random random)
    {
        double total = candidates.Sum(weightOf);
        double target = random.NextDouble() * total;
        foreach (Pitch candidate in candidates)
        {
            target -= weightOf(candidate);
            if (target < 0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    /// <summary>
    /// Swaps prompts for <paramref name="weak"/> until it has the minimum appearances, only where the new pitch is a
    /// legal neighbour on both sides and the pitch it replaces is not itself a drilled note at its minimum.
    /// </summary>
    private static void RaiseAppearances(
        Pitch[] sequence,
        Pitch weak,
        Pitch[] drilled,
        int maxLeap,
        Random random)
    {
        int[] positions = [.. Enumerable.Range(0, sequence.Length).OrderBy(_ => random.Next())];
        foreach (int position in positions)
        {
            if (sequence.Count(pitch => pitch == weak) >= MinimumDrilledAppearances)
            {
                return;
            }

            Pitch replaced = sequence[position];
            bool replacedIsProtected = replaced != weak &&
                drilled.Contains(replaced) &&
                sequence.Count(pitch => pitch == replaced) <= MinimumDrilledAppearances;
            if (replaced == weak ||
                replacedIsProtected ||
                (position > 0 && !IsLegalStep(sequence[position - 1], weak, maxLeap)) ||
                (position < sequence.Length - 1 && !IsLegalStep(weak, sequence[position + 1], maxLeap)))
            {
                continue;
            }

            sequence[position] = weak;
        }
    }

    private static bool IsLegalStep(Pitch from, Pitch to, int maxLeap) =>
        from != to && Math.Abs(from.DiatonicIndex - to.DiatonicIndex) <= maxLeap;

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
        SightReadingPresetId.DMajor => "D major",
        SightReadingPresetId.BFlatMajor => "B-flat major",
        SightReadingPresetId.AMinor => "A minor",
        SightReadingPresetId.Accidentals => "accidentals",
        SightReadingPresetId.Chords => "chords",
        _ => throw new ArgumentOutOfRangeException(nameof(presetId), presetId, "Unsupported preset."),
    };

    /// <summary>
    /// A preset's pitch palette, its maximum allowed diatonic leap between consecutive prompts, any groups of
    /// pitches that must each contribute at least one appearance (e.g. ledger lines requires one prompt below the
    /// staff and one above), the key signature its pitches are spelled against, and, for the accidentals preset only,
    /// which of its pitches are altered (that preset picks its notes by <see cref="ComposeAccidentalPitches"/>).
    /// </summary>
    private sealed record PresetPalette(
        IReadOnlyList<Pitch> Pitches,
        int MaxDiatonicLeap,
        IReadOnlyList<IReadOnlyList<Pitch>> RequiredGroups,
        int KeyFifths,
        IReadOnlyList<Pitch>? AlteredPitches = null);

    /// <summary>
    /// One event (a note to be pitched, or a rest) within a rhythm pattern's fixed measure template.
    /// <paramref name="TiesToNext"/> ties the note to the next note, which then has
    /// <paramref name="IsTiedContinuation"/>: it repeats the tied note's pitch and is not a prompt of its own.
    /// </summary>
    private sealed record RhythmEvent(
        bool IsRest,
        NoteValue Value,
        BeamState Beam = BeamState.None,
        bool TiesToNext = false,
        bool IsTiedContinuation = false);

    private static bool StartsWithTiedContinuation(IReadOnlyList<RhythmEvent> pattern) =>
        pattern[0].IsTiedContinuation;
}
