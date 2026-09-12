using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class IdlePracticeNoteCheckerTests
{
    [Fact]
    public void Check_CorrectPitch_MarksNoteCorrectAndAdvancesExpectedNote()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var checker = new IdlePracticeNoteChecker();
        checker.Reset(CreateScore([firstNote, secondNote]));

        IdlePracticeNoteChecker.Result result = checker.Check(firstNote.Pitch);

        Assert.True(result.IsCorrect);
        Assert.True(result.DidAdvance);
        Assert.False(result.IsComplete);
        Assert.Equal(Verdict.Correct, checker.Verdicts[firstNote]);
        Assert.Equal(1, checker.CurrentOnsetBeats);
        Assert.Equal(secondNote, Assert.Single(checker.ExpectedNotes));
    }

    [Fact]
    public void Check_WrongPitch_MarksExpectedNoteWrongWithoutAdvancing()
    {
        var expectedNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var checker = new IdlePracticeNoteChecker();
        checker.Reset(CreateScore([expectedNote]));

        IdlePracticeNoteChecker.Result wrongResult = checker.Check(
            new Pitch(NoteLetter.D, 0, 4));

        Assert.False(wrongResult.IsCorrect);
        Assert.False(wrongResult.DidAdvance);
        Assert.False(wrongResult.IsComplete);
        Assert.Equal(0, checker.CurrentOnsetBeats);
        Assert.Equal(Verdict.WrongPitch, checker.Verdicts[expectedNote]);

        IdlePracticeNoteChecker.Result correctResult = checker.Check(expectedNote.Pitch);

        Assert.True(correctResult.IsCorrect);
        Assert.True(correctResult.IsComplete);
        Assert.Equal(Verdict.Correct, checker.Verdicts[expectedNote]);
    }

    [Fact]
    public void Check_CorrectChordPitchesInAnyOrder_AdvancesAfterChordIsComplete()
    {
        var lowerChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var nextNote = CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 1);
        var checker = new IdlePracticeNoteChecker();
        checker.Reset(CreateScore([lowerChordNote, upperChordNote, nextNote]));

        IdlePracticeNoteChecker.Result firstResult = checker.Check(upperChordNote.Pitch);

        Assert.True(firstResult.IsCorrect);
        Assert.False(firstResult.DidAdvance);
        Assert.Equal(0, checker.CurrentOnsetBeats);
        Assert.Equal(Verdict.Correct, checker.Verdicts[upperChordNote]);
        Assert.Equal(lowerChordNote, Assert.Single(checker.ExpectedNotes));

        IdlePracticeNoteChecker.Result secondResult = checker.Check(lowerChordNote.Pitch);

        Assert.True(secondResult.IsCorrect);
        Assert.True(secondResult.DidAdvance);
        Assert.Equal(1, checker.CurrentOnsetBeats);
        Assert.Equal(nextNote, Assert.Single(checker.ExpectedNotes));
    }

    [Fact]
    public void Check_FinalCorrectPitch_CompletesSequence()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var checker = new IdlePracticeNoteChecker();
        checker.Reset(CreateScore([note]));

        IdlePracticeNoteChecker.Result result = checker.Check(note.Pitch);

        Assert.True(result.IsCorrect);
        Assert.True(result.DidAdvance);
        Assert.True(result.IsComplete);
        Assert.Null(checker.CurrentOnsetBeats);
        Assert.Empty(checker.ExpectedNotes);
    }

    private static ScoreNote CreateNote(NoteLetter letter, int measureIndex, double beatOffset) =>
        new(
            new Pitch(letter, 0, 4),
            new NoteValue(4),
            measureIndex,
            beatOffset,
            Staff.Treble);

    private static Score CreateScore(IReadOnlyList<ScoreNote> notes) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);
}
