using Microsoft.Extensions.Time.Testing;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class NoteReadingSessionTests
{
    [Fact]
    public void Check_CorrectPitch_MarksNoteCorrectAndAdvancesExpectedNote()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession(new FakeTimeProvider());
        session.Reset(CreateScore([firstNote, secondNote]));

        NoteReadingSession.CheckResult result = session.Check(firstNote.Pitch);

        Assert.True(result.IsCorrect);
        Assert.True(result.DidAdvance);
        Assert.False(result.IsComplete);
        Assert.Equal(Verdict.Correct, session.Verdicts[firstNote]);
        Assert.Equal(1, session.CurrentOnsetBeats);
        Assert.Equal(secondNote, Assert.Single(session.ExpectedNotes));
        Assert.Equal(1, session.CompletedPromptCount);
        Assert.Equal(1, session.FirstTryCorrectCount);
        Assert.Equal(100, session.FirstTryAccuracyPercent);
    }

    [Fact]
    public void Check_WrongThenCorrectPitch_RecordsMistakeAndCompletesPrompt()
    {
        var expectedNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var timeProvider = new FakeTimeProvider();
        var session = new NoteReadingSession(timeProvider);
        session.Reset(CreateScore([expectedNote]));
        timeProvider.Advance(TimeSpan.FromSeconds(3));

        NoteReadingSession.CheckResult wrongResult = session.Check(new Pitch(NoteLetter.D, 0, 4));

        Assert.False(wrongResult.IsCorrect);
        Assert.False(wrongResult.DidAdvance);
        Assert.False(wrongResult.IsComplete);
        Assert.Equal(0, session.CurrentOnsetBeats);
        Assert.DoesNotContain(expectedNote, session.Verdicts.Keys);
        Assert.Equal(expectedNote, Assert.Single(session.ExpectedNotes));
        Assert.Equal(1, session.WrongAttemptCount);

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        NoteReadingSession.CheckResult correctResult = session.Check(expectedNote.Pitch);

        Assert.True(correctResult.IsCorrect);
        Assert.True(correctResult.IsComplete);
        Assert.Equal(Verdict.Correct, session.Verdicts[expectedNote]);
        Assert.Equal(1, session.CompletedPromptCount);
        Assert.Equal(0, session.FirstTryCorrectCount);
        Assert.Equal(0, session.FirstTryAccuracyPercent);
        Assert.Equal(TimeSpan.FromSeconds(5), session.ElapsedTime);
    }

    [Fact]
    public void Check_EnharmonicPitch_MarksExpectedNoteCorrect()
    {
        var expectedNote = new ScoreNote(
            new Pitch(NoteLetter.D, -1, 4),
            new NoteValue(4),
            MeasureIndex: 0,
            BeatOffset: 0,
            Staff.Treble);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([expectedNote]));

        NoteReadingSession.CheckResult result = session.Check(new Pitch(NoteLetter.C, 1, 4));

        Assert.True(result.IsCorrect);
        Assert.True(result.IsComplete);
        Assert.Equal(Verdict.Correct, session.Verdicts[expectedNote]);
    }

    [Fact]
    public void Check_CorrectChordPitchesInAnyOrder_AdvancesAfterChordIsComplete()
    {
        var lowerChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var nextNote = CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([lowerChordNote, upperChordNote, nextNote]));

        NoteReadingSession.CheckResult firstResult = session.Check(upperChordNote.Pitch);

        Assert.True(firstResult.IsCorrect);
        Assert.False(firstResult.DidAdvance);
        Assert.Equal(0, session.CurrentOnsetBeats);
        Assert.Equal(Verdict.Correct, session.Verdicts[upperChordNote]);
        Assert.Equal(lowerChordNote, Assert.Single(session.ExpectedNotes));

        NoteReadingSession.CheckResult secondResult = session.Check(lowerChordNote.Pitch);

        Assert.True(secondResult.IsCorrect);
        Assert.True(secondResult.DidAdvance);
        Assert.Equal(1, session.CurrentOnsetBeats);
        Assert.Equal(nextNote, Assert.Single(session.ExpectedNotes));
    }

    [Fact]
    public void Check_FinalCorrectPitch_CompletesSequence()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([note]));

        NoteReadingSession.CheckResult result = session.Check(note.Pitch);

        Assert.True(result.IsCorrect);
        Assert.True(result.DidAdvance);
        Assert.True(result.IsComplete);
        Assert.True(session.IsComplete);
        Assert.Null(session.CurrentOnsetBeats);
        Assert.Empty(session.ExpectedNotes);
    }

    [Fact]
    public void Reset_CompletedSequence_ClearsProgressAndRestoresFirstExpectedNote()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        Score score = CreateScore([note]);
        var session = new NoteReadingSession(new FakeTimeProvider());
        session.Reset(score);
        session.Check(new Pitch(NoteLetter.D, 0, 4));
        session.Check(note.Pitch);

        session.Reset(score);

        Assert.Empty(session.Verdicts);
        Assert.Equal(0, session.CurrentOnsetBeats);
        Assert.Equal(note, Assert.Single(session.ExpectedNotes));
        Assert.Equal(1, session.PromptCount);
        Assert.Equal(0, session.CompletedPromptCount);
        Assert.Equal(0, session.FirstTryCorrectCount);
        Assert.Equal(0, session.WrongAttemptCount);
        Assert.False(session.IsComplete);
        Assert.Equal(TimeSpan.Zero, session.ElapsedTime);
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
