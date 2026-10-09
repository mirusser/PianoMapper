using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingExerciseComposerTests
{
    [Fact]
    public void Compose_FiveNoteTrebleExercise_CreatesBalancedQuarterNoteScore()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(42));

        Assert.Equal("Treble five-note note reading", score.Title);
        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        // Task 5: the exercise default is now the beginner tempo of 60 quarter-note pulses per minute (was 120).
        Assert.Equal(new Tempo(60), score.Tempo);
        Assert.Equal(0, score.KeyFifths);
        Assert.Equal(2, score.Measures.Count);

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Equal(8, notes.Length);
        Assert.All(notes, note =>
        {
            Assert.Equal(new NoteValue(4), note.NoteValue);
            Assert.Equal(Staff.Treble, note.Staff);
            Assert.Equal(0, note.Pitch.Alter);
            Assert.InRange(note.Pitch.DiatonicIndex, new Pitch(NoteLetter.C, 0, 4).DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 4).DiatonicIndex);
        });

        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            Assert.Equal([0d, 1d, 2d, 3d], score.Measures[measureIndex].Notes.Select(note => note.BeatOffset));
            Assert.All(score.Measures[measureIndex].Notes, note => Assert.Equal(measureIndex, note.MeasureIndex));
            Assert.Empty(score.Measures[measureIndex].Rests);
        }

        int[] occurrenceCounts = notes
            .GroupBy(note => note.Pitch)
            .Select(group => group.Count())
            .ToArray();
        Assert.Equal(5, occurrenceCounts.Length);
        Assert.InRange(occurrenceCounts.Max() - occurrenceCounts.Min(), 0, 1);
    }

    [Fact]
    public void Compose_SameSeed_ReturnsSamePitchSequence()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(8675309));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(8675309));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void Compose_MultiplePitchPalette_AvoidsAdjacentRepetitions()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.FiveNote,
            PromptCount: 32,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(7));
        Pitch[] pitches = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch)
            .ToArray();

        Assert.All(
            pitches.Zip(pitches.Skip(1)),
            pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_WithPitchWeights_StillGivesEveryPalettePitchInitialCoverageBeforeRepeating()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 20,
            NoteReadingMode.PitchAndOrder);
        var weights = new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)] = 10.0 };

        Score score = SightReadingExerciseComposer.Compose(options, new Random(3), weights);
        Pitch[] firstFivePitches = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch)
            .Take(5)
            .ToArray();

        // The five-note palette has five pitches: even with G4 weighted ten times more than the rest, the first
        // five prompts must still be five *different* pitches — coverage always comes before any adaptive repeat.
        Assert.Equal(5, firstFivePitches.Distinct().Count());
    }

    [Fact]
    public void Compose_WithPitchWeights_WeakPitchAppearsMeasurablyMoreOftenThanWithoutWeights()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 40,
            NoteReadingMode.PitchAndOrder);
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var weights = new Dictionary<(Pitch, Staff), double> { [(weakPitch, Staff.Treble)] = 5.0 };

        Score neutral = SightReadingExerciseComposer.Compose(options, new Random(11));
        Score weighted = SightReadingExerciseComposer.Compose(options, new Random(11), weights);

        int neutralCount = CountOccurrences(neutral, weakPitch);
        int weightedCount = CountOccurrences(weighted, weakPitch);

        // Actual counts for this seed: 8 (neutral, i.e. an exactly even 40/5) versus 15 weighted — almost double,
        // comfortably past this threshold rather than a coincidental near-miss.
        Assert.True(
            weightedCount > neutralCount + 5,
            $"Expected the weighted G4 count ({weightedCount}) to be measurably higher than the neutral count " +
            $"({neutralCount}).");
    }

    [Fact]
    public void Compose_WithPitchWeights_StillRespectsAdjacentAndLeapConstraints()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 32,
            NoteReadingMode.PitchAndOrder);
        var weights = new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.C, 0, 5), Staff.Treble)] = 8.0 };

        Score score = SightReadingExerciseComposer.Compose(options, new Random(19), weights);
        Pitch[] pitches = score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch).ToArray();

        Assert.All(pitches.Zip(pitches.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
        AssertMaxDiatonicLeap(score, maxLeap: 4);
    }

    [Fact]
    public void Compose_WithPitchWeights_KeySignatureAlterationsStayCorrect()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);
        var weights = new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.F, 1, 5), Staff.Treble)] = 6.0 };

        Score score = SightReadingExerciseComposer.Compose(options, new Random(23), weights);

        Assert.Equal(1, score.KeyFifths);
        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Equal(note.Pitch.Letter == NoteLetter.F ? 1 : 0, note.Pitch.Alter));
    }

    [Fact]
    public void Compose_EmptyPitchWeights_ProducesTheSameSequenceAsNoWeights()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.OneOctave,
            PromptCount: 24,
            NoteReadingMode.PitchAndOrder);

        Score neutral = SightReadingExerciseComposer.Compose(options, new Random(29));
        Score empty = SightReadingExerciseComposer.Compose(options, new Random(29), new Dictionary<(Pitch, Staff), double>());

        Assert.Equal(
            neutral.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            empty.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    private static int CountOccurrences(Score score, Pitch pitch) =>
        score.Measures.SelectMany(measure => measure.Notes).Count(note => note.Pitch == pitch);

    [Fact]
    public void Compose_NonPositivePromptCount_Throws()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 0,
            NoteReadingMode.PitchAndOrder);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SightReadingExerciseComposer.Compose(options, new Random(42)));
    }

    [Fact]
    public void Compose_PromptCountNotAMultipleOfMeasureLength_Throws()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 9,
            NoteReadingMode.PitchAndOrder);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SightReadingExerciseComposer.Compose(options, new Random(42)));
    }

    [Fact]
    public void Compose_SupportedPromptCount_ProducesExactlyThatManyPrompts()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(3));

        Assert.Equal(16, score.Measures.SelectMany(measure => measure.Notes).Count());
    }

    [Fact]
    public void ComposeFromMissedPrompts_FewerThanOneMeasureWorth_LaysOutSequentialQuarterNotes()
    {
        Score original = CreateOriginalScore();
        ScoreNote first = CreateSourceNote(NoteLetter.E, Staff.Treble);
        ScoreNote second = CreateSourceNote(NoteLetter.G, Staff.Treble);
        ScoreNote third = CreateSourceNote(NoteLetter.C, Staff.Treble);

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(
            original,
            [[first], [second], [third]]);

        Assert.Single(missedScore.Measures);
        ScoreNote[] notes = missedScore.Measures[0].Notes.ToArray();
        Assert.Equal(3, notes.Length);
        Assert.Equal([first.Pitch, second.Pitch, third.Pitch], notes.Select(note => note.Pitch));
        Assert.Equal([0d, 1d, 2d], notes.Select(note => note.BeatOffset));
        Assert.All(notes, note => Assert.Equal(new NoteValue(4), note.NoteValue));
        Assert.All(notes, note => Assert.Equal(Staff.Treble, note.Staff));
        Assert.All(notes, note => Assert.Equal(0, note.MeasureIndex));
    }

    [Fact]
    public void ComposeFromMissedPrompts_MoreThanOneMeasureWorth_SpansAdditionalMeasure()
    {
        Score original = CreateOriginalScore();
        IReadOnlyList<ScoreNote>[] groups = Enumerable.Range(0, 5)
            .Select(index => (IReadOnlyList<ScoreNote>)[CreateSourceNote((NoteLetter)(index % 7), Staff.Treble)])
            .ToArray();

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, groups);

        Assert.Equal(2, missedScore.Measures.Count);
        Assert.Equal(4, missedScore.Measures[0].Notes.Count);
        ScoreNote overflowNote = Assert.Single(missedScore.Measures[1].Notes);
        Assert.Equal(1, overflowNote.MeasureIndex);
        Assert.Equal(0d, overflowNote.BeatOffset);
    }

    [Fact]
    public void ComposeFromMissedPrompts_ChordGroup_PreservesChordMembershipAtSameOnset()
    {
        Score original = CreateOriginalScore();
        ScoreNote lowerChordNote = CreateSourceNote(NoteLetter.C, Staff.Treble);
        ScoreNote upperChordNote = CreateSourceNote(NoteLetter.E, Staff.Treble);
        ScoreNote nextNote = CreateSourceNote(NoteLetter.G, Staff.Treble);

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(
            original,
            [[lowerChordNote, upperChordNote], [nextNote]]);

        ScoreNote[] notes = missedScore.Measures[0].Notes.ToArray();
        Assert.Equal(3, notes.Length);
        Assert.Equal(2, notes.Count(note => note.BeatOffset == 0d));
        Assert.Equal(1, notes.Count(note => note.BeatOffset == 1d));
    }

    [Fact]
    public void Compose_DefaultTempoInSimpleMeter_IsSixtyQuarterNotePulses()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 8,
                NoteReadingMode.PitchHoldAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Basic),
            new Random(7));

        Assert.Equal(new Tempo(60), score.Tempo);
        Assert.Equal(TimeSpan.FromSeconds(4), MusicalTime.BeatsToDuration(score.TimeSignature.Numerator, score.Tempo));
    }

    [Fact]
    public void Compose_DefaultTempoInCompoundMeter_IsFortyDottedQuarterPulses()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 8,
                NoteReadingMode.PitchHoldAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Compound),
            new Random(7));

        // 40 dotted-quarter pulses per minute is 120 eighth-note beats per minute; one 6/8 measure lasts 3 seconds.
        Assert.Equal(new Tempo(120), score.Tempo);
        Assert.Equal(TimeSpan.FromSeconds(3), MusicalTime.BeatsToDuration(score.TimeSignature.Numerator, score.Tempo));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Fixed, 90, 90)]
    [InlineData(SightReadingRhythmPreset.Basic, 30, 30)]
    [InlineData(SightReadingRhythmPreset.Basic, 200, 200)]
    [InlineData(SightReadingRhythmPreset.Compound, 50, 150)]
    [InlineData(SightReadingRhythmPreset.Extended, 90, 90)]
    public void Compose_ExplicitTempo_IsConvertedToTheScoresWrittenBeatUnit(
        SightReadingRhythmPreset rhythmPreset,
        int pulsesPerMinute,
        double expectedBeatsPerMinute)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 8,
                NoteReadingMode.PitchHoldAndRhythm,
                RhythmPreset: rhythmPreset,
                TempoPulsesPerMinute: pulsesPerMinute),
            new Random(7));

        Assert.Equal(expectedBeatsPerMinute, score.Tempo.BeatsPerMinute);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(201)]
    [InlineData(0)]
    [InlineData(-60)]
    public void Compose_TempoOutsideSupportedRange_Throws(int pulsesPerMinute)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchHoldAndRhythm,
            TempoPulsesPerMinute: pulsesPerMinute);

        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingExerciseComposer.Compose(options, new Random(7)));
    }

    [Fact]
    public void Compose_DifferentTempos_ProduceTheSameSeededPitchSequence()
    {
        static Pitch[] ComposePitches(int? pulsesPerMinute) =>
            SightReadingExerciseComposer.Compose(
                    new SightReadingExerciseOptions(
                        Staff.Bass,
                        SightReadingPresetId.OneOctave,
                        PromptCount: 16,
                        NoteReadingMode.PitchAndOrder,
                        TempoPulsesPerMinute: pulsesPerMinute),
                    new Random(99))
                .Measures.SelectMany(measure => measure.Notes)
                .Select(note => note.Pitch)
                .ToArray();

        Assert.Equal(ComposePitches(null), ComposePitches(45));
        Assert.Equal(ComposePitches(null), ComposePitches(180));
    }

    [Theory]
    [InlineData(Staff.Treble, NoteLetter.B, 4)]
    [InlineData(Staff.Bass, NoteLetter.D, 3)]
    public void Compose_RhythmOnlyFixedRhythm_RepeatsTheStaffsCentreLinePitch(
        Staff staff,
        NoteLetter expectedLetter,
        int expectedOctave)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(staff, SightReadingPresetId.FiveNote, PromptCount: 8, NoteReadingMode.RhythmOnly),
            new Random(3));

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Equal(8, notes.Length);
        Assert.All(notes, note =>
        {
            Assert.Equal(new Pitch(expectedLetter, 0, expectedOctave), note.Pitch);
            Assert.Equal(staff, note.Staff);
            Assert.Equal(new NoteValue(4), note.NoteValue);
        });
        Assert.Equal(0, score.KeyFifths);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Basic, 4)]
    [InlineData(SightReadingRhythmPreset.Compound, 6)]
    public void Compose_RhythmOnlyVariableRhythm_UsesThePatternCatalogOnOnePitch(
        SightReadingRhythmPreset rhythmPreset,
        int expectedNumerator)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.RhythmOnly,
                RhythmPreset: rhythmPreset),
            new Random(21));

        Assert.Equal(expectedNumerator, score.TimeSignature.Numerator);
        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.True(notes.Length >= 16);
        Assert.Single(notes.Select(note => note.Pitch).Distinct());
        Assert.Equal(new Pitch(NoteLetter.B, 0, 4), notes[0].Pitch);
    }

    [Fact]
    public void Compose_RhythmOnlyKeyPresetOrRange_IsIgnoredBecauseThereIsNoPalette()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.GMajor,
                PromptCount: 8,
                NoteReadingMode.RhythmOnly),
            new Random(3));

        Assert.Equal(0, score.KeyFifths);
        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Equal(new Pitch(NoteLetter.B, 0, 4), note.Pitch));
    }

    [Fact]
    public void Compose_RhythmOnlyWithSameSeed_IsDeterministic()
    {
        static Score Compose() => SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Bass,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.RhythmOnly,
                RhythmPreset: SightReadingRhythmPreset.Basic),
            new Random(77));

        Score first = Compose();
        Score second = Compose();

        Assert.Equal(
            first.Measures.Select(measure => measure.Notes.Select(note => (note.BeatOffset, note.NoteValue)).ToArray()),
            second.Measures.Select(measure => measure.Notes.Select(note => (note.BeatOffset, note.NoteValue)).ToArray()));
    }

    [Fact]
    public void Compose_RhythmOnlyWithGrandStaff_ThrowsWithAClearMessage()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.RhythmOnly,
            IsGrandStaff: true);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => SightReadingExerciseComposer.Compose(options, new Random(1)));

        Assert.Contains("grand staff", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_RhythmOnlyWithChords_ThrowsWithAClearMessage()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 8,
            NoteReadingMode.RhythmOnly);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => SightReadingExerciseComposer.Compose(options, new Random(1)));

        Assert.Contains("chord", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComposeFromMissedMeasures_MissInTheSecondMeasure_ReplaysThatMeasureWithItsRhythmAndRests()
    {
        Score original = ComposeBasicRhythm(seed: 21, staff: Staff.Treble, promptCount: 16);
        ScoreMeasure sourceMeasure = original.Measures[1];
        ScoreNote missed = sourceMeasure.Notes[0];

        Score replay = SightReadingExerciseComposer.ComposeFromMissedMeasures(original, [missed]);

        ScoreMeasure replayedMeasure = Assert.Single(replay.Measures);
        Assert.Equal(
            sourceMeasure.Notes.Select(note => (note.Pitch, note.NoteValue, note.BeatOffset, note.BeamState)),
            replayedMeasure.Notes.Select(note => (note.Pitch, note.NoteValue, note.BeatOffset, note.BeamState)));
        Assert.Equal(
            sourceMeasure.Rests.Select(rest => (rest.NoteValue, rest.BeatOffset, rest.Staff)),
            replayedMeasure.Rests.Select(rest => (rest.NoteValue, rest.BeatOffset, rest.Staff)));
        Assert.All(replayedMeasure.Notes, note => Assert.Equal(0, note.MeasureIndex));
        Assert.All(replayedMeasure.Rests, rest => Assert.Equal(0, rest.MeasureIndex));
        Assert.Equal(original.TimeSignature, replay.TimeSignature);
        Assert.Equal(original.Tempo, replay.Tempo);
        Assert.Equal(original.KeyFifths, replay.KeyFifths);
    }

    [Fact]
    public void ComposeFromMissedMeasures_MissesInSeveralMeasures_ReplaysEachOnceInOrderWithRecomputedIndexes()
    {
        Score original = ComposeBasicRhythm(seed: 21, staff: Staff.Bass, promptCount: 16);
        ScoreNote[] missed =
        [
            original.Measures[2].Notes[0],
            original.Measures[0].Notes[0],
            original.Measures[2].Notes[^1],
        ];

        Score replay = SightReadingExerciseComposer.ComposeFromMissedMeasures(original, missed);

        Assert.Equal(2, replay.Measures.Count);
        Assert.Equal(
            original.Measures[0].Notes.Select(note => (note.NoteValue, note.BeatOffset)),
            replay.Measures[0].Notes.Select(note => (note.NoteValue, note.BeatOffset)));
        Assert.Equal(
            original.Measures[2].Notes.Select(note => (note.NoteValue, note.BeatOffset)),
            replay.Measures[1].Notes.Select(note => (note.NoteValue, note.BeatOffset)));
        Assert.All(replay.Measures[0].Notes, note => Assert.Equal(0, note.MeasureIndex));
        Assert.All(replay.Measures[1].Notes, note => Assert.Equal(1, note.MeasureIndex));
    }

    [Fact]
    public void ComposeFromMissedMeasures_ChordMeasure_KeepsEveryChordMemberAtItsOnset()
    {
        Score original = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.Chords,
                PromptCount: 8,
                NoteReadingMode.PitchAndRhythm),
            new Random(4));
        ScoreNote missed = original.Measures[1].Notes[0];

        Score replay = SightReadingExerciseComposer.ComposeFromMissedMeasures(original, [missed]);

        ScoreMeasure replayedMeasure = Assert.Single(replay.Measures);
        Assert.Equal(original.Measures[1].Notes.Count, replayedMeasure.Notes.Count);
        Assert.Equal(
            original.Measures[1].Notes.Select(note => (note.Pitch, note.BeatOffset)),
            replayedMeasure.Notes.Select(note => (note.Pitch, note.BeatOffset)));
    }

    [Fact]
    public void ComposeFromMissedMeasures_NoMissedNotes_Throws()
    {
        Score original = ComposeBasicRhythm(seed: 21, staff: Staff.Treble, promptCount: 8);

        Assert.Throws<ArgumentException>(
            () => SightReadingExerciseComposer.ComposeFromMissedMeasures(original, []));
    }

    [Fact]
    public void ComposeFromMissedMeasures_NoteFromAnotherScore_Throws()
    {
        Score original = ComposeBasicRhythm(seed: 21, staff: Staff.Treble, promptCount: 8);
        var stranger = new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), MeasureIndex: 99, 0, Staff.Treble);

        Assert.Throws<ArgumentException>(
            () => SightReadingExerciseComposer.ComposeFromMissedMeasures(original, [stranger]));
    }

    private static Score ComposeBasicRhythm(int seed, Staff staff, int promptCount) =>
        SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                staff,
                SightReadingPresetId.FiveNote,
                promptCount,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Basic),
            new Random(seed));

    [Fact]
    public void Compose_CoverageFirstByDefault_ReproducesTodaysSeededSequences()
    {
        // Golden sequences captured from the composer before the strategy option existed.
        Assert.Equal(
            "F4,D4,C4,E4,G4,E4,F4,G4",
            Sequence(Staff.Treble, SightReadingPresetId.FiveNote, promptCount: 8, seed: 42));
        Assert.Equal(
            "F3,C4,A3,D3,E3,B3,G3,C3,G3,B3,F3,C4,A3,D3,E3,C3",
            Sequence(Staff.Bass, SightReadingPresetId.OneOctave, promptCount: 16, seed: 7));
        Assert.Equal(
            "E4,D3,D4,E3,C4,G3,D4,F3",
            Sequence(Staff.Treble, SightReadingPresetId.FiveNote, promptCount: 8, seed: 9, isGrandStaff: true));
        Assert.Equal(
            "E4,C4,D4,F4,G4,E4,G4,F4,G4,E4,G4,F4,G4,E4,G4,F4",
            Sequence(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                promptCount: 16,
                seed: 11,
                weights: new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)] = 5.0 }));
    }

    [Fact]
    public void Compose_ExplicitCoverageFirst_EqualsTheDefault()
    {
        var weights = new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.E, 0, 4), Staff.Treble)] = 4.0 };

        string byDefault = Sequence(Staff.Treble, SightReadingPresetId.FiveNote, 16, seed: 5, weights: weights);
        string explicitStrategy = Sequence(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            16,
            seed: 5,
            weights: weights,
            strategy: SightReadingGenerationStrategy.CoverageFirst);

        Assert.Equal(byDefault, explicitStrategy);
    }

    [Fact]
    public void Compose_GrandStaffWeightOnlyForBass_LeavesTheTrebleNotesUntouchedAndBoostsBass()
    {
        var c4 = new Pitch(NoteLetter.C, 0, 4);
        var weights = new Dictionary<(Pitch, Staff), double> { [(c4, Staff.Bass)] = 8.0 };
        // The one-octave range contains C4 on both staves (the top of the bass staff's octave, the bottom of the treble's).
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 40,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Score neutral = SightReadingExerciseComposer.Compose(options, new Random(17));
        Score weighted = SightReadingExerciseComposer.Compose(options, new Random(17), weights);

        static string[] PitchesOn(Score score, Staff staff) => score.Measures
            .SelectMany(measure => measure.Notes)
            .Where(note => note.Staff == staff)
            .Select(note => note.Pitch.ToString())
            .ToArray();
        Assert.Equal(PitchesOn(neutral, Staff.Treble), PitchesOn(weighted, Staff.Treble));
        int neutralBassC4 = PitchesOn(neutral, Staff.Bass).Count(pitch => pitch == "C4");
        int weightedBassC4 = PitchesOn(weighted, Staff.Bass).Count(pitch => pitch == "C4");
        Assert.True(weightedBassC4 > neutralBassC4, $"bass C4: neutral {neutralBassC4}, weighted {weightedBassC4}");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void Compose_WeaknessFirst_KeepsEveryInvariantAcrossManySeeds(int promptCount)
    {
        var weights = new Dictionary<(Pitch, Staff), double>
        {
            [(new Pitch(NoteLetter.C, 0, 4), Staff.Treble)] = 6.0,
            [(new Pitch(NoteLetter.E, 0, 4), Staff.Treble)] = 5.0,
            [(new Pitch(NoteLetter.G, 0, 4), Staff.Treble)] = 4.0,
            [(new Pitch(NoteLetter.D, 0, 4), Staff.Treble)] = 1.5,
        };
        Pitch[] palette = [.. FiveNoteTreblePalette()];
        Pitch[] weakestThree =
        [
            new Pitch(NoteLetter.C, 0, 4),
            new Pitch(NoteLetter.E, 0, 4),
            new Pitch(NoteLetter.G, 0, 4),
        ];

        for (int seed = 0; seed < 300; seed++)
        {
            Pitch[] pitches = PitchesOf(Compose(promptCount, seed, weights, SightReadingGenerationStrategy.WeaknessFirst));

            Assert.Equal(promptCount, pitches.Length);
            Assert.All(pitches, pitch => Assert.Contains(pitch, palette));
            Assert.All(pitches.Zip(pitches.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
            Assert.All(
                pitches.Zip(pitches.Skip(1)),
                pair => Assert.True(
                    Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex) <= 2,
                    $"seed {seed}: leap {pair.First} to {pair.Second}"));
            foreach (Pitch weak in weakestThree)
            {
                Assert.True(
                    pitches.Count(pitch => pitch == weak) >= 2,
                    $"seed {seed}: {weak} appeared {pitches.Count(pitch => pitch == weak)} times in {string.Join(",", pitches)}");
            }
        }
    }

    [Theory]
    [InlineData(SightReadingPresetId.FiveNote, 8)]
    [InlineData(SightReadingPresetId.FiveNote, 16)]
    [InlineData(SightReadingPresetId.OneOctave, 8)]
    [InlineData(SightReadingPresetId.OneOctave, 16)]
    public void Compose_WeaknessFirst_ServesTheWeakPitchMoreOftenThanCoverageFirstEvenAtShortLengths(
        SightReadingPresetId preset,
        int promptCount)
    {
        var weakPitch = new Pitch(NoteLetter.G, 0, 4);
        var weights = new Dictionary<(Pitch, Staff), double> { [(weakPitch, Staff.Treble)] = 5.0 };
        double coverageTotal = 0;
        double weaknessTotal = 0;

        for (int seed = 0; seed < 200; seed++)
        {
            coverageTotal += PitchesOf(Compose(promptCount, seed, weights, SightReadingGenerationStrategy.CoverageFirst, preset))
                .Count(pitch => pitch == weakPitch);
            weaknessTotal += PitchesOf(Compose(promptCount, seed, weights, SightReadingGenerationStrategy.WeaknessFirst, preset))
                .Count(pitch => pitch == weakPitch);
        }

        // Coverage-first spends its first prompts on one of each pitch, so a weight barely moves 8 or 16 prompts (the
        // original shortfall); the drill must visibly beat it.
        Assert.True(
            weaknessTotal > coverageTotal * 1.25,
            $"{preset} x {promptCount}: weakness-first {weaknessTotal / 200:F2} weak notes per exercise vs " +
            $"coverage-first {coverageTotal / 200:F2}.");
    }

    [Fact]
    public void Compose_WeaknessFirstWithoutWeights_StillComposesAValidExercise()
    {
        Pitch[] pitches = PitchesOf(Compose(8, seed: 3, weights: null, SightReadingGenerationStrategy.WeaknessFirst));

        Assert.Equal(8, pitches.Length);
        Assert.All(pitches.Zip(pitches.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_WeaknessFirstWithSameSeed_IsDeterministic()
    {
        var weights = new Dictionary<(Pitch, Staff), double> { [(new Pitch(NoteLetter.D, 0, 4), Staff.Treble)] = 7.0 };

        Assert.Equal(
            Sequence(Staff.Treble, SightReadingPresetId.FiveNote, 16, 99, weights: weights, strategy: SightReadingGenerationStrategy.WeaknessFirst),
            Sequence(Staff.Treble, SightReadingPresetId.FiveNote, 16, 99, weights: weights, strategy: SightReadingGenerationStrategy.WeaknessFirst));
    }

    [Fact]
    public void Compose_WeaknessFirstOnALedgerLinePreset_DrawsOnlyFromThePaletteAndKeepsBothLedgerSides()
    {
        HashSet<Pitch> palette = [.. PitchesOf(Compose(
                200,
                seed: 1,
                weights: null,
                SightReadingGenerationStrategy.CoverageFirst,
                SightReadingPresetId.LedgerLines))];
        Pitch weakPitch = palette.OrderBy(pitch => pitch.MidiNumber).First();
        var weak = new Dictionary<(Pitch, Staff), double> { [(weakPitch, Staff.Treble)] = 6.0 };

        for (int seed = 0; seed < 100; seed++)
        {
            Pitch[] pitches = PitchesOf(Compose(
                8,
                seed,
                weak,
                SightReadingGenerationStrategy.WeaknessFirst,
                SightReadingPresetId.LedgerLines));

            Assert.All(pitches, pitch => Assert.Contains(pitch, palette));
            // The preset requires at least one prompt below and one above the staff; the drill keeps that.
            Assert.Contains(pitches, pitch => pitch.DiatonicIndex < new Pitch(NoteLetter.E, 0, 4).DiatonicIndex);
            Assert.Contains(pitches, pitch => pitch.DiatonicIndex > new Pitch(NoteLetter.F, 0, 5).DiatonicIndex);
        }
    }

    [Fact]
    public void GetRangePitches_FiveNoteTreble_IsCToG()
    {
        Pitch[] pitches = [.. SightReadingExerciseComposer.GetRangePitches(Staff.Treble, SightReadingPresetId.FiveNote)];

        Assert.Equal(FiveNoteTreblePalette(), pitches);
    }

    [Fact]
    public void GetRangePitches_SameRangeAsWhatComposeDrawsFrom()
    {
        HashSet<Pitch> range = [.. SightReadingExerciseComposer.GetRangePitches(Staff.Bass, SightReadingPresetId.OneOctave)];
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Bass,
                SightReadingPresetId.OneOctave,
                PromptCount: 64,
                NoteReadingMode.PitchAndOrder),
            new Random(8));

        Assert.All(PitchesOf(score), pitch => Assert.Contains(pitch, range));
        Assert.Equal(range.Count, PitchesOf(score).Distinct().Count());
    }

    [Fact]
    public void Options_Strategy_DefaultsToCoverageFirst()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Assert.Equal(SightReadingGenerationStrategy.CoverageFirst, options.Strategy);
    }

    private static string Sequence(
        Staff staff,
        SightReadingPresetId preset,
        int promptCount,
        int seed,
        bool isGrandStaff = false,
        IReadOnlyDictionary<(Pitch, Staff), double>? weights = null,
        SightReadingGenerationStrategy strategy = SightReadingGenerationStrategy.CoverageFirst) =>
        string.Join(
            ",",
            PitchesOf(SightReadingExerciseComposer.Compose(
                new SightReadingExerciseOptions(
                    staff,
                    preset,
                    promptCount,
                    NoteReadingMode.PitchAndOrder,
                    isGrandStaff,
                    Strategy: strategy),
                new Random(seed),
                weights)));

    private static Score Compose(
        int promptCount,
        int seed,
        IReadOnlyDictionary<(Pitch, Staff), double>? weights,
        SightReadingGenerationStrategy strategy,
        SightReadingPresetId preset = SightReadingPresetId.FiveNote) =>
        SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                preset,
                promptCount,
                NoteReadingMode.PitchAndOrder,
                Strategy: strategy),
            new Random(seed),
            weights);

    private static Pitch[] PitchesOf(Score score) =>
        [.. score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch)];

    private static IEnumerable<Pitch> FiveNoteTreblePalette() =>
        new[] { NoteLetter.C, NoteLetter.D, NoteLetter.E, NoteLetter.F, NoteLetter.G }.Select(letter => new Pitch(letter, 0, 4));

    [Fact]
    public void ComposeFromMissedPrompts_PreservesTimeSignatureTempoAndKeyFifthsFromOriginal()
    {
        Score original = CreateOriginalScore() with { KeyFifths = 1 };
        ScoreNote note = CreateSourceNote(NoteLetter.C, Staff.Bass);

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, [[note]]);

        Assert.Equal(original.TimeSignature, missedScore.TimeSignature);
        Assert.Equal(original.Tempo, missedScore.Tempo);
        Assert.Equal(1, missedScore.KeyFifths);
    }

    [Fact]
    public void ComposeFromMissedPrompts_NoMissedGroups_Throws()
    {
        Score original = CreateOriginalScore();

        Assert.Throws<ArgumentException>(() =>
            SightReadingExerciseComposer.ComposeFromMissedPrompts(original, []));
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_FiveNote_NeverLeapsMoreThanADiatonicThird(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(21));

        AssertMaxDiatonicLeap(score, maxLeap: 2);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_OneOctave_NeverLeapsMoreThanADiatonicFifth(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(21));

        AssertMaxDiatonicLeap(score, maxLeap: 4);
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_LedgerLines_NeverLeapsMoreThanADiatonicOctave(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.LedgerLines,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(21));

        AssertMaxDiatonicLeap(score, maxLeap: 7);
    }

    [Fact]
    public void Compose_LedgerLinesTreble_UsesFullA3ToC6NaturalRange()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.LedgerLines,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(3));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.A, 0, 3).DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 6).DiatonicIndex));
    }

    [Fact]
    public void Compose_LedgerLinesBass_UsesFullC2ToE4NaturalRange()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.LedgerLines,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(3));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 2).DiatonicIndex,
                new Pitch(NoteLetter.E, 0, 4).DiatonicIndex));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Compose_LedgerLinesTreble_AlwaysContainsAPromptBelowAndAboveTheStaff(int seed)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.LedgerLines,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(seed));

        int[] diatonicIndexes = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch.DiatonicIndex)
            .ToArray();
        int staffBottom = new Pitch(NoteLetter.E, 0, 4).DiatonicIndex;
        int staffTop = new Pitch(NoteLetter.F, 0, 5).DiatonicIndex;
        Assert.Contains(diatonicIndexes, index => index < staffBottom);
        Assert.Contains(diatonicIndexes, index => index > staffTop);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Compose_LedgerLinesBass_AlwaysContainsAPromptBelowAndAboveTheStaff(int seed)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.LedgerLines,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(seed));

        int[] diatonicIndexes = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch.DiatonicIndex)
            .ToArray();
        int staffBottom = new Pitch(NoteLetter.G, 0, 2).DiatonicIndex;
        int staffTop = new Pitch(NoteLetter.A, 0, 3).DiatonicIndex;
        Assert.Contains(diatonicIndexes, index => index < staffBottom);
        Assert.Contains(diatonicIndexes, index => index > staffTop);
    }

    [Fact]
    public void Compose_LedgerLinesSameSeed_ReturnsSamePitchSequence()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.LedgerLines,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(555));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(555));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_GMajor_SetsKeyFifthsToOneSharp(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.GMajor,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.Equal(1, score.KeyFifths);
    }

    [Fact]
    public void Compose_GMajorTreble_SpellsFAsSharpAndEverythingElseNatural()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(score.Measures.SelectMany(measure => measure.Notes), note =>
        {
            int expectedAlter = note.Pitch.Letter == NoteLetter.F ? 1 : 0;
            Assert.Equal(expectedAlter, note.Pitch.Alter);
        });
        Assert.Contains(
            score.Measures.SelectMany(measure => measure.Notes),
            note => note.Pitch.Letter == NoteLetter.F);
    }

    [Fact]
    public void Compose_GMajorTreble_UsesG4ToG5Range()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 4).DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 5).DiatonicIndex));
    }

    [Fact]
    public void Compose_GMajorBass_UsesG2ToG3Range()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 2).DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 3).DiatonicIndex));
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_FMajor_SetsKeyFifthsToOneFlat(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.FMajor,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.Equal(-1, score.KeyFifths);
    }

    [Fact]
    public void Compose_FMajorTreble_SpellsBAsFlatAndEverythingElseNatural()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(score.Measures.SelectMany(measure => measure.Notes), note =>
        {
            int expectedAlter = note.Pitch.Letter == NoteLetter.B ? -1 : 0;
            Assert.Equal(expectedAlter, note.Pitch.Alter);
        });
        Assert.Contains(
            score.Measures.SelectMany(measure => measure.Notes),
            note => note.Pitch.Letter == NoteLetter.B);
    }

    [Fact]
    public void Compose_FMajorTreble_UsesF4ToF5Range()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.F, 0, 4).DiatonicIndex,
                new Pitch(NoteLetter.F, 0, 5).DiatonicIndex));
    }

    [Fact]
    public void Compose_FMajorBass_UsesF2ToF3Range()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.FMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(9));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.F, 0, 2).DiatonicIndex,
                new Pitch(NoteLetter.F, 0, 3).DiatonicIndex));
    }

    [Theory]
    [InlineData(SightReadingPresetId.DMajor, Staff.Treble, "D4,E4,F#4,G4,A4,B4,C#5,D5")]
    [InlineData(SightReadingPresetId.DMajor, Staff.Bass, "D2,E2,F#2,G2,A2,B2,C#3,D3")]
    [InlineData(SightReadingPresetId.BFlatMajor, Staff.Treble, "Bb4,C5,D5,Eb5,F5,G5,A5,Bb5")]
    [InlineData(SightReadingPresetId.BFlatMajor, Staff.Bass, "Bb2,C3,D3,Eb3,F3,G3,A3,Bb3")]
    [InlineData(SightReadingPresetId.AMinor, Staff.Treble, "A4,B4,C5,D5,E5,F5,G5,A5")]
    [InlineData(SightReadingPresetId.AMinor, Staff.Bass, "A2,B2,C3,D3,E3,F3,G3,A3")]
    public void GetRangePitches_NewKeyPresets_AreTheKeysScaleSpelledForItsSignature(
        SightReadingPresetId preset,
        Staff staff,
        string expected)
    {
        Assert.Equal(expected, string.Join(",", SightReadingExerciseComposer.GetRangePitches(staff, preset)));
    }

    [Theory]
    [InlineData(SightReadingPresetId.DMajor, 2)]
    [InlineData(SightReadingPresetId.BFlatMajor, -2)]
    [InlineData(SightReadingPresetId.AMinor, 0)]
    public void Compose_NewKeyPresets_SetTheKeySignatureAndNeverWriteARedundantAccidental(
        SightReadingPresetId preset,
        int keyFifths)
    {
        NoteLetter[] alteredLetters = keyFifths switch
        {
            2 => [NoteLetter.F, NoteLetter.C],
            -2 => [NoteLetter.B, NoteLetter.E],
            _ => [],
        };

        foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
        {
            Score score = SightReadingExerciseComposer.Compose(
                new SightReadingExerciseOptions(staff, preset, PromptCount: 16, NoteReadingMode.PitchAndOrder),
                new Random(8));

            Assert.Equal(keyFifths, score.KeyFifths);
            Assert.All(
                PitchesOf(score),
                pitch => Assert.Equal(alteredLetters.Contains(pitch.Letter) ? Math.Sign(keyFifths) : 0, pitch.Alter));
        }
    }

    [Theory]
    [InlineData(SightReadingPresetId.DMajor)]
    [InlineData(SightReadingPresetId.BFlatMajor)]
    [InlineData(SightReadingPresetId.AMinor)]
    public void GetRangePitches_NewKeyPresets_NeedAtMostTwoLedgerLinesOnEitherStaff(SightReadingPresetId preset)
    {
        foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
        {
            Assert.All(
                SightReadingExerciseComposer.GetRangePitches(staff, preset),
                pitch => Assert.InRange(CountLedgerLines(pitch, staff), 0, 2));
        }
    }

    [Theory]
    [InlineData(SightReadingPresetId.DMajor)]
    [InlineData(SightReadingPresetId.BFlatMajor)]
    [InlineData(SightReadingPresetId.AMinor)]
    public void Compose_NewKeyPresets_UseTheWholeRangeLeapAtMostAFifthAndAreDeterministic(SightReadingPresetId preset)
    {
        foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
        {
            IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(staff, preset);
            var options = new SightReadingExerciseOptions(staff, preset, PromptCount: 16, NoteReadingMode.PitchAndOrder);

            Score first = SightReadingExerciseComposer.Compose(options, new Random(31));
            Score second = SightReadingExerciseComposer.Compose(options, new Random(31));

            Assert.Equal(PitchesOf(first), PitchesOf(second));
            Assert.Equal(palette, PitchesOf(first).Distinct().OrderBy(pitch => pitch.DiatonicIndex));
            AssertMaxDiatonicLeap(first, maxLeap: 4);
        }
    }

    [Theory]
    [InlineData(Staff.Treble, "C4,C#4,D4,Eb4,E4,F4,F#4,G4,A4,Bb4,B4,C5")]
    [InlineData(Staff.Bass, "C3,C#3,D3,Eb3,E3,F3,F#3,G3,A3,Bb3,B3,C4")]
    public void GetRangePitches_Accidentals_IsAnOctaveOfNaturalsPlusCSharpEFlatFSharpAndBFlat(Staff staff, string expected)
    {
        Assert.Equal(
            expected,
            string.Join(",", SightReadingExerciseComposer.GetRangePitches(staff, SightReadingPresetId.Accidentals)));
    }

    [Fact]
    public void GetRangePitches_Accidentals_NeedsAtMostTwoLedgerLinesOnEitherStaff()
    {
        foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
        {
            Assert.All(
                SightReadingExerciseComposer.GetRangePitches(staff, SightReadingPresetId.Accidentals),
                pitch => Assert.InRange(CountLedgerLines(pitch, staff), 0, 2));
        }
    }

    [Theory]
    [InlineData(Staff.Treble, false, 8, SightReadingRhythmPreset.Fixed)]
    [InlineData(Staff.Treble, false, 16, SightReadingRhythmPreset.Fixed)]
    [InlineData(Staff.Bass, false, 16, SightReadingRhythmPreset.Fixed)]
    [InlineData(Staff.Treble, true, 8, SightReadingRhythmPreset.Fixed)]
    [InlineData(Staff.Treble, true, 16, SightReadingRhythmPreset.Fixed)]
    [InlineData(Staff.Treble, false, 16, SightReadingRhythmPreset.Basic)]
    [InlineData(Staff.Bass, false, 8, SightReadingRhythmPreset.Compound)]
    public void Compose_Accidentals_KeepsEveryRuleAcrossManySeeds(
        Staff staff,
        bool isGrandStaff,
        int promptCount,
        SightReadingRhythmPreset rhythmPreset)
    {
        for (int seed = 0; seed < 150; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(
                new SightReadingExerciseOptions(
                    staff,
                    SightReadingPresetId.Accidentals,
                    promptCount,
                    NoteReadingMode.PitchAndOrder,
                    isGrandStaff,
                    rhythmPreset),
                new Random(seed));

            Assert.Equal(0, score.KeyFifths);
            AssertNoAmbiguousAccidentals(score);
            foreach (Staff usedStaff in score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Staff).Distinct())
            {
                ScoreNote[] notes = score.Measures
                    .SelectMany(measure => measure.Notes)
                    .Where(note => note.Staff == usedStaff)
                    .OrderBy(note => note.MeasureIndex)
                    .ThenBy(note => note.BeatOffset)
                    .ToArray();
                IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(
                    usedStaff,
                    SightReadingPresetId.Accidentals);
                Assert.All(notes, note => Assert.Contains(note.Pitch, palette));
                Assert.All(
                    notes.Zip(notes.Skip(1)),
                    pair => Assert.InRange(Math.Abs(pair.First.Pitch.DiatonicIndex - pair.Second.Pitch.DiatonicIndex), 1, 4));
                int required = Math.Min(2, notes.Length / 4);
                Assert.True(notes.Count(note => note.Pitch.Alter > 0) >= required, $"seed {seed}: too few sharps");
                Assert.True(notes.Count(note => note.Pitch.Alter < 0) >= required, $"seed {seed}: too few flats");
            }
        }
    }

    [Fact]
    public void Compose_Accidentals_AlteredNotesCarryTheirPrintedAccidentalAndPlainNotesCarryNone()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Bass,
                SightReadingPresetId.Accidentals,
                PromptCount: 16,
                NoteReadingMode.PitchAndOrder,
                RhythmPreset: SightReadingRhythmPreset.Basic),
            new Random(6));

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Contains(notes, note => note.Pitch.Alter != 0);
        Assert.All(
            notes,
            note => Assert.Equal(
                note.Pitch.Alter switch
                {
                    1 => ScoreAccidental.Sharp,
                    -1 => ScoreAccidental.Flat,
                    _ => (ScoreAccidental?)null,
                },
                note.Accidental));
    }

    [Theory]
    [InlineData(SightReadingPresetId.GMajor)]
    [InlineData(SightReadingPresetId.FMajor)]
    [InlineData(SightReadingPresetId.DMajor)]
    [InlineData(SightReadingPresetId.BFlatMajor)]
    [InlineData(SightReadingPresetId.FiveNote)]
    public void Compose_PresetsSpelledForTheirKey_CarryNoExplicitAccidentals(SightReadingPresetId preset)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(Staff.Treble, preset, PromptCount: 16, NoteReadingMode.PitchAndOrder),
            new Random(6));

        Assert.All(score.Measures.SelectMany(measure => measure.Notes), note => Assert.Null(note.Accidental));
    }

    [Fact]
    public void ComposeFromMissedPrompts_CarriesTheSourceNotesPrintedAccidental()
    {
        Score original = CreateOriginalScore();
        ScoreNote fSharp = CreateSourceNote(NoteLetter.F, Staff.Treble) with
        {
            Pitch = new Pitch(NoteLetter.F, 1, 4),
            Accidental = ScoreAccidental.Sharp,
        };

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, [[fSharp]]);

        Assert.Equal(ScoreAccidental.Sharp, Assert.Single(missedScore.Measures[0].Notes).Accidental);
    }

    [Fact]
    public void Compose_Accidentals_SameSeedIsDeterministic()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Accidentals,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Assert.Equal(
            PitchesOf(SightReadingExerciseComposer.Compose(options, new Random(5))),
            PitchesOf(SightReadingExerciseComposer.Compose(options, new Random(5))));
    }

    [Fact]
    public void Compose_AccidentalsWithMasteryWeights_StillKeepsEveryRule()
    {
        var weights = new Dictionary<(Pitch, Staff), double>
        {
            [(new Pitch(NoteLetter.F, 1, 4), Staff.Treble)] = 5.0,
            [(new Pitch(NoteLetter.F, 0, 4), Staff.Treble)] = 5.0,
        };
        for (int seed = 0; seed < 50; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(
                new SightReadingExerciseOptions(
                    Staff.Treble,
                    SightReadingPresetId.Accidentals,
                    PromptCount: 16,
                    NoteReadingMode.PitchAndOrder),
                new Random(seed),
                weights);

            AssertNoAmbiguousAccidentals(score);
        }
    }

    [Fact]
    public void SupportsMotion_Accidentals_IsFalseBecauseItPicksItsOwnNotes()
    {
        Assert.False(SightReadingExerciseComposer.SupportsMotion(SightReadingPresetId.Accidentals));
    }

    [Fact]
    public void Compose_Accidentals_ASessionAcceptsTheSoundingKeyWhateverItsSpelling()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.Accidentals,
                PromptCount: 16,
                NoteReadingMode.PitchAndOrder),
            new Random(2));
        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Contains(notes, note => note.Pitch.Alter > 0);
        Assert.Contains(notes, note => note.Pitch.Alter < 0);
        var session = new NoteReadingSession();
        session.Reset(score);

        foreach (ScoreNote note in notes)
        {
            // Play the other enharmonic spelling of the same key (C# as Db, Eb as D#, natural notes as themselves).
            Pitch respelled = note.Pitch.Alter switch
            {
                1 => new Pitch(note.Pitch.Letter + 1, -1, note.Pitch.Octave),
                -1 => new Pitch(note.Pitch.Letter - 1, 1, note.Pitch.Octave),
                _ => note.Pitch,
            };
            Assert.Equal(note.Pitch.MidiNumber, respelled.MidiNumber);
            Assert.True(session.Check(respelled).IsCorrect);
        }

        Assert.True(session.IsComplete);
        Assert.Equal(notes.Length, session.FirstTryCorrectCount);
        Assert.Equal(
            notes.Select(note => note.Pitch),
            session.PromptResults.SelectMany(result => result.ExpectedPitches));
    }

    [Fact]
    public void ComposeFromMissedPrompts_NaturalAfterTheSameLetterAlteredInTheMeasure_StartsAFreshMeasure()
    {
        Score original = CreateOriginalScore();
        ScoreNote fSharp = CreateSourceNote(NoteLetter.F, Staff.Treble) with { Pitch = new Pitch(NoteLetter.F, 1, 4) };
        ScoreNote g = CreateSourceNote(NoteLetter.G, Staff.Treble);
        ScoreNote fNatural = CreateSourceNote(NoteLetter.F, Staff.Treble);

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(
            original,
            [[fSharp], [g], [fNatural]]);

        Assert.Equal(2, missedScore.Measures.Count);
        Assert.Equal([fSharp.Pitch, g.Pitch], missedScore.Measures[0].Notes.Select(note => note.Pitch));
        Assert.Equal([0d, 1d], missedScore.Measures[0].Notes.Select(note => note.BeatOffset));
        ScoreNote retried = Assert.Single(missedScore.Measures[1].Notes);
        Assert.Equal(fNatural.Pitch, retried.Pitch);
        Assert.Equal(1, retried.MeasureIndex);
        Assert.Equal(0d, retried.BeatOffset);
        AssertNoAmbiguousAccidentals(missedScore);
    }

    [Fact]
    public void ComposeFromMissedPrompts_SameAlterationRepeatedInAMeasure_StaysInOneMeasure()
    {
        Score original = CreateOriginalScore();
        ScoreNote fSharp = CreateSourceNote(NoteLetter.F, Staff.Treble) with { Pitch = new Pitch(NoteLetter.F, 1, 4) };
        ScoreNote g = CreateSourceNote(NoteLetter.G, Staff.Treble);

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, [[fSharp], [g], [fSharp]]);

        ScoreMeasure only = Assert.Single(missedScore.Measures);
        Assert.Equal(3, only.Notes.Count);
    }

    [Fact]
    public void ComposeFromMissedPrompts_AlteredNoteAgainstItsKeySignature_IsNotMistakenForAnAccidental()
    {
        // In G major an F# is the key's own note, so a following F# (or a G) never needs a new measure.
        Score original = CreateOriginalScore() with { KeyFifths = 1 };
        ScoreNote fSharp = CreateSourceNote(NoteLetter.F, Staff.Treble) with { Pitch = new Pitch(NoteLetter.F, 1, 4) };
        ScoreNote fSharpAgain = CreateSourceNote(NoteLetter.F, Staff.Treble) with { Pitch = new Pitch(NoteLetter.F, 1, 5) };

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, [[fSharp], [fSharpAgain]]);

        Assert.Single(missedScore.Measures);
    }

    [Fact]
    public void Compose_GMajorAndFMajor_NeverLeapMoreThanADiatonicFifth()
    {
        var gMajorOptions = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);
        var fMajorOptions = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.FMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        AssertMaxDiatonicLeap(SightReadingExerciseComposer.Compose(gMajorOptions, new Random(9)), maxLeap: 4);
        AssertMaxDiatonicLeap(SightReadingExerciseComposer.Compose(fMajorOptions, new Random(9)), maxLeap: 4);
    }

    [Fact]
    public void Compose_KeySignaturePresets_SameSeedIsDeterministic()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.GMajor,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(77));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(77));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void Compose_GrandStaff_AlternatesTrebleAndBassStartingWithTreble()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(13));

        Staff[] staves = score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Staff).ToArray();
        Assert.Equal(
            [Staff.Treble, Staff.Bass, Staff.Treble, Staff.Bass, Staff.Treble, Staff.Bass, Staff.Treble, Staff.Bass],
            staves);
    }

    [Fact]
    public void Compose_GrandStaff_GivesBothStavesEqualRepresentation()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(13));

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Equal(8, notes.Count(note => note.Staff == Staff.Treble));
        Assert.Equal(8, notes.Count(note => note.Staff == Staff.Bass));
    }

    [Fact]
    public void Compose_GrandStaff_UsesEachStaffsOwnPresetRangeAndLeapLimit()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(13));

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.All(
            notes.Where(note => note.Staff == Staff.Treble),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 4).DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 5).DiatonicIndex));
        Assert.All(
            notes.Where(note => note.Staff == Staff.Bass),
            note => Assert.InRange(
                note.Pitch.DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 3).DiatonicIndex,
                new Pitch(NoteLetter.C, 0, 4).DiatonicIndex));

        int[] trebleIndexes = notes.Where(note => note.Staff == Staff.Treble)
            .Select(note => note.Pitch.DiatonicIndex).ToArray();
        int[] bassIndexes = notes.Where(note => note.Staff == Staff.Bass)
            .Select(note => note.Pitch.DiatonicIndex).ToArray();
        Assert.All(
            trebleIndexes.Zip(trebleIndexes.Skip(1)),
            pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 1, 4));
        Assert.All(
            bassIndexes.Zip(bassIndexes.Skip(1)),
            pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 1, 4));
    }

    [Fact]
    public void Compose_GrandStaff_SameSeedIsDeterministic()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(444));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(444));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => (note.Pitch, note.Staff)),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => (note.Pitch, note.Staff)));
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_Chords_ComposesTriadsWithThreeDistinctPitchesPerPrompt(Staff staff)
    {
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.Chords,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            var notesByBeat = score.Measures[measureIndex].Notes.GroupBy(note => note.BeatOffset);
            foreach (var chordNotes in notesByBeat)
            {
                Pitch[] distinctPitches = chordNotes.Select(note => note.Pitch).Distinct().ToArray();
                Assert.Equal(3, distinctPitches.Length);
            }
        }
    }

    [Fact]
    public void Compose_Chords_UsesIivAndVTriadsOfCMajorInAnyInversion()
    {
        // Tightened from "exactly the three root-position triads" now that the preset also uses their inversions: it
        // still means the I, IV and V chords, so each prompt must be made of exactly those notes' letters.
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);
        var expectedTriads = new[]
        {
            new HashSet<NoteLetter> { NoteLetter.C, NoteLetter.E, NoteLetter.G },
            new HashSet<NoteLetter> { NoteLetter.F, NoteLetter.A, NoteLetter.C },
            new HashSet<NoteLetter> { NoteLetter.G, NoteLetter.B, NoteLetter.D },
        };

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        foreach (var chordNotes in score.Measures.SelectMany(measure => measure.Notes).GroupBy(
            note => (note.MeasureIndex, note.BeatOffset)))
        {
            var playedLetters = chordNotes.Select(note => note.Pitch.Letter).ToHashSet();
            Assert.Contains(expectedTriads, expectedTriad => expectedTriad.SetEquals(playedLetters));
        }
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_Chords_NeedAtMostTwoLedgerLinesOnTheirStaff(Staff staff)
    {
        // The same limit the key presets keep. First-inversion V (B3 D4 G4) put G4 on the bass staff's third ledger line.
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        for (int seed = 0; seed < 20; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(options, new Random(seed));

            Assert.All(
                score.Measures.SelectMany(measure => measure.Notes),
                note => Assert.InRange(CountLedgerLines(note.Pitch, staff), 0, 2));
        }
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void Compose_Chords_LongExerciseUsesAllNineVoicingsEachSortedOnTheStaff(Staff staff)
    {
        // Root position, first inversion, second inversion of I, IV and V, each in close position, written out as
        // literals, not derived from the composer's table. Treble: the lowest note is in the octave above C4. Bass: the
        // same shapes an octave lower, except first-inversion V, which would reach the third ledger line at B3 D4 G4 and is
        // voiced a further octave down (B2 D3 G3), so all nine stay available within two ledger lines.
        string[] expected = (staff == Staff.Treble
            ? new[]
            {
                "C4 E4 G4", "E4 G4 C5", "G4 C5 E5",
                "F4 A4 C5", "A4 C5 F5", "C4 F4 A4",
                "G4 B4 D5", "B4 D5 G5", "D4 G4 B4",
            }
            : new[]
            {
                "C3 E3 G3", "E3 G3 C4", "G3 C4 E4",
                "F3 A3 C4", "A3 C4 F4", "C3 F3 A3",
                "G3 B3 D4", "B2 D3 G3", "D3 G3 B3",
            })
            .Order()
            .ToArray();
        var options = new SightReadingExerciseOptions(
            staff,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        for (int seed = 0; seed < 20; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(options, new Random(seed));

            string[] voicings = score.Measures
                .SelectMany(measure => measure.Notes)
                .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
                .Select(group => string.Join(" ", group.Select(note => note.Pitch.ToString())))
                .ToArray();
            Assert.Equal(expected, voicings.Distinct().Order());
            Assert.All(
                score.Measures.SelectMany(measure => measure.Notes).GroupBy(note => (note.MeasureIndex, note.BeatOffset)),
                group => Assert.Equal(
                    group.Select(note => note.Pitch.DiatonicIndex).Order(),
                    group.Select(note => note.Pitch.DiatonicIndex)));
        }
    }

    [Fact]
    public void Compose_Chords_NeverRepeatsTheSameVoicingImmediatelyAndUsesTheLeastUsedOneFirst()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        for (int seed = 0; seed < 30; seed++)
        {
            string[] voicings = SightReadingExerciseComposer.Compose(options, new Random(seed)).Measures
                .SelectMany(measure => measure.Notes)
                .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
                .Select(group => string.Join(" ", group.Select(note => note.Pitch.ToString())))
                .ToArray();

            Assert.All(voicings.Zip(voicings.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
            // Least-used selection: the first nine prompts are nine different voicings, the rest then repeat evenly.
            Assert.Equal(9, voicings.Take(9).Distinct().Count());
            Assert.All(voicings.GroupBy(voicing => voicing), group => Assert.InRange(group.Count(), 1, 2));
        }
    }

    [Fact]
    public void Compose_Chords_NeverRepeatsSameTriadImmediately()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        HashSet<Pitch>[] chords = score.Measures.SelectMany(measure => measure.Notes)
            .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
            .OrderBy(group => group.Key.MeasureIndex).ThenBy(group => group.Key.BeatOffset)
            .Select(group => group.Select(note => note.Pitch).ToHashSet())
            .ToArray();

        Assert.All(
            chords.Zip(chords.Skip(1)),
            pair => Assert.False(pair.First.SetEquals(pair.Second)));
    }

    [Fact]
    public void Compose_Chords_AllNotesUseTheSelectedStaff()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingPresetId.Chords,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Equal(Staff.Bass, note.Staff));
    }

    [Fact]
    public void Compose_Chords_PromptCountMatchesChordCountNotNoteCount()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        int chordCount = score.Measures.SelectMany(measure => measure.Notes)
            .Select(note => (note.MeasureIndex, note.BeatOffset))
            .Distinct()
            .Count();
        Assert.Equal(8, chordCount);
        Assert.Equal(24, score.Measures.SelectMany(measure => measure.Notes).Count());
    }

    [Fact]
    public void Compose_Chords_SameSeedIsDeterministic()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(88));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(88));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void Compose_BasicRhythm_UsesFourFourTimeSignatureWithExactBeatTotalsPerMeasure()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Basic);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        AssertEachMeasureHasExactBeatTotal(score, expectedBeatsPerMeasure: 4);
    }

    [Fact]
    public void Compose_CompoundRhythm_UsesSixEightTimeSignatureWithExactBeatTotalsPerMeasure()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Compound);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        Assert.Equal(new TimeSignature(6, new NoteValue(8)), score.TimeSignature);
        AssertEachMeasureHasExactBeatTotal(score, expectedBeatsPerMeasure: 6);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Basic)]
    [InlineData(SightReadingRhythmPreset.Compound)]
    [InlineData(SightReadingRhythmPreset.Extended)]
    [InlineData(SightReadingRhythmPreset.ThreeFour)]
    [InlineData(SightReadingRhythmPreset.TwoFour)]
    [InlineData(SightReadingRhythmPreset.Syncopated)]
    public void Compose_Rhythm_GeneratesAtLeastTheRequestedPromptCount(SightReadingRhythmPreset rhythmPreset)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: rhythmPreset);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        int noteCount = score.Measures.SelectMany(measure => measure.Notes).Count();
        Assert.True(noteCount >= 8, $"Expected at least 8 note prompts, got {noteCount}.");
    }

    [Fact]
    public void Compose_BasicRhythm_OnlyUsesHalfQuarterAndEighthNoteValues()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Basic);
        NoteValue[] allowedValues = [new NoteValue(2), new NoteValue(4), new NoteValue(8)];

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Contains(note.NoteValue, allowedValues));
        Assert.All(
            score.Measures.SelectMany(measure => measure.Rests),
            rest => Assert.Equal(new NoteValue(4), rest.NoteValue));
    }

    [Fact]
    public void Compose_CompoundRhythm_OnlyUsesTheCompoundNoteAndRestValues()
    {
        // Widened from "dotted quarter and eighth only": the extended 6/8 catalog adds quarter notes, a dotted half and
        // an eighth rest.
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Compound);
        NoteValue[] allowedValues = [new NoteValue(2, dots: 1), new NoteValue(4, dots: 1), new NoteValue(4), new NoteValue(8)];

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Contains(note.NoteValue, allowedValues));
        Assert.All(
            score.Measures.SelectMany(measure => measure.Rests),
            rest => Assert.Equal(new NoteValue(8), rest.NoteValue));
    }

    [Fact]
    public void Compose_BasicRhythm_BeamedEighthPairsHaveConsistentBeamState()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Basic);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        AssertBeamedEighthNotesFormValidGroups(score);
    }

    [Fact]
    public void Compose_CompoundRhythm_BeamedEighthGroupsHaveConsistentBeamState()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Compound);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        // Lone eighths (beside a quarter) are allowed now that the 6/8 catalog has quarter + eighth groupings.
        AssertBeamedEighthNotesFormValidGroups(score, allowLoneEighths: true);
    }

    /// <summary>One measure's rhythm as text, e.g. "H. Q" or "Q r8 8 H" (a leading "r" marks a rest, a trailing "~" a tie to the next note), for pattern checks.</summary>
    private static string DescribeMeasureRhythm(ScoreMeasure measure)
    {
        static string Name(NoteValue value) =>
            value.Denominator switch
            {
                1 => "W",
                2 => "H",
                4 => "Q",
                8 => "8",
                _ => throw new ArgumentOutOfRangeException(nameof(value)),
            } + new string('.', value.Dots);

        return string.Join(
            " ",
            measure.Notes.Select(note => (note.BeatOffset, Text: Name(note.NoteValue) + (note.TiesToNext ? "~" : string.Empty)))
                .Concat(measure.Rests.Select(rest => (rest.BeatOffset, Text: "r" + Name(rest.NoteValue))))
                .OrderBy(item => item.BeatOffset)
                .Select(item => item.Text));
    }

    [Fact]
    public void Compose_ExtendedRhythm_UsesFourFourWithExactBeatTotalsAndTheSlowTempoDefault()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Extended),
            new Random(5));

        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(new Tempo(60), score.Tempo);
        AssertEachMeasureHasExactBeatTotal(score, expectedBeatsPerMeasure: 4);
    }

    [Fact]
    public void Compose_ExtendedRhythm_UsesEveryPatternAcrossALongExerciseAndBalancesThem()
    {
        string[] expectedPatterns = ["H. Q", "Q H.", "W", "Q. 8 H", "Q r8 8 H"];

        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 60,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Extended),
            new Random(8));

        string[] patterns = score.Measures.Select(DescribeMeasureRhythm).ToArray();
        Assert.Equal(expectedPatterns.Order(), patterns.Distinct().Order());
        int[] counts = patterns.GroupBy(pattern => pattern).Select(group => group.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1, $"Pattern usage is unbalanced: {string.Join(",", counts)}");
        Assert.All(patterns.Zip(patterns.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_ExtendedRhythm_WritesNoBeamsBecauseNoPatternBeamsTwoNotes()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 40,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Extended),
            new Random(2));

        Assert.All(score.Measures.SelectMany(measure => measure.Notes), note => Assert.Equal(BeamState.None, note.BeamState));
        Assert.All(
            score.Measures.SelectMany(measure => measure.Rests),
            rest => Assert.Equal(new NoteValue(8), rest.NoteValue));
        Assert.Contains(score.Measures.SelectMany(measure => measure.Rests), _ => true);
    }

    [Fact]
    public void Compose_ExtendedRhythm_NotesKeepEachPatternsOnsets()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 60,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Extended),
            new Random(8));

        ScoreMeasure dottedFigure = score.Measures.First(measure => DescribeMeasureRhythm(measure) == "Q. 8 H");
        ScoreMeasure eighthRest = score.Measures.First(measure => DescribeMeasureRhythm(measure) == "Q r8 8 H");
        Assert.Equal([0d, 1.5, 2], dottedFigure.Notes.Select(note => note.BeatOffset));
        Assert.Equal([0d, 1.5, 2], eighthRest.Notes.Select(note => note.BeatOffset));
        Assert.Equal(1, Assert.Single(eighthRest.Rests).BeatOffset);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.ThreeFour, 3, "Q Q Q|H Q|Q H|H.|8 8 Q Q|Q 8 8 Q|Q rQ Q")]
    [InlineData(SightReadingRhythmPreset.TwoFour, 2, "Q Q|H|8 8 Q|Q 8 8|Q rQ")]
    public void Compose_SimpleMeterRhythm_UsesItsOwnTimeSignatureAndEveryPatternWithExactBeatTotals(
        SightReadingRhythmPreset rhythmPreset,
        int beatsPerMeasure,
        string expectedPatterns)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 60,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: rhythmPreset),
            new Random(8));

        Assert.Equal(new TimeSignature(beatsPerMeasure, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(new Tempo(60), score.Tempo);
        AssertEachMeasureHasExactBeatTotal(score, beatsPerMeasure);
        string[] patterns = score.Measures.Select(DescribeMeasureRhythm).ToArray();
        Assert.Equal(expectedPatterns.Split('|').Order(), patterns.Distinct().Order());
        int[] counts = patterns.GroupBy(pattern => pattern).Select(group => group.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1, $"Pattern usage is unbalanced: {string.Join(",", counts)}");
        Assert.All(patterns.Zip(patterns.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.ThreeFour)]
    [InlineData(SightReadingRhythmPreset.TwoFour)]
    public void Compose_SimpleMeterRhythm_BeamsEighthPairsExplicitlyAndRestsOnlyOnQuarters(
        SightReadingRhythmPreset rhythmPreset)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Bass,
                SightReadingPresetId.OneOctave,
                PromptCount: 40,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: rhythmPreset),
            new Random(2));

        AssertBeamedEighthNotesFormValidGroups(score);
        Assert.All(
            score.Measures.SelectMany(measure => measure.Rests),
            rest => Assert.Equal(new NoteValue(4), rest.NoteValue));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.ThreeFour, 90, 90)]
    [InlineData(SightReadingRhythmPreset.TwoFour, 45, 45)]
    public void Compose_SimpleMeterRhythm_TempoPulseIsTheQuarterNote(
        SightReadingRhythmPreset rhythmPreset,
        int pulses,
        double expectedBeatsPerMinute)
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 8,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: rhythmPreset,
                TempoPulsesPerMinute: pulses),
            new Random(1));

        Assert.Equal(new Tempo(expectedBeatsPerMinute), score.Tempo);
    }

    [Fact]
    public void Compose_ThreeFour_AnOnsetGradedSessionGradesAgainstTheThreeBeatMeasure()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.ThreeFour),
            new Random(3));
        ScoreNote secondMeasureFirstNote = score.Measures[1].Notes.OrderBy(note => note.BeatOffset).First();
        var onTime = new NoteReadingSession();
        onTime.Reset(score, NoteReadingMode.PitchAndRhythm, TimeSpan.FromMilliseconds(60), TimeSpan.Zero);
        var early = new NoteReadingSession();
        early.Reset(score, NoteReadingMode.PitchAndRhythm, TimeSpan.FromMilliseconds(60), TimeSpan.Zero);

        // At 60 pulses a 3/4 measure lasts exactly 3 seconds, so the second measure starts at 3.0 s. Play up to there.
        foreach (NoteReadingSession session in new[] { onTime, early })
        {
            foreach (ScoreNote note in score.Measures[0].Notes.OrderBy(note => note.BeatOffset))
            {
                session.Check(note.Pitch, TimeSpan.FromSeconds(note.BeatOffset));
            }
        }

        onTime.Check(secondMeasureFirstNote.Pitch, TimeSpan.FromSeconds(3));
        early.Check(secondMeasureFirstNote.Pitch, TimeSpan.FromSeconds(2));

        NoteReadingPromptResult onTimeResult = onTime.PromptResults.First(result => result.OnsetBeats == 3);
        NoteReadingPromptResult earlyResult = early.PromptResults.First(result => result.OnsetBeats == 3);
        Assert.Equal(Verdict.Correct, onTimeResult.OnsetVerdict);
        Assert.Equal(Verdict.Early, earlyResult.OnsetVerdict);
    }

    [Fact]
    public void Compose_ThreeFour_ADottedHalfIsHeldForThreeBeatsInAHoldGradedSession()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 60,
                NoteReadingMode.PitchAndHold,
                RhythmPreset: SightReadingRhythmPreset.ThreeFour),
            new Random(3));
        ScoreNote dottedHalf = score.Measures.SelectMany(measure => measure.Notes)
            .First(note => note.NoteValue == new NoteValue(2, dots: 1));
        var held = new NoteReadingSession();
        var lifted = new NoteReadingSession();
        held.Reset(score, NoteReadingMode.PitchAndHold, TimeSpan.FromMilliseconds(60));
        lifted.Reset(score, NoteReadingMode.PitchAndHold, TimeSpan.FromMilliseconds(60));
        double onset = ScoreDerivation.GetOnsetBeats(dottedHalf, score.TimeSignature);

        // Play every earlier prompt cleanly, then the dotted half: held 3 s (right) versus lifted after 1 s (too short).
        foreach (NoteReadingSession session in new[] { held, lifted })
        {
            foreach (ScoreNote earlier in score.Measures.SelectMany(measure => measure.Notes)
                .Where(note => ScoreDerivation.GetOnsetBeats(note, score.TimeSignature) < onset)
                .OrderBy(note => ScoreDerivation.GetOnsetBeats(note, score.TimeSignature)))
            {
                double earlierOnset = ScoreDerivation.GetOnsetBeats(earlier, score.TimeSignature);
                session.Check(earlier.Pitch, TimeSpan.FromSeconds(earlierOnset));
                session.Release(
                    earlier.Pitch,
                    TimeSpan.FromSeconds(earlierOnset + MusicalTime.GetBeats(earlier.NoteValue, score.TimeSignature)));
            }
        }

        held.Check(dottedHalf.Pitch, TimeSpan.FromSeconds(onset));
        held.Release(dottedHalf.Pitch, TimeSpan.FromSeconds(onset + 3));
        lifted.Check(dottedHalf.Pitch, TimeSpan.FromSeconds(onset));
        lifted.Release(dottedHalf.Pitch, TimeSpan.FromSeconds(onset + 1));

        Assert.Equal(Verdict.Correct, held.PromptResults.First(result => result.OnsetBeats == onset).DurationVerdict);
        Assert.Equal(Verdict.TooShort, lifted.PromptResults.First(result => result.OnsetBeats == onset).DurationVerdict);
    }

    private static Score ComposeSyncopated(
        int promptCount,
        int seed,
        NoteReadingMode mode = NoteReadingMode.PitchAndRhythm,
        SightReadingPresetId preset = SightReadingPresetId.OneOctave,
        Staff staff = Staff.Treble) =>
        SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                staff,
                preset,
                promptCount,
                mode,
                RhythmPreset: SightReadingRhythmPreset.Syncopated),
            new Random(seed));

    [Fact]
    public void Compose_SyncopatedRhythm_UsesEveryPatternWithExactBeatTotalsAndBalancedUsage()
    {
        string[] expectedPatterns =
        [
            "Q 8 Q 8 Q", "8 Q 8 Q Q", "Q Q~ Q Q", "Q 8 8~ Q Q", "Q Q Q 8 8~", "H Q Q~", "Q Q H", "H Q Q",
        ];

        Score score = ComposeSyncopated(promptCount: 100, seed: 8);

        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        AssertEachMeasureHasExactBeatTotal(score, expectedBeatsPerMeasure: 4);
        string[] patterns = score.Measures.Select(DescribeMeasureRhythm).ToArray();
        Assert.Equal(expectedPatterns.Order(), patterns.Distinct().Order());
        int[] counts = patterns.GroupBy(pattern => pattern).Select(group => group.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1, $"Pattern usage is unbalanced: {string.Join(",", counts)}");
        Assert.All(patterns.Zip(patterns.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_SyncopatedRhythm_EveryTieIsContinuedBySameStaffAndPitchNoteWhereTheFirstEnds()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            Score score = ComposeSyncopated(promptCount: 16, seed);
            ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();

            Assert.True(notes.Any(note => note.TiesToNext), $"seed {seed} produced no tie at all");
            foreach (ScoreNote tied in notes.Where(note => note.TiesToNext))
            {
                double end = ScoreDerivation.GetOnsetBeats(tied, score.TimeSignature) +
                    MusicalTime.GetBeats(tied.NoteValue, score.TimeSignature);
                Assert.Contains(
                    notes,
                    candidate => candidate.Pitch == tied.Pitch &&
                        candidate.Staff == tied.Staff &&
                        Math.Abs(ScoreDerivation.GetOnsetBeats(candidate, score.TimeSignature) - end) < 1e-9);
            }

            // A tie across the barline is never left open at the end of the exercise.
            Assert.False(score.Measures[^1].Notes.OrderBy(note => note.BeatOffset).Last().TiesToNext);
        }
    }

    [Fact]
    public void Compose_SyncopatedRhythm_ATiedPairIsOnePromptWhoseDurationsAddUp()
    {
        Score score = ComposeSyncopated(promptCount: 100, seed: 3);

        IReadOnlyList<ScoreEvent> events = ScoreDerivation.Flatten(score);
        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        // Every tied note is continued by exactly one more note, which is not a prompt of its own.
        Assert.Equal(notes.Length - notes.Count(note => note.TiesToNext), events.Count);
        Assert.True(events.Count >= 100);
        Assert.Contains(events, scoreEvent => scoreEvent.SourceNotes.Count == 2);
        Assert.All(
            events,
            scoreEvent => Assert.Equal(
                scoreEvent.SourceNotes.Sum(note => MusicalTime.GetBeats(note.NoteValue, score.TimeSignature)),
                scoreEvent.DurationBeats,
                precision: 9));
    }

    [Fact]
    public void Compose_SyncopatedRhythm_UsesOneNewPitchPerAttackAndRepeatsItAcrossATie()
    {
        Score score = ComposeSyncopated(promptCount: 40, seed: 6);
        ScoreEvent[] events = ScoreDerivation.Flatten(score).ToArray();

        // Every event is one pitch (a tied pair shares it) and consecutive events never repeat a pitch.
        Assert.All(events.Zip(events.Skip(1)), pair => Assert.NotEqual(pair.First.Pitch, pair.Second.Pitch));
        Assert.All(events, scoreEvent => Assert.All(scoreEvent.SourceNotes, note => Assert.Equal(scoreEvent.Pitch, note.Pitch)));
    }

    [Fact]
    public void Compose_SyncopatedRhythm_SameSeedIsDeterministic()
    {
        Score first = ComposeSyncopated(promptCount: 24, seed: 11);
        Score second = ComposeSyncopated(promptCount: 24, seed: 11);

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => (note.Pitch, note.NoteValue, note.MeasureIndex, note.BeatOffset, note.TiesToNext, note.BeamState)),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => (note.Pitch, note.NoteValue, note.MeasureIndex, note.BeatOffset, note.TiesToNext, note.BeamState)));
    }

    [Fact]
    public void Compose_SyncopatedRhythm_BeamsOnlyAdjacentEighthPairsAndLeavesOtherEighthsFlagged()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            Score score = ComposeSyncopated(promptCount: 40, seed);

            foreach (ScoreMeasure measure in score.Measures)
            {
                ScoreNote[] notes = measure.Notes.OrderBy(note => note.BeatOffset).ToArray();
                for (int index = 0; index < notes.Length; index++)
                {
                    if (notes[index].BeamState != BeamState.None)
                    {
                        Assert.Equal(new NoteValue(8), notes[index].NoteValue);
                    }

                    if (notes[index].BeamState == BeamState.Begin)
                    {
                        Assert.Equal(BeamState.End, notes[index + 1].BeamState);
                        Assert.Equal(notes[index].BeatOffset + 0.5, notes[index + 1].BeatOffset);
                    }

                    if (notes[index].BeamState == BeamState.End)
                    {
                        Assert.Equal(BeamState.Begin, notes[index - 1].BeamState);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(Staff.Treble, SightReadingPresetId.Accidentals)]
    [InlineData(Staff.Bass, SightReadingPresetId.Accidentals)]
    public void Compose_SyncopatedWithAccidentals_NeverLeavesAPlainNoteAfterATiedAlteredNote(
        Staff staff,
        SightReadingPresetId preset)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            Score score = ComposeSyncopated(promptCount: 16, seed, preset: preset, staff: staff);

            AssertNoAmbiguousAccidentals(score);
        }
    }

    [Fact]
    public void ComposeFromMissedMeasures_ATieWhoseContinuationIsNotReplayed_LosesItsTieSoNoCurveDangles()
    {
        Score score = ComposeSyncopated(promptCount: 40, seed: 2);
        int crossBarlineMeasure = Enumerable.Range(0, score.Measures.Count - 1)
            .First(index => score.Measures[index].Notes.Any(note => note.BeatOffset >= 3 && note.TiesToNext));
        ScoreNote earlierNote = score.Measures[crossBarlineMeasure].Notes.First(note => !note.TiesToNext);

        Score replay = SightReadingExerciseComposer.ComposeFromMissedMeasures(score, [earlierNote]);

        Assert.Single(replay.Measures);
        Assert.DoesNotContain(replay.Measures[0].Notes, note => note.TiesToNext);
    }

    [Fact]
    public void ComposeFromMissedMeasures_BothMeasuresOfACrossBarlineTie_KeepTheTie()
    {
        Score score = ComposeSyncopated(promptCount: 40, seed: 2);
        int crossBarlineMeasure = Enumerable.Range(0, score.Measures.Count - 1)
            .First(index => score.Measures[index].Notes.Any(note => note.BeatOffset >= 3 && note.TiesToNext));
        ScoreNote tied = score.Measures[crossBarlineMeasure].Notes.First(note => note.TiesToNext && note.BeatOffset >= 3);
        ScoreNote continuation = score.Measures[crossBarlineMeasure + 1].Notes.OrderBy(note => note.BeatOffset).First();

        Score replay = SightReadingExerciseComposer.ComposeFromMissedMeasures(score, [tied, continuation]);

        Assert.Equal(2, replay.Measures.Count);
        Assert.Contains(replay.Measures[0].Notes, note => note.TiesToNext && note.BeatOffset >= 3);
        Assert.Equal(tied.Pitch, replay.Measures[1].Notes.OrderBy(note => note.BeatOffset).First().Pitch);
    }

    [Fact]
    public void ComposeFromMissedPrompts_ATiedPair_BecomesOneQuarterNoteNotTwoOnTopOfEachOther()
    {
        Score original = CreateOriginalScore();
        ScoreNote first = CreateSourceNote(NoteLetter.E, Staff.Treble);
        ScoreNote continuation = first with { MeasureIndex = 4, BeatOffset = 0, TiesToNext = false };

        Score missedScore = SightReadingExerciseComposer.ComposeFromMissedPrompts(original, [[first, continuation]]);

        ScoreNote only = Assert.Single(missedScore.Measures.SelectMany(measure => measure.Notes));
        Assert.Equal(first.Pitch, only.Pitch);
        Assert.False(only.TiesToNext);
    }

    private static Score ComposeCompound(int promptCount, int seed, NoteReadingMode mode = NoteReadingMode.PitchAndRhythm) =>
        SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                promptCount,
                mode,
                RhythmPreset: SightReadingRhythmPreset.Compound),
            new Random(seed));

    [Fact]
    public void Compose_CompoundRhythm_UsesEveryPatternOfTheExtendedCatalogAndBalancesThem()
    {
        string[] expectedPatterns =
        [
            "Q. Q.", "8 8 8 Q.", "Q. 8 8 8", "8 8 8 8 8 8",
            "Q 8 Q 8", "Q 8 Q.", "Q. Q 8", "r8 8 8 Q.", "H.",
        ];

        Score score = ComposeCompound(promptCount: 100, seed: 8);

        AssertEachMeasureHasExactBeatTotal(score, expectedBeatsPerMeasure: 6);
        string[] patterns = score.Measures.Select(DescribeMeasureRhythm).ToArray();
        Assert.Equal(expectedPatterns.Order(), patterns.Distinct().Order());
        int[] counts = patterns.GroupBy(pattern => pattern).Select(group => group.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1, $"Pattern usage is unbalanced: {string.Join(",", counts)}");
        Assert.All(patterns.Zip(patterns.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_CompoundRhythm_OnlyTheDottedHalfCrossesTheMiddleOfTheBarAndBeamsStayInsideAGroup()
    {
        Score score = ComposeCompound(promptCount: 100, seed: 3);

        foreach (ScoreMeasure measure in score.Measures)
        {
            ScoreNote[] notes = measure.Notes.OrderBy(note => note.BeatOffset).ToArray();
            foreach (ScoreNote note in notes)
            {
                double end = note.BeatOffset + MusicalTime.GetBeats(note.NoteValue, score.TimeSignature);
                bool crossesTheMiddle = note.BeatOffset < 3 && end > 3;
                Assert.Equal(note.NoteValue == new NoteValue(2, dots: 1), crossesTheMiddle);
            }

            for (int index = 0; index < notes.Length; index++)
            {
                if (notes[index].BeamState == BeamState.Begin)
                {
                    int endIndex = index;
                    while (notes[endIndex].BeamState != BeamState.End)
                    {
                        endIndex++;
                    }

                    Assert.Equal(notes[index].BeatOffset < 3, notes[endIndex].BeatOffset < 3);
                }
            }
        }
    }

    [Fact]
    public void Compose_CompoundRhythm_AQuarterWithAnEighthHasNoBeamAndTheEighthRestHasItsOwnBeamedPair()
    {
        Score score = ComposeCompound(promptCount: 100, seed: 5);

        ScoreMeasure quarterAndEighth = score.Measures.First(measure => DescribeMeasureRhythm(measure) == "Q 8 Q.");
        ScoreMeasure eighthRest = score.Measures.First(measure => DescribeMeasureRhythm(measure) == "r8 8 8 Q.");
        Assert.All(quarterAndEighth.Notes, note => Assert.Equal(BeamState.None, note.BeamState));
        Assert.Equal(0d, Assert.Single(eighthRest.Rests).BeatOffset);
        Assert.Equal([BeamState.Begin, BeamState.End, BeamState.None], eighthRest.Notes.OrderBy(note => note.BeatOffset).Select(note => note.BeamState));
    }

    [Fact]
    public void Compose_CompoundRhythm_TempoCountInAndAccentsStayOnTheDottedQuarterPulse()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.PitchAndRhythm,
                RhythmPreset: SightReadingRhythmPreset.Compound,
                TempoPulsesPerMinute: 50),
            new Random(1));

        // 50 dotted-quarter pulses are 150 eighth-note beats per minute: a measure of six beats lasts 2.4 s.
        Assert.Equal(new Tempo(150), score.Tempo);
        Assert.Equal(TimeSpan.FromSeconds(2.4), MusicalTime.BeatsToDuration(score.TimeSignature.Numerator, score.Tempo));
        Assert.Equal(3, new MetronomeGrid(TimeSpan.Zero, score.Tempo, score.TimeSignature).BeatsPerGroup);
    }

    [Fact]
    public void Compose_ExtendedRhythmOnly_RepeatsTheCentreLinePitch()
    {
        Score score = SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Bass,
                SightReadingPresetId.FiveNote,
                PromptCount: 16,
                NoteReadingMode.RhythmOnly,
                RhythmPreset: SightReadingRhythmPreset.Extended),
            new Random(4));

        Assert.All(PitchesOf(score), pitch => Assert.Equal(new Pitch(NoteLetter.D, 0, 3), pitch));
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Basic)]
    [InlineData(SightReadingRhythmPreset.Compound)]
    [InlineData(SightReadingRhythmPreset.Extended)]
    [InlineData(SightReadingRhythmPreset.ThreeFour)]
    [InlineData(SightReadingRhythmPreset.TwoFour)]
    [InlineData(SightReadingRhythmPreset.Syncopated)]
    public void Compose_Rhythm_SameSeedIsDeterministic(SightReadingRhythmPreset rhythmPreset)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: rhythmPreset);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(303));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(303));

        Assert.Equal(first.Measures.Count, second.Measures.Count);
        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes)
                .Select(note => (note.Pitch, note.NoteValue, note.MeasureIndex, note.BeatOffset, note.BeamState)),
            second.Measures.SelectMany(measure => measure.Notes)
                .Select(note => (note.Pitch, note.NoteValue, note.MeasureIndex, note.BeatOffset, note.BeamState)));
        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Rests)
                .Select(rest => (rest.NoteValue, rest.MeasureIndex, rest.BeatOffset)),
            second.Measures.SelectMany(measure => measure.Rests)
                .Select(rest => (rest.NoteValue, rest.MeasureIndex, rest.BeatOffset)));
    }

    [Fact]
    public void Options_Motion_DefaultsToRandomWithAThirdAsTheIntervalSize()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder);

        Assert.Equal(SightReadingMotion.Random, options.Motion);
        Assert.Equal(2, options.IntervalSteps);
    }

    [Fact]
    public void Compose_ExplicitRandomMotion_EqualsTheDefault()
    {
        Assert.Equal(
            Sequence(Staff.Treble, SightReadingPresetId.OneOctave, 16, seed: 21),
            MotionSequence(Staff.Treble, SightReadingPresetId.OneOctave, SightReadingMotion.Random, seed: 21));
    }

    [Theory]
    [InlineData(SightReadingPresetId.FiveNote, Staff.Treble)]
    [InlineData(SightReadingPresetId.OneOctave, Staff.Bass)]
    [InlineData(SightReadingPresetId.GMajor, Staff.Treble)]
    [InlineData(SightReadingPresetId.FMajor, Staff.Bass)]
    public void Compose_Melodic_StaysInsideThePaletteAndNeverMovesMoreThanAThird(
        SightReadingPresetId preset,
        Staff staff)
    {
        IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(staff, preset);

        for (int seed = 0; seed < 100; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(staff, preset, SightReadingMotion.Melodic, promptCount: 16),
                new Random(seed)));

            Assert.Equal(16, pitches.Length);
            Assert.All(pitches, pitch => Assert.Contains(pitch, palette));
            Assert.All(
                pitches.Zip(pitches.Skip(1)),
                pair => Assert.InRange(Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex), 0, 2));
        }
    }

    [Fact]
    public void Compose_Melodic_RepeatsAboutAQuarterOfTheNotesAndSometimesSkipsAThird()
    {
        int moves = 0;
        int repeats = 0;
        int skips = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, SightReadingPresetId.OneOctave, SightReadingMotion.Melodic, promptCount: 16),
                new Random(seed)));
            foreach ((Pitch first, Pitch second) in pitches.Zip(pitches.Skip(1)))
            {
                int distance = Math.Abs(first.DiatonicIndex - second.DiatonicIndex);
                moves++;
                repeats += distance == 0 ? 1 : 0;
                skips += distance == 2 ? 1 : 0;
            }
        }

        Assert.InRange((double)repeats / moves, 0.15, 0.35);
        Assert.InRange((double)skips / moves, 0.05, 0.25);
    }

    [Fact]
    public void Compose_Melodic_MostlyKeepsGoingTheSameWayBeforeTurning()
    {
        int sameDirection = 0;
        int comparisons = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, SightReadingPresetId.OneOctave, SightReadingMotion.Melodic, promptCount: 16),
                new Random(seed)));
            int[] steps = pitches
                .Zip(pitches.Skip(1), (first, second) => second.DiatonicIndex - first.DiatonicIndex)
                .Where(step => step != 0)
                .ToArray();
            foreach ((int first, int second) in steps.Zip(steps.Skip(1)))
            {
                comparisons++;
                sameDirection += Math.Sign(first) == Math.Sign(second) ? 1 : 0;
            }
        }

        Assert.True((double)sameDirection / comparisons > 0.6, $"{sameDirection} of {comparisons} moves kept their direction");
    }

    [Theory]
    [InlineData(SightReadingPresetId.FiveNote, Staff.Treble, 1)]
    [InlineData(SightReadingPresetId.FiveNote, Staff.Bass, 2)]
    [InlineData(SightReadingPresetId.FiveNote, Staff.Treble, 3)]
    [InlineData(SightReadingPresetId.FiveNote, Staff.Bass, 4)]
    [InlineData(SightReadingPresetId.OneOctave, Staff.Treble, 3)]
    [InlineData(SightReadingPresetId.GMajor, Staff.Treble, 4)]
    [InlineData(SightReadingPresetId.FMajor, Staff.Bass, 2)]
    public void Compose_Intervallic_EveryLeapIsExactlyTheChosenNumberOfDiatonicSteps(
        SightReadingPresetId preset,
        Staff staff,
        int intervalSteps)
    {
        IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(staff, preset);

        for (int seed = 0; seed < 100; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(staff, preset, SightReadingMotion.Intervallic, promptCount: 16, intervalSteps),
                new Random(seed)));

            Assert.Equal(16, pitches.Length);
            Assert.All(pitches, pitch => Assert.Contains(pitch, palette));
            Assert.All(
                pitches.Zip(pitches.Skip(1)),
                pair => Assert.Equal(intervalSteps, Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex)));
        }
    }

    [Fact]
    public void Compose_Intervallic_UsesBothDirectionsWhereThePaletteAllowsIt()
    {
        var directions = new HashSet<int>();
        for (int seed = 0; seed < 50; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, SightReadingPresetId.OneOctave, SightReadingMotion.Intervallic, 16, 2),
                new Random(seed)));
            foreach ((Pitch first, Pitch second) in pitches.Zip(pitches.Skip(1)))
            {
                directions.Add(Math.Sign(second.DiatonicIndex - first.DiatonicIndex));
            }
        }

        Assert.Equal([-1, 1], directions.Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Compose_IntervallicWithUnsupportedIntervalSize_Throws(int intervalSteps)
    {
        SightReadingExerciseOptions options = MotionOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            SightReadingMotion.Intervallic,
            promptCount: 8,
            intervalSteps);

        Assert.Throws<ArgumentOutOfRangeException>(() => SightReadingExerciseComposer.Compose(options, new Random(1)));
    }

    [Theory]
    [InlineData(SightReadingMotion.Melodic)]
    [InlineData(SightReadingMotion.Intervallic)]
    public void Compose_MelodicAndIntervallic_SameSeedIsDeterministic(SightReadingMotion motion)
    {
        Assert.Equal(
            MotionSequence(Staff.Treble, SightReadingPresetId.OneOctave, motion, seed: 77),
            MotionSequence(Staff.Treble, SightReadingPresetId.OneOctave, motion, seed: 77));
        Assert.NotEqual(
            MotionSequence(Staff.Treble, SightReadingPresetId.OneOctave, motion, seed: 77),
            MotionSequence(Staff.Treble, SightReadingPresetId.OneOctave, motion, seed: 78));
    }

    [Theory]
    [InlineData(SightReadingMotion.Melodic)]
    [InlineData(SightReadingMotion.Intervallic)]
    public void Compose_MotionOnKeyPresets_KeepsEveryNoteSpelledForItsKeySignature(SightReadingMotion motion)
    {
        Score gMajor = SightReadingExerciseComposer.Compose(
            MotionOptions(Staff.Treble, SightReadingPresetId.GMajor, motion, promptCount: 16),
            new Random(4));
        Score fMajor = SightReadingExerciseComposer.Compose(
            MotionOptions(Staff.Bass, SightReadingPresetId.FMajor, motion, promptCount: 16),
            new Random(4));

        Assert.Equal(1, gMajor.KeyFifths);
        Assert.All(PitchesOf(gMajor), pitch => Assert.Equal(pitch.Letter == NoteLetter.F ? 1 : 0, pitch.Alter));
        Assert.Equal(-1, fMajor.KeyFifths);
        Assert.All(PitchesOf(fMajor), pitch => Assert.Equal(pitch.Letter == NoteLetter.B ? -1 : 0, pitch.Alter));
    }

    [Theory]
    [InlineData(SightReadingMotion.Melodic)]
    [InlineData(SightReadingMotion.Intervallic)]
    public void Compose_MotionWithMasteryWeights_IgnoresThemBecauseWeightsOnlyShapeRandomPicks(SightReadingMotion motion)
    {
        var weights = new Dictionary<(Pitch, Staff), double>
        {
            [(new Pitch(NoteLetter.E, 0, 4), Staff.Treble)] = 5.0,
        };
        SightReadingExerciseOptions options = MotionOptions(Staff.Treble, SightReadingPresetId.OneOctave, motion, 16);

        Assert.Equal(
            PitchesOf(SightReadingExerciseComposer.Compose(options, new Random(9))),
            PitchesOf(SightReadingExerciseComposer.Compose(options, new Random(9), weights)));
    }

    [Theory]
    [InlineData(SightReadingPresetId.Chords)]
    public void Compose_MotionOnPresetsWithTheirOwnNoteChoice_IsIgnored(SightReadingPresetId preset)
    {
        Assert.False(SightReadingExerciseComposer.SupportsMotion(preset));
        Assert.Equal(
            PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, preset, SightReadingMotion.Random, 8),
                new Random(3))),
            PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, preset, SightReadingMotion.Melodic, 8),
                new Random(3))));
    }

    [Theory]
    [InlineData(SightReadingPresetId.FiveNote)]
    [InlineData(SightReadingPresetId.OneOctave)]
    [InlineData(SightReadingPresetId.GMajor)]
    [InlineData(SightReadingPresetId.FMajor)]
    [InlineData(SightReadingPresetId.LedgerLines)]
    public void SupportsMotion_PlainAndKeyRangesAndLedgerLines_IsTrue(SightReadingPresetId preset) =>
        Assert.True(SightReadingExerciseComposer.SupportsMotion(preset));

    [Theory]
    [InlineData(Staff.Treble, 4)]
    [InlineData(Staff.Treble, 8)]
    [InlineData(Staff.Treble, 16)]
    [InlineData(Staff.Bass, 4)]
    [InlineData(Staff.Bass, 8)]
    [InlineData(Staff.Bass, 16)]
    public void Compose_MelodicLedgerLines_ReachesBelowAndAboveTheStaffInTwoMelodicPhrases(Staff staff, int promptCount)
    {
        IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(staff, SightReadingPresetId.LedgerLines);
        (int staffBottom, int staffTop) = GetStaffLineIndexes(staff);

        for (int seed = 0; seed < 100; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(staff, SightReadingPresetId.LedgerLines, SightReadingMotion.Melodic, promptCount),
                new Random(seed)));

            Assert.Equal(promptCount, pitches.Length);
            Assert.All(pitches, pitch => Assert.Contains(pitch, palette));
            Assert.Contains(pitches, pitch => pitch.DiatonicIndex < staffBottom);
            Assert.Contains(pitches, pitch => pitch.DiatonicIndex > staffTop);
            // A melodic line moves by a step, a skip or a repeat; the one move between its two phrases may be larger.
            Assert.InRange(
                pitches.Zip(pitches.Skip(1)).Count(pair => Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex) > 2),
                0,
                1);
        }
    }

    [Theory]
    [InlineData(Staff.Treble, 1)]
    [InlineData(Staff.Treble, 4)]
    [InlineData(Staff.Bass, 2)]
    [InlineData(Staff.Bass, 3)]
    public void Compose_IntervallicLedgerLines_ReachesBelowAndAboveTheStaffWithOnlyThePhraseChangeBreakingTheInterval(
        Staff staff,
        int intervalSteps)
    {
        (int staffBottom, int staffTop) = GetStaffLineIndexes(staff);

        for (int seed = 0; seed < 100; seed++)
        {
            Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(
                    staff,
                    SightReadingPresetId.LedgerLines,
                    SightReadingMotion.Intervallic,
                    promptCount: 16,
                    intervalSteps),
                new Random(seed)));

            Assert.Contains(pitches, pitch => pitch.DiatonicIndex < staffBottom);
            Assert.Contains(pitches, pitch => pitch.DiatonicIndex > staffTop);
            Assert.InRange(
                pitches.Zip(pitches.Skip(1)).Count(pair =>
                    Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex) != intervalSteps),
                0,
                1);
        }
    }

    [Fact]
    public void Compose_MelodicLedgerLinesOnAGrandStaff_ReachesBelowAndAboveEachStaff()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.LedgerLines,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true,
            Motion: SightReadingMotion.Melodic);

        for (int seed = 0; seed < 50; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(options, new Random(seed));

            foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
            {
                (int staffBottom, int staffTop) = GetStaffLineIndexes(staff);
                int[] indexes = score.Measures
                    .SelectMany(measure => measure.Notes)
                    .Where(note => note.Staff == staff)
                    .Select(note => note.Pitch.DiatonicIndex)
                    .ToArray();
                Assert.Contains(indexes, index => index < staffBottom);
                Assert.Contains(indexes, index => index > staffTop);
            }
        }
    }

    [Fact]
    public void Compose_MelodicLedgerLines_StartsEitherPhraseFirst()
    {
        int staffBottom = GetStaffLineIndexes(Staff.Treble).Bottom;
        bool[] startsBelow = Enumerable.Range(0, 100)
            .Select(seed => PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(Staff.Treble, SightReadingPresetId.LedgerLines, SightReadingMotion.Melodic, promptCount: 8),
                new Random(seed)))[0].DiatonicIndex < staffBottom)
            .ToArray();

        Assert.Contains(true, startsBelow);
        Assert.Contains(false, startsBelow);
    }

    [Theory]
    [InlineData(SightReadingMotion.Melodic)]
    [InlineData(SightReadingMotion.Intervallic)]
    public void Compose_MotionOnGrandStaff_KeepsEachStaffInItsOwnPalette(SightReadingMotion motion)
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true,
            Motion: motion);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(12));

        foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
        {
            IReadOnlyList<Pitch> palette = SightReadingExerciseComposer.GetRangePitches(staff, SightReadingPresetId.FiveNote);
            Assert.All(
                score.Measures.SelectMany(measure => measure.Notes).Where(note => note.Staff == staff),
                note => Assert.Contains(note.Pitch, palette));
        }
    }

    [Fact]
    public void Compose_MelodicWithAVariableRhythm_GivesEveryNoteAMelodicPitch()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.OneOctave,
            PromptCount: 16,
            NoteReadingMode.PitchAndRhythm,
            RhythmPreset: SightReadingRhythmPreset.Basic,
            Motion: SightReadingMotion.Melodic);

        Pitch[] pitches = PitchesOf(SightReadingExerciseComposer.Compose(options, new Random(5)));

        Assert.True(pitches.Length >= 16);
        Assert.All(
            pitches.Zip(pitches.Skip(1)),
            pair => Assert.InRange(Math.Abs(pair.First.DiatonicIndex - pair.Second.DiatonicIndex), 0, 2));
    }

    private static SightReadingExerciseOptions HandsTogetherOptions(
        int promptCount = 8,
        SightReadingPresetId preset = SightReadingPresetId.FiveNote,
        bool isGrandStaff = true,
        NoteReadingMode mode = NoteReadingMode.PitchAndOrder) =>
        new(Staff.Treble, preset, promptCount, mode, IsGrandStaff: isGrandStaff, IsHandsTogether: true);

    [Fact]
    public void Options_IsHandsTogether_DefaultsToFalse()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 8,
            NoteReadingMode.PitchAndOrder,
            IsGrandStaff: true);

        Assert.False(options.IsHandsTogether);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void Compose_HandsTogether_EveryPromptIsOneTrebleNoteAndOneBassNoteAtTheSameOnset(int promptCount)
    {
        IReadOnlyList<Pitch> treblePalette = SightReadingExerciseComposer.GetRangePitches(Staff.Treble, SightReadingPresetId.FiveNote);
        IReadOnlyList<Pitch> bassPalette = SightReadingExerciseComposer.GetRangePitches(Staff.Bass, SightReadingPresetId.FiveNote);

        for (int seed = 0; seed < 50; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(HandsTogetherOptions(promptCount), new Random(seed));

            var prompts = score.Measures
                .SelectMany(measure => measure.Notes)
                .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
                .OrderBy(group => group.Key.MeasureIndex)
                .ThenBy(group => group.Key.BeatOffset)
                .ToArray();
            Assert.Equal(promptCount, prompts.Length);
            Assert.All(
                prompts,
                prompt =>
                {
                    ScoreNote[] notes = prompt.ToArray();
                    Assert.Equal(2, notes.Length);
                    Assert.Equal(Staff.Treble, Assert.Single(notes, note => note.Staff == Staff.Treble).Staff);
                    Assert.Contains(Assert.Single(notes, note => note.Staff == Staff.Treble).Pitch, treblePalette);
                    Assert.Contains(Assert.Single(notes, note => note.Staff == Staff.Bass).Pitch, bassPalette);
                });
            Assert.All(score.Measures, measure => Assert.Equal(8, measure.Notes.Count));
        }
    }

    [Fact]
    public void Compose_HandsTogether_EachHandFollowsTheFiveNoteRules()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            Score score = SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(seed));

            foreach (Staff staff in new[] { Staff.Treble, Staff.Bass })
            {
                int[] indexes = score.Measures
                    .SelectMany(measure => measure.Notes)
                    .Where(note => note.Staff == staff)
                    .OrderBy(note => note.MeasureIndex)
                    .ThenBy(note => note.BeatOffset)
                    .Select(note => note.Pitch.DiatonicIndex)
                    .ToArray();
                Assert.All(
                    indexes.Zip(indexes.Skip(1)),
                    pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 1, 2));
            }
        }
    }

    [Fact]
    public void Compose_HandsTogether_SameSeedIsDeterministic()
    {
        Assert.Equal(
            PitchesOf(SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(14))),
            PitchesOf(SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(14))));
        Assert.NotEqual(
            PitchesOf(SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(14))),
            PitchesOf(SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(15))));
    }

    [Fact]
    public void Compose_HandsTogetherWithoutAGrandStaff_Throws()
    {
        Assert.Throws<ArgumentException>(() => SightReadingExerciseComposer.Compose(
            HandsTogetherOptions(isGrandStaff: false),
            new Random(1)));
    }

    [Theory]
    [InlineData(SightReadingPresetId.OneOctave)]
    [InlineData(SightReadingPresetId.LedgerLines)]
    [InlineData(SightReadingPresetId.Chords)]
    [InlineData(SightReadingPresetId.GMajor)]
    [InlineData(SightReadingPresetId.Accidentals)]
    public void Compose_HandsTogetherWithARangeOtherThanFiveNotes_Throws(SightReadingPresetId preset)
    {
        Assert.Throws<ArgumentException>(() => SightReadingExerciseComposer.Compose(
            HandsTogetherOptions(preset: preset),
            new Random(1)));
    }

    [Fact]
    public void Compose_HandsTogether_FingeringGeneratorGivesEveryNoteAValidFinger()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            Score generated = ScoreFingeringGenerator.Generate(
                SightReadingExerciseComposer.Compose(HandsTogetherOptions(16), new Random(seed)));

            Assert.All(
                generated.Measures.SelectMany(measure => measure.Notes),
                note => Assert.InRange(note.Fingering?.Number ?? 0, 1, 5));
        }
    }

    [Fact]
    public void Compose_HandsTogether_APitchOnlySessionNeedsBothHandsOnEachPrompt()
    {
        Score score = SightReadingExerciseComposer.Compose(HandsTogetherOptions(8), new Random(3));
        var session = new NoteReadingSession();
        session.Reset(score);
        ScoreNote[][] prompts = score.Measures
            .SelectMany(measure => measure.Notes)
            .GroupBy(note => (note.MeasureIndex, note.BeatOffset))
            .OrderBy(group => group.Key.MeasureIndex)
            .ThenBy(group => group.Key.BeatOffset)
            .Select(group => group.ToArray())
            .ToArray();

        foreach (ScoreNote[] prompt in prompts)
        {
            NoteReadingSession.CheckResult first = session.Check(prompt[1].Pitch);
            Assert.True(first.IsCorrect);
            Assert.False(first.DidAdvance);
            Assert.True(session.Check(prompt[0].Pitch).DidAdvance);
        }

        Assert.True(session.IsComplete);
        Assert.Equal(8, session.FirstTryCorrectCount);
    }

    [Fact]
    public void Compose_HandsTogether_AnOnsetGradedSessionTakesTheLaterOfTheTwoHandsAsTheOnsetDeviation()
    {
        Score score = SightReadingExerciseComposer.Compose(
            HandsTogetherOptions(8, mode: NoteReadingMode.PitchAndRhythm),
            new Random(3));
        var session = new NoteReadingSession();
        session.Reset(score, NoteReadingMode.PitchAndRhythm, TimeSpan.FromMilliseconds(60), explicitRhythmAnchor: TimeSpan.Zero);
        ScoreNote[] firstPrompt = score.Measures[0].Notes.Where(note => note.BeatOffset == 0).ToArray();

        // The treble hand lands on time, the bass hand 120 ms late (tolerance 60 ms): the prompt is graded late.
        session.Check(firstPrompt.Single(note => note.Staff == Staff.Treble).Pitch, TimeSpan.Zero);
        session.Check(firstPrompt.Single(note => note.Staff == Staff.Bass).Pitch, TimeSpan.FromMilliseconds(120));

        NoteReadingPromptResult result = session.PromptResults[0];
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(120), result.OnsetDeviation);
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.False(result.IsFirstTryCorrect);
    }

    private static SightReadingExerciseOptions MotionOptions(
        Staff staff,
        SightReadingPresetId preset,
        SightReadingMotion motion,
        int promptCount,
        int intervalSteps = SightReadingExerciseOptions.DefaultIntervalSteps) =>
        new(
            staff,
            preset,
            promptCount,
            NoteReadingMode.PitchAndOrder,
            Motion: motion,
            IntervalSteps: intervalSteps);

    private static string MotionSequence(Staff staff, SightReadingPresetId preset, SightReadingMotion motion, int seed) =>
        string.Join(
            ",",
            PitchesOf(SightReadingExerciseComposer.Compose(
                MotionOptions(staff, preset, motion, promptCount: 16),
                new Random(seed))));

    /// <summary>
    /// The renderer prints an accidental only against the key signature and never a cautionary natural, so within a
    /// measure and staff a letter that was altered must stay altered the same way: a plain note of that letter would
    /// read as the altered one.
    /// </summary>
    private static void AssertNoAmbiguousAccidentals(Score score)
    {
        foreach (ScoreMeasure measure in score.Measures)
        {
            var alteredLetters = new Dictionary<(Staff, NoteLetter), int>();
            foreach (ScoreNote note in measure.Notes.OrderBy(candidate => candidate.BeatOffset))
            {
                (Staff, NoteLetter) key = (note.Staff, note.Pitch.Letter);
                if (alteredLetters.TryGetValue(key, out int alter))
                {
                    Assert.Equal(alter, note.Pitch.Alter);
                }
                else if (note.Pitch.Alter != 0)
                {
                    alteredLetters[key] = note.Pitch.Alter;
                }
            }
        }
    }

    private static (int Bottom, int Top) GetStaffLineIndexes(Staff staff) =>
        staff == Staff.Treble
            ? (new Pitch(NoteLetter.E, 0, 4).DiatonicIndex, new Pitch(NoteLetter.F, 0, 5).DiatonicIndex)
            : (new Pitch(NoteLetter.G, 0, 2).DiatonicIndex, new Pitch(NoteLetter.A, 0, 3).DiatonicIndex);

    /// <summary>Ledger lines a notehead needs: the staff's lines run from its bottom to top line, and every second step beyond them is one more.</summary>
    private static int CountLedgerLines(Pitch pitch, Staff staff)
    {
        (Pitch bottomLine, Pitch topLine) = staff == Staff.Treble
            ? (new Pitch(NoteLetter.E, 0, 4), new Pitch(NoteLetter.F, 0, 5))
            : (new Pitch(NoteLetter.G, 0, 2), new Pitch(NoteLetter.A, 0, 3));
        return pitch.DiatonicIndex < bottomLine.DiatonicIndex
            ? (bottomLine.DiatonicIndex - pitch.DiatonicIndex) / 2
            : pitch.DiatonicIndex > topLine.DiatonicIndex
                ? (pitch.DiatonicIndex - topLine.DiatonicIndex) / 2
                : 0;
    }

    private static void AssertEachMeasureHasExactBeatTotal(Score score, int expectedBeatsPerMeasure)
    {
        foreach (ScoreMeasure measure in score.Measures)
        {
            double noteBeats = measure.Notes.Sum(note => MusicalTime.GetBeats(note.NoteValue, score.TimeSignature));
            double restBeats = measure.Rests.Sum(rest => MusicalTime.GetBeats(rest.NoteValue, score.TimeSignature));
            Assert.Equal(expectedBeatsPerMeasure, noteBeats + restBeats, precision: 9);
        }
    }

    /// <summary>
    /// Every beamed eighth belongs to a Begin, Continue*, End group. With <paramref name="allowLoneEighths"/> an eighth
    /// may also stand alone with no beam (it is then drawn with a flag), as the 6/8 patterns that pair an eighth with a
    /// quarter need; by default every eighth must be beamed.
    /// </summary>
    private static void AssertBeamedEighthNotesFormValidGroups(Score score, bool allowLoneEighths = false)
    {
        ScoreNote[] eighthNotes = score.Measures
            .SelectMany(measure => measure.Notes)
            .Where(note => note.NoteValue == new NoteValue(8))
            .OrderBy(note => note.MeasureIndex)
            .ThenBy(note => note.BeatOffset)
            .ToArray();
        if (allowLoneEighths)
        {
            eighthNotes = eighthNotes.Where(note => note.BeamState != BeamState.None).ToArray();
        }

        Assert.All(eighthNotes, note => Assert.NotEqual(BeamState.None, note.BeamState));

        int index = 0;
        while (index < eighthNotes.Length)
        {
            Assert.Equal(BeamState.Begin, eighthNotes[index].BeamState);
            int groupEnd = index;
            while (eighthNotes[groupEnd].BeamState != BeamState.End)
            {
                groupEnd++;
                Assert.True(groupEnd < eighthNotes.Length, "Beam group never reaches an End state.");
            }

            for (int middleIndex = index + 1; middleIndex < groupEnd; middleIndex++)
            {
                Assert.Equal(BeamState.Continue, eighthNotes[middleIndex].BeamState);
            }

            index = groupEnd + 1;
        }
    }

    private static void AssertMaxDiatonicLeap(Score score, int maxLeap)
    {
        int[] diatonicIndexes = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch.DiatonicIndex)
            .ToArray();
        Assert.All(
            diatonicIndexes.Zip(diatonicIndexes.Skip(1)),
            pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 1, maxLeap));
    }

    private static Score CreateOriginalScore() =>
        SightReadingExerciseComposer.Compose(
            new SightReadingExerciseOptions(
                Staff.Treble,
                SightReadingPresetId.FiveNote,
                PromptCount: 8,
                NoteReadingMode.PitchAndOrder),
            new Random(1));

    private static ScoreNote CreateSourceNote(NoteLetter letter, Staff staff) =>
        new(new Pitch(letter, 0, 4), new NoteValue(4), MeasureIndex: 3, BeatOffset: 2, staff, TiesToNext: true);
}
