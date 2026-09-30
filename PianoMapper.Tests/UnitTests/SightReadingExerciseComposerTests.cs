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
        Assert.Equal(new Tempo(120), score.Tempo);
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
        var weights = new Dictionary<Pitch, double> { [new Pitch(NoteLetter.G, 0, 4)] = 10.0 };

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
        var weights = new Dictionary<Pitch, double> { [weakPitch] = 5.0 };

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
        var weights = new Dictionary<Pitch, double> { [new Pitch(NoteLetter.C, 0, 5)] = 8.0 };

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
        var weights = new Dictionary<Pitch, double> { [new Pitch(NoteLetter.F, 1, 5)] = 6.0 };

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
        Score empty = SightReadingExerciseComposer.Compose(options, new Random(29), new Dictionary<Pitch, double>());

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
    public void Compose_Chords_UsesIivAndVTriadsOfCMajor()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.Chords,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder);
        var expectedTriads = new[]
        {
            new HashSet<Pitch> { new(NoteLetter.C, 0, 4), new(NoteLetter.E, 0, 4), new(NoteLetter.G, 0, 4) },
            new HashSet<Pitch> { new(NoteLetter.F, 0, 4), new(NoteLetter.A, 0, 4), new(NoteLetter.C, 0, 5) },
            new HashSet<Pitch> { new(NoteLetter.G, 0, 4), new(NoteLetter.B, 0, 4), new(NoteLetter.D, 0, 5) },
        };

        Score score = SightReadingExerciseComposer.Compose(options, new Random(6));

        foreach (var chordNotes in score.Measures.SelectMany(measure => measure.Notes).GroupBy(
            note => (note.MeasureIndex, note.BeatOffset)))
        {
            var playedTriad = chordNotes.Select(note => note.Pitch).ToHashSet();
            Assert.Contains(expectedTriads, expectedTriad => expectedTriad.SetEquals(playedTriad));
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
    public void Compose_CompoundRhythm_OnlyUsesDottedQuarterAndEighthNoteValues()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingPresetId.FiveNote,
            PromptCount: 16,
            NoteReadingMode.PitchAndOrder,
            RhythmPreset: SightReadingRhythmPreset.Compound);
        NoteValue[] allowedValues = [new NoteValue(4, dots: 1), new NoteValue(8)];

        Score score = SightReadingExerciseComposer.Compose(options, new Random(5));

        Assert.All(
            score.Measures.SelectMany(measure => measure.Notes),
            note => Assert.Contains(note.NoteValue, allowedValues));
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

        AssertBeamedEighthNotesFormValidGroups(score);
    }

    [Theory]
    [InlineData(SightReadingRhythmPreset.Basic)]
    [InlineData(SightReadingRhythmPreset.Compound)]
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

    private static void AssertEachMeasureHasExactBeatTotal(Score score, int expectedBeatsPerMeasure)
    {
        foreach (ScoreMeasure measure in score.Measures)
        {
            double noteBeats = measure.Notes.Sum(note => MusicalTime.GetBeats(note.NoteValue, score.TimeSignature));
            double restBeats = measure.Rests.Sum(rest => MusicalTime.GetBeats(rest.NoteValue, score.TimeSignature));
            Assert.Equal(expectedBeatsPerMeasure, noteBeats + restBeats, precision: 9);
        }
    }

    private static void AssertBeamedEighthNotesFormValidGroups(Score score)
    {
        ScoreNote[] eighthNotes = score.Measures
            .SelectMany(measure => measure.Notes)
            .Where(note => note.NoteValue == new NoteValue(8))
            .OrderBy(note => note.MeasureIndex)
            .ThenBy(note => note.BeatOffset)
            .ToArray();
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
