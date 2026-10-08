using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringLockTests
{
    [Fact]
    public void Generate_InteriorAnchor_ChangesTheSurroundingChoices()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 2),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 3),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 4),
        ]);
        int[] unlocked = FingeringNumbers(ScoreFingeringGenerator.Generate(score));
        int anchoredFinger = unlocked[2] == 5 ? 4 : 5;
        var options = new ScoreFingeringGenerationOptions(
            locks: [new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 2), anchoredFinger)]);

        Score generated = ScoreFingeringGenerator.Generate(score, options);

        int[] anchored = FingeringNumbers(generated);
        Assert.Equal(anchoredFinger, anchored[2]);
        Assert.NotEqual(unlocked, anchored);
        Assert.Empty(FingeringConstraintValidator.Validate(generated, ScoreFingeringProfile.Default));
    }

    [Fact]
    public void Generate_ChordNotesLockedToTheSameFinger_ReportsTheSecondNote()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 2),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 2),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Equal(new ScoreFingeringNoteAddress(0, 1), exception.Address);
    }

    [Fact]
    public void Generate_ChordLocksThatCrossFingers_ReportsTheCrossing()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 3),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 2),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Contains("cross", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(0, 9)]
    public void Generate_LockOutsideTheScore_ReportsTheAddress(int measureIndex, int noteIndex)
    {
        Score score = CreateScore([CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0)]);
        var address = new ScoreFingeringNoteAddress(measureIndex, noteIndex);
        var options = new ScoreFingeringGenerationOptions(locks: [new ScoreFingeringLock(address, 1)]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Equal(address, exception.Address);
    }

    [Fact]
    public void Generate_UnisonNotesLockedToDifferentFingers_ReportsTheContradiction()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 1),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 2),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Equal(new ScoreFingeringNoteAddress(0, 1), exception.Address);
    }

    [Fact]
    public void Generate_LockOnTheSecondTiedNote_PinsTheWholeChain()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, tiesToNext: true),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 1),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks: [new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 4)]);

        Assert.Equal([4, 4], FingeringNumbers(ScoreFingeringGenerator.Generate(score, options)));
    }

    [Fact]
    public void Generate_RepeatedIdenticalLock_IsHonouredOnce()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
        ]);
        var address = new ScoreFingeringNoteAddress(0, 0);
        var options = new ScoreFingeringGenerationOptions(
            locks: [new ScoreFingeringLock(address, 3), new ScoreFingeringLock(address, 3)]);

        Assert.Equal(3, FingeringNumbers(ScoreFingeringGenerator.Generate(score, options))[0]);
    }

    [Fact]
    public void Generate_LocksInBothHands_AreHonouredIndependently()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.C, 3, Staff.Bass, beatOffset: 0),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 3),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 2),
            ]);

        Assert.Equal([3, 2], FingeringNumbers(ScoreFingeringGenerator.Generate(score, options)));
    }
}
