using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringSustainedNoteTests
{
    private static readonly NoteValue Half = new(2);
    private static readonly NoteValue Whole = new(1);

    [Fact]
    public void Generate_NoteSustainedUnderShorterNotes_KeepsItsFingerOccupiedAndOrdered()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0, noteValue: Half),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
        ]);

        int[] fingers = FingeringNumbers(ScoreFingeringGenerator.Generate(score));

        int sustainedFinger = fingers[0];
        Assert.True(fingers[1] < fingers[2], "C4 and D4 must stay in pitch order");
        Assert.True(fingers[2] < sustainedFinger, "D4 must be played by a finger below the held G4");
    }

    [Fact]
    public void Generate_ChordWithUnequalDurations_ReleasedFingerIsFreeForTheNextAttack()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: Half),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 1),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 3),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 2), 3),
            ]);

        int[] fingers = FingeringNumbers(ScoreFingeringGenerator.Generate(score, options));

        Assert.Equal(3, fingers[1]);
        Assert.Equal(3, fingers[2]);
        Assert.True(fingers[0] < 3, "the held C4 must stay below the finger that played E4 and G4");
    }

    [Fact]
    public void Generate_HeldNoteAcrossBarline_ConstrainsAttacksInTheNextMeasure()
    {
        Score score = CreateScore(
            4,
            120,
            [CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 2, noteValue: Whole)],
            [
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0, measureIndex: 1),
                CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 1, measureIndex: 1),
            ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        int[] fingers = FingeringNumbers(generated);
        Assert.True(fingers[0] < fingers[1], "E4 is struck while the C4 from the previous measure is held");
        Assert.True(fingers[0] < fingers[2], "G4 is struck while the C4 from the previous measure is held");
        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_HeldNoteBeyondReachInALaterMeasure_ReportsTheLaterNote()
    {
        Score score = CreateScore(
            4,
            120,
            [CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 2, noteValue: Whole)],
            [
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0, measureIndex: 1),
                CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 1, measureIndex: 1),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, new ScoreFingeringGenerationOptions(CreateProfile(3))));

        Assert.Equal(new ScoreFingeringNoteAddress(1, 1), exception.Address);
        Assert.Contains("reach", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_TieAcrossBarline_UsesOneFingerAndKeepsOtherKeysOrderedAroundIt()
    {
        Score score = CreateScore(
            4,
            120,
            [
                CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 2, noteValue: Half, tiesToNext: true),
                CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 3),
            ],
            [
                CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: Half, measureIndex: 1),
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0, measureIndex: 1),
            ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        int[] fingers = FingeringNumbers(generated);
        Assert.Equal(fingers[0], fingers[2]);
        Assert.True(fingers[0] < fingers[1], "G4 is struck while the tied C4 is held");
        Assert.True(fingers[0] < fingers[3], "E4 is struck while the tied C4 is held");
        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_UnrelatedSamePitchNotes_AreNotMergedIntoOneTieChain()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, tiesToNext: true),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 1),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 2),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 2),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 2), 4),
            ]);

        Assert.Equal([2, 2, 4], FingeringNumbers(ScoreFingeringGenerator.Generate(score, options)));
    }

    [Fact]
    public void Generate_DoubledUnisonsOfDifferentLengths_ShareOneFingerWhileEitherSounds()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: Half),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 1),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        int[] fingers = FingeringNumbers(generated);
        Assert.Equal(fingers[0], fingers[1]);
        Assert.True(fingers[0] < fingers[2], "E4 is struck while the longer C4 is still held");
        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_SameKeyAttackedWhileHeld_RestrikesItWithTheHoldingFinger()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: Half),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 1),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 1),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        int[] fingers = FingeringNumbers(generated);
        Assert.Equal(fingers[0], fingers[1]);
        Assert.True(fingers[1] < fingers[2]);
        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_ReleasedLeapBeyondAnyStaticReach_RemainsPossible()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 3, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 1),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_SustainedNotesPlusAnAttackNeedSixKeys_ReportsTheAttack()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: Whole),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 0, noteValue: Whole),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0, noteValue: Whole),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 0, noteValue: Whole),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0, noteValue: Whole),
            CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 1),
        ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score));

        Assert.Equal(new ScoreFingeringNoteAddress(0, 5), exception.Address);
        Assert.Contains("more than five", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
