using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringEditorTests
{
    [Fact]
    public void SetFingering_ChordMemberOrdinal_UpdatesOnlyTargetAndPreservesPlacement()
    {
        var first = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(1));
        var target = new ScoreNote(
            new Pitch(NoteLetter.E, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(2, ScoreFingeringPlacement.Above));
        var score = CreateScore([first, target]);

        Score updated = ScoreFingeringEditor.SetFingering(score, noteOrdinal: 1, fingerNumber: 4);

        Assert.NotSame(score, updated);
        Assert.Same(first, updated.Measures[0].Notes[0]);
        Assert.Equal(new ScoreFingering(4, ScoreFingeringPlacement.Above), updated.Measures[0].Notes[1].Fingering);
        Assert.Equal(new ScoreFingering(2, ScoreFingeringPlacement.Above), score.Measures[0].Notes[1].Fingering);
    }

    [Fact]
    public void SetFingering_NoteWithoutFingering_AssignsNumberWithoutPlacement()
    {
        var score = CreateScore(
            [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)]);

        Score updated = ScoreFingeringEditor.SetFingering(score, noteOrdinal: 0, fingerNumber: 3);

        Assert.Equal(new ScoreFingering(3), updated.Measures[0].Notes[0].Fingering);
    }

    [Fact]
    public void SetFingering_NullNumber_RemovesFingering()
    {
        var score = CreateScore(
            [
                new ScoreNote(
                    new Pitch(NoteLetter.C, 0, 4),
                    new NoteValue(4),
                    0,
                    0,
                    Staff.Treble,
                    Fingering: new ScoreFingering(3)),
            ]);

        Score updated = ScoreFingeringEditor.SetFingering(score, noteOrdinal: 0, fingerNumber: null);

        Assert.Null(updated.Measures[0].Notes[0].Fingering);
        Assert.Equal(new ScoreFingering(3), score.Measures[0].Notes[0].Fingering);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void SetFingering_InvalidFingerNumber_Throws(int fingerNumber)
    {
        var score = CreateScore(
            [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScoreFingeringEditor.SetFingering(score, noteOrdinal: 0, fingerNumber));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SetFingering_InvalidNoteOrdinal_Throws(int noteOrdinal)
    {
        var score = CreateScore(
            [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScoreFingeringEditor.SetFingering(score, noteOrdinal, fingerNumber: 2));
    }

    [Theory]
    [InlineData(Staff.Treble)]
    [InlineData(Staff.Bass)]
    public void SetHand_ChordMemberOrdinal_UpdatesOnlyTargetAndPreservesFingering(Staff hand)
    {
        Staff initialHand = hand == Staff.Treble ? Staff.Bass : Staff.Treble;
        var target = new ScoreNote(
            new Pitch(NoteLetter.C, 0, 4),
            new NoteValue(4),
            0,
            0,
            initialHand,
            Fingering: new ScoreFingering(3, ScoreFingeringPlacement.Above));
        var untouched = new ScoreNote(
            new Pitch(NoteLetter.E, 0, 4),
            new NoteValue(4),
            0,
            0,
            Staff.Treble,
            Fingering: new ScoreFingering(1));
        var score = CreateScore([target, untouched]);

        Score updated = ScoreFingeringEditor.SetHand(score, noteOrdinal: 0, hand);

        Assert.Equal(hand, updated.Measures[0].Notes[0].Staff);
        Assert.Equal(target.Fingering, updated.Measures[0].Notes[0].Fingering);
        Assert.Same(untouched, updated.Measures[0].Notes[1]);
        Assert.Equal(initialHand, score.Measures[0].Notes[0].Staff);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void SetHand_InvalidHand_Throws(int handValue)
    {
        var score = CreateScore(
            [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScoreFingeringEditor.SetHand(score, noteOrdinal: 0, (Staff)handValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SetHand_InvalidNoteOrdinal_Throws(int noteOrdinal)
    {
        var score = CreateScore(
            [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScoreFingeringEditor.SetHand(score, noteOrdinal, Staff.Bass));
    }

    private static Score CreateScore(IReadOnlyList<ScoreNote> notes) =>
        new(
            "Fingerings",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);
}
