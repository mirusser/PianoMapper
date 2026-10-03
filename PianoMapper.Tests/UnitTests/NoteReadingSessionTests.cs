using System.Collections.Immutable;
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
        Assert.Equal(Verdict.WrongPitch, wrongResult.Verdict);
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
        Assert.Equal(TimeSpan.FromSeconds(2), session.ElapsedTime);
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
    public void Release_CrossStaffChordBeforeCompletion_RequiresReleasedPitchAgain()
    {
        var trebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0, Staff.Treble);
        var bassNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0, Staff.Bass);
        var nextNote = CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([trebleNote, bassNote, nextNote]));

        session.Check(trebleNote.Pitch);
        session.Release(trebleNote.Pitch);
        NoteReadingSession.CheckResult bassResult = session.Check(bassNote.Pitch);

        Assert.True(bassResult.IsCorrect);
        Assert.False(bassResult.DidAdvance);
        Assert.Equal(0, session.CurrentOnsetBeats);
        Assert.Equal(trebleNote, Assert.Single(session.ExpectedNotes));
        Assert.DoesNotContain(trebleNote, session.Verdicts.Keys);

        NoteReadingSession.CheckResult trebleResult = session.Check(trebleNote.Pitch);

        Assert.True(trebleResult.DidAdvance);
        Assert.Equal(1, session.CurrentOnsetBeats);
        Assert.Equal(nextNote, Assert.Single(session.ExpectedNotes));
    }

    [Fact]
    public void ReleaseAll_PartialChord_RestoresEveryExpectedNote()
    {
        var trebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0, Staff.Treble);
        var bassNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0, Staff.Bass);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([trebleNote, bassNote]));
        session.Check(trebleNote.Pitch);

        session.ReleaseAll();

        Assert.Empty(session.Verdicts);
        Assert.Equal(2, session.ExpectedNotes.Count);
        Assert.Contains(trebleNote, session.ExpectedNotes);
        Assert.Contains(bassNote, session.ExpectedNotes);
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

    [Fact]
    public void Release_PitchAndOrderAfterPromptAdvance_KeepsCorrectVerdict()
    {
        var bassNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            Staff.Bass,
            new NoteValue(2));
        var trebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var nextNote = CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([bassNote, trebleNote, nextNote]));

        session.Check(bassNote.Pitch);
        session.Check(trebleNote.Pitch);
        session.Release(bassNote.Pitch);

        Assert.Equal(Verdict.Correct, session.Verdicts[bassNote]);
        Assert.Equal(1, session.CurrentOnsetBeats);
        Assert.Equal(nextNote, Assert.Single(session.ExpectedNotes));
    }

    [Fact]
    public void Release_PitchAndHoldLongBassNoteTooSoon_MarksTooShortAfterTrebleAdvances()
    {
        var bassNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            Staff.Bass,
            new NoteValue(2));
        var firstTrebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var secondTrebleNote = CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([bassNote, firstTrebleNote, secondTrebleNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(bassNote.Pitch, TimeSpan.Zero);
        session.Check(firstTrebleNote.Pitch, TimeSpan.Zero);
        session.Check(secondTrebleNote.Pitch, TimeSpan.FromMilliseconds(300));
        NoteReadingSession.ReleaseResult result = session.Release(
            bassNote.Pitch,
            TimeSpan.FromMilliseconds(500));

        Assert.True(result.WasTracked);
        Assert.Equal(Verdict.TooShort, result.Verdict);
        Assert.Equal(Verdict.TooShort, session.Verdicts[bassNote]);
        Assert.DoesNotContain(bassNote, session.ExpectedNotes);
        Assert.Equal(1, session.WrongAttemptCount);
    }

    [Fact]
    public void Release_PitchAndHoldLongBassNoteForWrittenDuration_MarksCorrect()
    {
        var bassNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            Staff.Bass,
            new NoteValue(2));
        var trebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([bassNote, trebleNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(bassNote.Pitch, TimeSpan.Zero);
        session.Check(trebleNote.Pitch, TimeSpan.Zero);
        session.Release(trebleNote.Pitch, TimeSpan.FromMilliseconds(500));
        NoteReadingSession.ReleaseResult result = session.Release(
            bassNote.Pitch,
            TimeSpan.FromSeconds(1));

        Assert.Equal(Verdict.Correct, result.Verdict);
        Assert.Equal(Verdict.Correct, session.Verdicts[bassNote]);
        Assert.True(result.IsComplete);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Release_PitchAndHoldFinalNoteTooLate_MarksTooLongAndCompletes()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        NoteReadingSession.CheckResult checkResult = session.Check(note.Pitch, TimeSpan.Zero);

        Assert.False(checkResult.IsComplete);
        Assert.Contains(note, session.ExpectedNotes);

        NoteReadingSession.ReleaseResult releaseResult = session.Release(
            note.Pitch,
            TimeSpan.FromMilliseconds(700));

        Assert.Equal(Verdict.TooLong, releaseResult.Verdict);
        Assert.Equal(Verdict.TooLong, session.Verdicts[note]);
        Assert.True(releaseResult.IsComplete);
        Assert.Equal(0, session.FirstTryCorrectCount);
    }

    [Fact]
    public void Release_PitchAndHoldTiedNotes_UsesCombinedDuration()
    {
        var firstNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            noteValue: new NoteValue(4),
            tiesToNext: true);
        var tiedNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 1,
            noteValue: new NoteValue(4));
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, tiedNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(firstNote.Pitch, TimeSpan.Zero);
        session.Release(firstNote.Pitch, TimeSpan.FromMilliseconds(500));

        Assert.Equal(Verdict.TooShort, session.Verdicts[firstNote]);
        Assert.Equal(Verdict.TooShort, session.Verdicts[tiedNote]);
    }

    [Theory]
    [InlineData(400, Verdict.Early)]
    [InlineData(500, Verdict.Correct)]
    [InlineData(600, Verdict.Late)]
    public void Check_PitchHoldAndRhythmSecondOnset_ClassifiesTiming(
        int secondOnsetMilliseconds,
        Verdict expectedVerdict)
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));

        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));
        NoteReadingSession.CheckResult result = session.Check(
            secondNote.Pitch,
            TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(secondOnsetMilliseconds));

        Assert.True(result.IsCorrect);
        Assert.Equal(expectedVerdict, result.Verdict);
    }

    [Fact]
    public void ReleaseAll_PitchAndHoldActiveNotes_GradesEveryHoldAtReleaseTime()
    {
        var bassNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            Staff.Bass,
            new NoteValue(2));
        var trebleNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([bassNote, trebleNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(bassNote.Pitch, TimeSpan.Zero);
        session.Check(trebleNote.Pitch, TimeSpan.Zero);

        session.ReleaseAll(TimeSpan.FromMilliseconds(500));

        Assert.Equal(Verdict.TooShort, session.Verdicts[bassNote]);
        Assert.Equal(Verdict.Correct, session.Verdicts[trebleNote]);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Release_PitchAndHoldChordToneBeforeChordCompletes_RequiresPitchAgain()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(firstNote.Pitch, TimeSpan.Zero);
        session.Release(firstNote.Pitch, TimeSpan.FromMilliseconds(500));
        NoteReadingSession.CheckResult secondResult = session.Check(
            secondNote.Pitch,
            TimeSpan.FromMilliseconds(500));

        Assert.False(secondResult.DidAdvance);
        Assert.Equal(firstNote, Assert.Single(session.ExpectedNotes, note => note == firstNote));

        NoteReadingSession.CheckResult retryResult = session.Check(
            firstNote.Pitch,
            TimeSpan.FromMilliseconds(500));

        Assert.True(retryResult.DidAdvance);
    }

    [Fact]
    public void Check_PitchHoldAndRhythmWrongPitchBeforeNonzeroFirstOnset_DoesNotMoveAnchor()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 1);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 2);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));

        session.Check(new Pitch(NoteLetter.G, 0, 4), TimeSpan.FromSeconds(9));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));
        NoteReadingSession.CheckResult result = session.Check(
            secondNote.Pitch,
            TimeSpan.FromSeconds(10.5));

        Assert.Equal(Verdict.Correct, result.Verdict);
    }

    [Fact]
    public void Release_PitchHoldAndRhythmEarlyOnsetAndShortHold_ReportsBothVerdicts()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));
        session.Check(secondNote.Pitch, TimeSpan.FromSeconds(10.4));

        NoteReadingSession.ReleaseResult result = session.Release(
            secondNote.Pitch,
            TimeSpan.FromSeconds(10.5));

        Assert.Equal(Verdict.TooShort, result.Verdict);
        Assert.Equal(Verdict.Early, result.OnsetVerdict);
        Assert.Equal(Verdict.TooShort, result.DurationVerdict);
        Assert.Equal(Verdict.TooShort, session.Verdicts[secondNote]);
        Assert.Equal(1, session.WrongAttemptCount);
    }

    [Fact]
    public void CompletedPromptCount_PitchAndHoldLongNote_RemainsPendingUntilRelease()
    {
        var note = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            noteValue: new NoteValue(2));
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(note.Pitch, TimeSpan.Zero);

        Assert.Equal(0, session.CompletedPromptCount);
        Assert.Equal(0, session.FirstTryCorrectCount);

        session.Release(note.Pitch, TimeSpan.FromSeconds(1));

        Assert.Equal(1, session.CompletedPromptCount);
        Assert.Equal(1, session.FirstTryCorrectCount);
        Assert.Equal(100, session.FirstTryAccuracyPercent);
    }

    // Deliberately updated for Task 2 (proportional release tolerance): the quarter note lasts 500 ms at 120 BPM, so
    // its release window is now max(60 ms onset tolerance, 25% of 500 ms) = 125 ms instead of the fixed 60 ms.
    // The inclusive edges therefore moved from 440/560 ms to 375/625 ms; the inclusive behavior itself is unchanged.
    [Theory]
    [InlineData(374, Verdict.TooShort)]
    [InlineData(375, Verdict.Correct)]
    [InlineData(625, Verdict.Correct)]
    [InlineData(626, Verdict.TooLong)]
    public void Release_PitchAndHoldAtToleranceBoundary_ClassifiesInclusively(
        int releaseMilliseconds,
        Verdict expectedVerdict)
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.Zero);

        NoteReadingSession.ReleaseResult result = session.Release(
            note.Pitch,
            TimeSpan.FromMilliseconds(releaseMilliseconds));

        Assert.Equal(expectedVerdict, result.Verdict);
    }

    [Theory]
    [InlineData(400, Verdict.Correct)]
    [InlineData(300, Verdict.TooShort)]
    [InlineData(700, Verdict.TooLong)]
    public void Release_PitchAndHoldReleasedAtFractionOfWrittenValue_UsesProportionalWindow(
        int releaseMilliseconds,
        Verdict expectedVerdict)
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.Zero);

        NoteReadingSession.ReleaseResult result = session.Release(
            note.Pitch,
            TimeSpan.FromMilliseconds(releaseMilliseconds));

        Assert.Equal(expectedVerdict, result.Verdict);
    }

    [Theory]
    [InlineData(66, Verdict.Correct)]
    [InlineData(64, Verdict.TooShort)]
    [InlineData(184, Verdict.Correct)]
    [InlineData(186, Verdict.TooLong)]
    public void Release_PitchAndHoldVeryShortNote_NeverGetsWindowBelowOnsetTolerance(
        int releaseMilliseconds,
        Verdict expectedVerdict)
    {
        // A sixteenth note lasts 125 ms at 120 BPM; 25% of that is only about 31 ms, so the 60 ms floor applies.
        var note = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            noteValue: new NoteValue(16));
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.Zero);

        NoteReadingSession.ReleaseResult result = session.Release(
            note.Pitch,
            TimeSpan.FromMilliseconds(releaseMilliseconds));

        Assert.Equal(expectedVerdict, result.Verdict);
    }

    [Fact]
    public void Check_PitchHoldAndRhythmOnsetNearProportionalWindow_StillUsesOnsetTolerance()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromMilliseconds(10_500));

        NoteReadingSession.CheckResult result = session.Check(
            secondNote.Pitch,
            TimeSpan.FromMilliseconds(10_600));

        Assert.Equal(Verdict.Late, result.Verdict);
    }

    [Fact]
    public void Release_PitchAndHoldSameOnsetUnison_GradesBothScoreNotesFromOneKey()
    {
        var trebleNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0, Staff.Treble);
        var bassNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0, Staff.Bass);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([trebleNote, bassNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(trebleNote.Pitch, TimeSpan.Zero);
        session.Release(trebleNote.Pitch, TimeSpan.FromMilliseconds(500));

        Assert.Equal(Verdict.Correct, session.Verdicts[trebleNote]);
        Assert.Equal(Verdict.Correct, session.Verdicts[bassNote]);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Reset_ActiveHoldAndRhythmAnchor_ClearsModeProgress()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        Score score = CreateScore([firstNote, secondNote]);
        var session = new NoteReadingSession();
        session.Reset(
            score,
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));

        session.Reset(score, NoteReadingMode.Off, TimeSpan.FromMilliseconds(60));

        Assert.Empty(session.ExpectedNotes);
        Assert.Empty(session.Verdicts);
        Assert.Equal(0, session.PromptCount);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Check_WrongPitchDuringFinalHold_RecordsMistakeAgainstPendingPrompt()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.Zero);

        NoteReadingSession.CheckResult wrongResult = session.Check(
            new Pitch(NoteLetter.D, 0, 4),
            TimeSpan.FromMilliseconds(250));
        session.Release(note.Pitch, TimeSpan.FromMilliseconds(500));

        Assert.Equal(Verdict.WrongPitch, wrongResult.Verdict);
        Assert.Equal(1, session.WrongAttemptCount);
        Assert.Equal(0, session.FirstTryCorrectCount);
    }

    [Fact]
    public void Release_PitchAndHoldOverlappingSamePitchVoices_UsesOneCombinedHold()
    {
        var bassNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            Staff.Bass,
            new NoteValue(2));
        var trebleNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 1,
            Staff.Treble,
            new NoteValue(4));
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([bassNote, trebleNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        NoteReadingSession.CheckResult checkResult = session.Check(bassNote.Pitch, TimeSpan.Zero);
        NoteReadingSession.ReleaseResult releaseResult = session.Release(
            bassNote.Pitch,
            TimeSpan.FromSeconds(1));

        Assert.True(checkResult.DidAdvance);
        Assert.Equal(1, session.PromptCount);
        Assert.Equal(Verdict.Correct, releaseResult.Verdict);
        Assert.Equal(Verdict.Correct, session.Verdicts[bassNote]);
        Assert.Equal(Verdict.Correct, session.Verdicts[trebleNote]);
    }

    [Fact]
    public void Check_PitchHoldAndRhythmFirstChordMember_AnchorsLaterChordMember()
    {
        var firstChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstChordNote, secondChordNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));

        session.Check(firstChordNote.Pitch, TimeSpan.FromSeconds(10));
        NoteReadingSession.CheckResult result = session.Check(
            secondChordNote.Pitch,
            TimeSpan.FromSeconds(10.1));

        Assert.True(result.DidAdvance);
        Assert.Equal(Verdict.Late, result.Verdict);
    }

    [Fact]
    public void Reset_ChordOnsetsWithinToleranceBoundary_FormOneStepRequiringBothPitches()
    {
        var lowerChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 1e-9);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([lowerChordNote, upperChordNote]));

        Assert.Equal(1, session.PromptCount);

        NoteReadingSession.CheckResult firstResult = session.Check(upperChordNote.Pitch);
        Assert.True(firstResult.IsCorrect);
        Assert.False(firstResult.DidAdvance);

        NoteReadingSession.CheckResult secondResult = session.Check(lowerChordNote.Pitch);
        Assert.True(secondResult.DidAdvance);
        Assert.True(secondResult.IsComplete);
    }

    [Fact]
    public void Reset_OnsetsBeyondToleranceBoundary_FormSeparateSequentialSteps()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 2e-9);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([firstNote, secondNote]));

        Assert.Equal(2, session.PromptCount);

        NoteReadingSession.CheckResult firstResult = session.Check(firstNote.Pitch);
        Assert.True(firstResult.DidAdvance);
        Assert.False(firstResult.IsComplete);
        Assert.Equal(2e-9, session.CurrentOnsetBeats);

        NoteReadingSession.CheckResult secondResult = session.Check(secondNote.Pitch);
        Assert.True(secondResult.DidAdvance);
        Assert.True(secondResult.IsComplete);
    }

    [Fact]
    public void Reset_HoldModeSamePitchSecondOnsetEqualsFirstEnd_RemainsSeparateSteps()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        Assert.Equal(2, session.PromptCount);

        session.Check(firstNote.Pitch, TimeSpan.Zero);
        NoteReadingSession.ReleaseResult firstRelease = session.Release(
            firstNote.Pitch,
            TimeSpan.FromMilliseconds(500));
        Assert.Equal(Verdict.Correct, firstRelease.Verdict);

        NoteReadingSession.CheckResult secondCheck = session.Check(
            secondNote.Pitch,
            TimeSpan.FromMilliseconds(500));
        Assert.True(secondCheck.IsCorrect);
        Assert.True(secondCheck.DidAdvance);
    }

    [Fact]
    public void Reset_CompoundMeterDottedAndTupletNoteValues_GradesEachPromptInOrder()
    {
        var dottedQuarterNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            noteValue: new NoteValue(4, dots: 1));
        var tripletEighthNote = CreateNote(
            NoteLetter.D,
            measureIndex: 0,
            beatOffset: 3,
            noteValue: new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2));
        var followingEighthNote = CreateNote(
            NoteLetter.E,
            measureIndex: 0,
            beatOffset: 3 + (2.0 / 3.0));
        var score = new Score(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            0,
            [new ScoreMeasure([dottedQuarterNote, tripletEighthNote, followingEighthNote], [])]);
        var session = new NoteReadingSession();
        session.Reset(score);

        Assert.Equal(3, session.PromptCount);

        session.Check(dottedQuarterNote.Pitch);
        session.Check(tripletEighthNote.Pitch);
        NoteReadingSession.CheckResult finalResult = session.Check(followingEighthNote.Pitch);

        Assert.True(finalResult.DidAdvance);
        Assert.True(finalResult.IsComplete);
    }

    [Fact]
    public void Reset_InvalidNoteReadingMode_ThrowsArgumentOutOfRangeException()
    {
        var session = new NoteReadingSession();
        Score score = CreateScore([CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.Reset(score, (NoteReadingMode)999, TimeSpan.FromMilliseconds(60)));
    }

    [Fact]
    public void Reset_NegativeTimingTolerance_ThrowsArgumentOutOfRangeException()
    {
        var session = new NoteReadingSession();
        Score score = CreateScore([CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0)]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.Reset(score, NoteReadingMode.PitchAndOrder, TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Reset_NullScore_ProducesEmptyCompletedSession()
    {
        var session = new NoteReadingSession();

        session.Reset(null);

        Assert.Equal(0, session.PromptCount);
        Assert.True(session.IsComplete);
        Assert.Empty(session.ExpectedNotes);
        Assert.Empty(session.Verdicts);
    }

    [Fact]
    public void Reset_EmptyScore_ProducesEmptyCompletedSession()
    {
        var session = new NoteReadingSession();
        var emptyScore = new Score("test", new TimeSignature(4, new NoteValue(4)), new Tempo(120), 0, []);

        session.Reset(emptyScore);

        Assert.Equal(0, session.PromptCount);
        Assert.True(session.IsComplete);
        Assert.Empty(session.ExpectedNotes);
    }

    [Fact]
    public void Check_AfterSequenceComplete_ReturnsIncompleteResultWithoutRecordingMistake()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([note]));
        session.Check(note.Pitch);
        int wrongAttemptsBeforeExtraInput = session.WrongAttemptCount;

        NoteReadingSession.CheckResult result = session.Check(new Pitch(NoteLetter.D, 0, 4));

        Assert.False(result.IsCorrect);
        Assert.False(result.DidAdvance);
        Assert.True(result.IsComplete);
        Assert.Equal(wrongAttemptsBeforeExtraInput, session.WrongAttemptCount);
    }

    [Theory]
    [InlineData(NoteReadingMode.PitchAndOrder)]
    [InlineData(NoteReadingMode.PitchAndHold)]
    public void Release_UnknownPitch_ReturnsNotTrackedWithoutThrowing(NoteReadingMode mode)
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            mode,
            TimeSpan.FromMilliseconds(60));

        NoteReadingSession.ReleaseResult result = session.Release(
            new Pitch(NoteLetter.D, 0, 4),
            TimeSpan.FromMilliseconds(100));

        Assert.False(result.WasTracked);
        Assert.Null(result.Verdict);
    }

    [Fact]
    public void ReleaseAll_NoTrackedNotes_DoesNotThrowOrChangeState()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([note]));

        session.ReleaseAll();

        Assert.Empty(session.Verdicts);
        Assert.Equal(note, Assert.Single(session.ExpectedNotes));
    }

    [Fact]
    public void ReleaseAll_CalledTwiceAfterHoldAlreadyReleased_IsIdempotent()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.Zero);
        session.ReleaseAll(TimeSpan.FromMilliseconds(500));

        session.ReleaseAll(TimeSpan.FromMilliseconds(600));

        Assert.True(session.IsComplete);
        Assert.Equal(Verdict.Correct, session.Verdicts[note]);
    }

    [Fact]
    public void Check_WrongPitchWithinChordThenCorrect_RecordsMistakeAndCompletesChord()
    {
        var lowerChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([lowerChordNote, upperChordNote]));

        NoteReadingSession.CheckResult wrongResult = session.Check(new Pitch(NoteLetter.D, 0, 4));
        Assert.Equal(Verdict.WrongPitch, wrongResult.Verdict);

        session.Check(lowerChordNote.Pitch);
        NoteReadingSession.CheckResult finalResult = session.Check(upperChordNote.Pitch);

        Assert.True(finalResult.DidAdvance);
        Assert.True(finalResult.IsComplete);
        Assert.Equal(1, session.WrongAttemptCount);
        Assert.Equal(0, session.FirstTryCorrectCount);
    }

    [Fact]
    public void PromptResults_SingleNoteCorrectFirstTry_PublishesCompletedFirstTryResult()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession(new FakeTimeProvider());
        session.Reset(CreateScore([note]));

        session.Check(note.Pitch);

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(0, result.PromptIndex);
        Assert.Equal(0, result.OnsetBeats);
        Assert.Equal(note, Assert.Single(result.ExpectedSourceNotes));
        Assert.Equal(note.Pitch, Assert.Single(result.ExpectedPitches));
        Assert.Empty(result.WrongPlayedPitches);
        Assert.Equal(0, result.WrongAttemptCount);
        Assert.True(result.IsFirstTryCorrect);
        Assert.True(result.IsComplete);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public void PromptResults_WrongThenCorrectPitch_RecordsWrongPlayedPitchAndFirstTryFailure()
    {
        var expectedNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession(new FakeTimeProvider());
        session.Reset(CreateScore([expectedNote]));
        var wrongPitch = new Pitch(NoteLetter.D, 0, 4);

        session.Check(wrongPitch);
        session.Check(expectedNote.Pitch);

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(wrongPitch, Assert.Single(result.WrongPlayedPitches));
        Assert.Equal(1, result.WrongAttemptCount);
        Assert.False(result.IsFirstTryCorrect);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void PromptResults_ChordPrompt_ExposesBothExpectedSourceNotesAndPitches()
    {
        var lowerChordNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperChordNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([lowerChordNote, upperChordNote]));

        session.Check(upperChordNote.Pitch);
        session.Check(lowerChordNote.Pitch);

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(2, result.ExpectedSourceNotes.Length);
        Assert.Contains(lowerChordNote, result.ExpectedSourceNotes);
        Assert.Contains(upperChordNote, result.ExpectedSourceNotes);
        Assert.Equal(2, result.ExpectedPitches.Length);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void PromptResults_TiedNotes_ExpectedSourceNotesIncludesBothTiedScoreNotes()
    {
        var firstNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 0,
            noteValue: new NoteValue(4),
            tiesToNext: true);
        var tiedNote = CreateNote(
            NoteLetter.C,
            measureIndex: 0,
            beatOffset: 1,
            noteValue: new NoteValue(4));
        var session = new NoteReadingSession();
        session.Reset(CreateScore([firstNote, tiedNote]));

        session.Check(firstNote.Pitch);

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(2, result.ExpectedSourceNotes.Length);
        Assert.Contains(firstNote, result.ExpectedSourceNotes);
        Assert.Contains(tiedNote, result.ExpectedSourceNotes);
    }

    [Fact]
    public void PromptResults_HoldModeDurationMistake_RecordsWrongAttemptWithoutWrongPitch()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));

        session.Check(note.Pitch, TimeSpan.Zero);
        session.Release(note.Pitch, TimeSpan.FromMilliseconds(700));

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(1, result.WrongAttemptCount);
        Assert.Empty(result.WrongPlayedPitches);
        Assert.False(result.IsFirstTryCorrect);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public void PromptResults_WrongAttemptOnInProgressPrompt_PublishesIncompleteResult()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([firstNote, secondNote]));

        session.Check(new Pitch(NoteLetter.G, 0, 4));

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(0, result.PromptIndex);
        Assert.False(result.IsComplete);
        Assert.Null(result.CompletedAt);
        Assert.Equal(1, result.WrongAttemptCount);
    }

    [Fact]
    public void PromptResults_PublishedResult_CannotBeMutatedByCaller()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([note]));
        session.Check(note.Pitch);

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        int originalCount = result.ExpectedSourceNotes.Length;
        ImmutableArray<ScoreNote> mutationAttempt = result.ExpectedSourceNotes.Add(
            CreateNote(NoteLetter.G, measureIndex: 0, beatOffset: 2));

        Assert.Equal(originalCount, result.ExpectedSourceNotes.Length);
        Assert.NotEqual(originalCount, mutationAttempt.Length);
    }

    [Fact]
    public void ElapsedTime_BeforeFirstAttempt_RemainsZeroDespiteTimePassing()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var timeProvider = new FakeTimeProvider();
        var session = new NoteReadingSession(timeProvider);
        session.Reset(CreateScore([note]));

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.Zero, session.ElapsedTime);
    }

    [Fact]
    public void ElapsedTime_MeasuredFromFirstAttemptNotFromReset()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var timeProvider = new FakeTimeProvider();
        var session = new NoteReadingSession(timeProvider);
        session.Reset(CreateScore([note]));
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        session.Check(new Pitch(NoteLetter.D, 0, 4));
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        session.Check(note.Pitch);

        Assert.Equal(TimeSpan.FromSeconds(2), session.ElapsedTime);
    }

    [Fact]
    public void ElapsedTime_AfterRetryReset_StartsFromZeroAgain()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        Score score = CreateScore([note]);
        var timeProvider = new FakeTimeProvider();
        var session = new NoteReadingSession(timeProvider);
        session.Reset(score);
        session.Check(note.Pitch);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        session.Reset(score);

        Assert.Equal(TimeSpan.Zero, session.ElapsedTime);
    }

    [Theory]
    [InlineData(9939, Verdict.Early)]
    [InlineData(9940, Verdict.Correct)]
    [InlineData(10060, Verdict.Correct)]
    [InlineData(10061, Verdict.Late)]
    public void Reset_ExplicitAnchorAtToleranceBoundary_ClassifiesInclusively(
        int eventTimeMilliseconds,
        Verdict expectedVerdict)
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        NoteReadingSession.CheckResult result = session.Check(
            note.Pitch,
            TimeSpan.FromMilliseconds(eventTimeMilliseconds));

        Assert.Equal(expectedVerdict, result.Verdict);
    }

    [Fact]
    public void Reset_ExplicitAnchorWithNonzeroFirstScoreOnset_GradesAgainstAnchorPlusOnsetOffset()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 2);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        // Score onset is beat 2; at 120 BPM (500 ms/beat) the expected onset is 10s + 1s = 11s.
        NoteReadingSession.CheckResult onTimeResult = session.Check(note.Pitch, TimeSpan.FromSeconds(11));

        Assert.Equal(Verdict.Correct, onTimeResult.Verdict);
    }

    [Fact]
    public void Reset_ExplicitAnchorInCompoundMeter_GradesAgainstEighthNoteBeatUnit()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 3);
        Score score = new(
            "test",
            new TimeSignature(6, new NoteValue(8)),
            new Tempo(120),
            0,
            [new ScoreMeasure([note], [])]);
        var session = new NoteReadingSession();
        session.Reset(
            score,
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        // Beat unit is the eighth note; onset beat 3 at 120 (eighth notes)/minute = 500 ms each, so 1.5s after anchor.
        NoteReadingSession.CheckResult onTimeResult = session.Check(note.Pitch, TimeSpan.FromSeconds(11.5));

        Assert.Equal(Verdict.Correct, onTimeResult.Verdict);
    }

    [Fact]
    public void Reset_WithoutExplicitAnchor_PreservesLazyAnchoringForFirstOnset()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60));

        // With no explicit anchor, the first played event always establishes the anchor and grades on-time,
        // regardless of when it's played.
        NoteReadingSession.CheckResult result = session.Check(note.Pitch, TimeSpan.FromSeconds(37));

        Assert.Equal(Verdict.Correct, result.Verdict);
    }

    [Fact]
    public void Reset_CalledAgainWithoutExplicitAnchor_ClearsPreviousExplicitAnchor()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        Score score = CreateScore([note]);
        var session = new NoteReadingSession();
        session.Reset(
            score,
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        session.Reset(score, NoteReadingMode.PitchHoldAndRhythm, TimeSpan.FromMilliseconds(60));
        NoteReadingSession.CheckResult result = session.Check(note.Pitch, TimeSpan.FromSeconds(999));

        Assert.Equal(Verdict.Correct, result.Verdict);
    }

    [Fact]
    public void Check_PitchOnlyMode_LeavesTimingOutcomesNull()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([note]));

        session.Check(note.Pitch, TimeSpan.FromSeconds(3));

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.True(result.IsFirstTryCorrect);
        Assert.Null(result.OnsetVerdict);
        Assert.Null(result.DurationVerdict);
        Assert.Null(result.OnsetDeviation);
        Assert.Equal(0, session.PitchMistakeCount);
        Assert.Equal(0, session.TimingMistakeCount);
    }

    [Fact]
    public void Check_WrongThenCorrectPitchOnTime_RecordsPitchMistakeButCorrectOnset()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));

        session.Check(new Pitch(NoteLetter.G, 0, 4), TimeSpan.FromSeconds(10.45));
        session.Check(secondNote.Pitch, TimeSpan.FromSeconds(10.5));
        session.Release(secondNote.Pitch, TimeSpan.FromSeconds(11));

        NoteReadingPromptResult result = session.PromptResults[1];
        Assert.False(result.IsPitchFirstTryCorrect);
        Assert.False(result.IsFirstTryCorrect);
        Assert.Equal(Verdict.Correct, result.OnsetVerdict);
        Assert.Equal(TimeSpan.Zero, result.OnsetDeviation);
        Assert.Equal(1, session.PitchMistakeCount);
        Assert.Equal(0, session.TimingMistakeCount);
        Assert.Equal(session.WrongAttemptCount, session.PitchMistakeCount + session.TimingMistakeCount);
    }

    [Fact]
    public void Check_RightPitchLate_KeepsPitchCorrectAndRecordsLateOnset()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));

        session.Check(secondNote.Pitch, TimeSpan.FromMilliseconds(10_585));
        session.Release(secondNote.Pitch, TimeSpan.FromMilliseconds(11_085));

        NoteReadingPromptResult result = session.PromptResults[1];
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.False(result.IsFirstTryCorrect);
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(85), result.OnsetDeviation);
        Assert.Equal(Verdict.Correct, result.DurationVerdict);
        Assert.Equal(0, session.PitchMistakeCount);
        Assert.Equal(1, session.TimingMistakeCount);
        Assert.Equal(session.WrongAttemptCount, session.PitchMistakeCount + session.TimingMistakeCount);
    }

    [Fact]
    public void Release_PitchAndHoldNoteLiftedTooSoon_RecordsDurationOnly()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndHold,
            TimeSpan.FromMilliseconds(60));
        session.Check(note.Pitch, TimeSpan.FromSeconds(1));

        NoteReadingPromptResult beforeRelease = Assert.Single(session.PromptResults);
        Assert.Null(beforeRelease.OnsetVerdict);
        Assert.Null(beforeRelease.DurationVerdict);

        session.Release(note.Pitch, TimeSpan.FromSeconds(1.2));

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.False(result.IsFirstTryCorrect);
        Assert.Null(result.OnsetVerdict);
        Assert.Null(result.OnsetDeviation);
        Assert.Equal(Verdict.TooShort, result.DurationVerdict);
        Assert.Equal(0, session.PitchMistakeCount);
        Assert.Equal(1, session.TimingMistakeCount);
    }

    [Fact]
    public void Release_PitchHoldAndRhythmEarlyAndShort_CountsOneTimingMistakePerAttempt()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));
        session.Check(secondNote.Pitch, TimeSpan.FromSeconds(10.4));
        session.Release(secondNote.Pitch, TimeSpan.FromSeconds(10.5));

        NoteReadingPromptResult result = session.PromptResults[1];
        Assert.Equal(Verdict.Early, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(-100), result.OnsetDeviation);
        Assert.Equal(Verdict.TooShort, result.DurationVerdict);
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.Equal(1, session.TimingMistakeCount);
    }

    [Fact]
    public void Check_RhythmChordWithLateMember_ReportsWorstOnsetOfTheChord()
    {
        var lowerNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upperNote = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([lowerNote, upperNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        session.Check(lowerNote.Pitch, TimeSpan.FromMilliseconds(10_010));
        session.Check(upperNote.Pitch, TimeSpan.FromMilliseconds(10_120));
        session.Release(lowerNote.Pitch, TimeSpan.FromSeconds(10.5));
        session.Release(upperNote.Pitch, TimeSpan.FromSeconds(10.5));

        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(120), result.OnsetDeviation);
        Assert.True(result.IsPitchFirstTryCorrect);
    }

    [Fact]
    public void Check_PitchOnlyMode_RecordsResponseTimeFromPreviousAttackToFirstCorrectAttack()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(CreateScore([firstNote, secondNote]));

        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(5));
        session.Check(new Pitch(NoteLetter.G, 0, 4), TimeSpan.FromSeconds(6));
        session.Check(secondNote.Pitch, TimeSpan.FromSeconds(7.5));

        Assert.Null(session.PromptResults[0].ResponseTime);
        Assert.Equal(TimeSpan.FromSeconds(2.5), session.PromptResults[1].ResponseTime);
    }

    [Fact]
    public void Check_RhythmMode_LeavesResponseTimeNull()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchHoldAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        session.Release(firstNote.Pitch, TimeSpan.FromSeconds(10.5));
        session.Check(secondNote.Pitch, TimeSpan.FromSeconds(10.5));

        Assert.All(session.PromptResults, result => Assert.Null(result.ResponseTime));
    }

    [Fact]
    public void Reset_AfterMistakes_ClearsMistakeCounts()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        Score score = CreateScore([note]);
        var session = new NoteReadingSession();
        session.Reset(score);
        session.Check(new Pitch(NoteLetter.D, 0, 4));

        session.Reset(score);

        Assert.Equal(0, session.PitchMistakeCount);
        Assert.Equal(0, session.TimingMistakeCount);
    }

    [Fact]
    public void Check_PitchAndRhythmLateRightPitch_RecordsOnsetVerdictWithoutHoldTracking()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.PitchAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        session.Check(firstNote.Pitch, TimeSpan.FromSeconds(10));
        NoteReadingSession.CheckResult late = session.Check(secondNote.Pitch, TimeSpan.FromMilliseconds(10_600));

        Assert.Equal(Verdict.Late, late.Verdict);
        Assert.True(session.IsComplete);
        Assert.Equal(Verdict.Late, session.Verdicts[secondNote]);
        NoteReadingPromptResult result = session.PromptResults[1];
        Assert.True(result.IsPitchFirstTryCorrect);
        Assert.Equal(Verdict.Late, result.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(100), result.OnsetDeviation);
        Assert.Null(result.DurationVerdict);
        Assert.Equal(1, session.TimingMistakeCount);
        Assert.Equal(0, session.PitchMistakeCount);
    }

    [Fact]
    public void Release_PitchAndRhythm_DoesNotTrackHolds()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(note.Pitch, TimeSpan.FromSeconds(10));

        NoteReadingSession.ReleaseResult release = session.Release(note.Pitch, TimeSpan.FromMilliseconds(10_050));

        Assert.False(release.WasTracked);
        Assert.Equal(Verdict.Correct, session.Verdicts[note]);
    }

    [Fact]
    public void Check_PitchAndRhythmWrongKey_IsAPitchMistakeAndOnsetStaysGraded()
    {
        var note = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([note]),
            NoteReadingMode.PitchAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        NoteReadingSession.CheckResult wrong = session.Check(new Pitch(NoteLetter.E, 0, 4), TimeSpan.FromSeconds(10));
        session.Check(note.Pitch, TimeSpan.FromMilliseconds(10_010));

        Assert.Equal(Verdict.WrongPitch, wrong.Verdict);
        NoteReadingPromptResult result = Assert.Single(session.PromptResults);
        Assert.False(result.IsPitchFirstTryCorrect);
        Assert.Equal(Verdict.Correct, result.OnsetVerdict);
    }

    [Fact]
    public void Release_PitchAndRhythmChordMemberBeforeCompletion_UnmatchesItLikePitchOnly()
    {
        var lower = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upper = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([lower, upper]),
            NoteReadingMode.PitchAndRhythm,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        session.Check(lower.Pitch, TimeSpan.FromSeconds(10));

        session.Release(lower.Pitch, TimeSpan.FromMilliseconds(10_020));

        Assert.Contains(lower, session.ExpectedNotes);
        Assert.False(session.IsComplete);
    }

    [Fact]
    public void Check_RhythmOnlyAnyKey_MatchesTheNextEventAndNeverRecordsAWrongPitch()
    {
        var firstNote = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var secondNote = CreateNote(NoteLetter.D, measureIndex: 0, beatOffset: 1);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([firstNote, secondNote]),
            NoteReadingMode.RhythmOnly,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        NoteReadingSession.CheckResult first = session.Check(new Pitch(NoteLetter.A, 0, 2), TimeSpan.FromSeconds(10));
        NoteReadingSession.CheckResult second = session.Check(new Pitch(NoteLetter.G, 1, 6), TimeSpan.FromMilliseconds(10_430));

        Assert.True(first.IsCorrect);
        Assert.True(first.DidAdvance);
        Assert.Equal(Verdict.Correct, first.Verdict);
        Assert.True(second.IsCorrect);
        Assert.Equal(Verdict.Early, second.Verdict);
        Assert.True(session.IsComplete);
        Assert.Equal(0, session.PitchMistakeCount);
        Assert.Equal(1, session.TimingMistakeCount);
        Assert.All(session.PromptResults, result =>
        {
            Assert.True(result.IsPitchFirstTryCorrect);
            Assert.Empty(result.WrongPlayedPitches);
            Assert.Null(result.DurationVerdict);
            Assert.Null(result.ResponseTime);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(-70), session.PromptResults[1].OnsetDeviation);
        Assert.Equal(Verdict.Early, session.Verdicts[secondNote]);
    }

    [Fact]
    public void Check_RhythmOnlyChordStep_CompletesByPressCount()
    {
        var lower = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upper = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([lower, upper]),
            NoteReadingMode.RhythmOnly,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));

        NoteReadingSession.CheckResult first = session.Check(new Pitch(NoteLetter.A, 0, 3), TimeSpan.FromSeconds(10));
        Assert.True(first.IsCorrect);
        Assert.False(first.DidAdvance);
        Assert.False(session.IsComplete);

        NoteReadingSession.CheckResult second = session.Check(new Pitch(NoteLetter.B, 0, 3), TimeSpan.FromSeconds(10));
        Assert.True(second.DidAdvance);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Release_RhythmOnlyUnfinishedChordStep_UnmatchesThePressedKey()
    {
        var lower = CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0);
        var upper = CreateNote(NoteLetter.E, measureIndex: 0, beatOffset: 0);
        var session = new NoteReadingSession();
        session.Reset(
            CreateScore([lower, upper]),
            NoteReadingMode.RhythmOnly,
            TimeSpan.FromMilliseconds(60),
            explicitRhythmAnchor: TimeSpan.FromSeconds(10));
        var pressedKey = new Pitch(NoteLetter.A, 0, 3);
        session.Check(pressedKey, TimeSpan.FromSeconds(10));
        Assert.Single(session.ExpectedNotes);

        session.Release(pressedKey, TimeSpan.FromMilliseconds(10_030));

        Assert.Equal(2, session.ExpectedNotes.Count);
        session.Check(new Pitch(NoteLetter.B, 0, 3), TimeSpan.FromMilliseconds(10_040));
        Assert.False(session.IsComplete);
        session.Check(new Pitch(NoteLetter.D, 0, 4), TimeSpan.FromMilliseconds(10_050));
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Reset_NewModesWithUndefinedNeighbour_ThrowsForUndefinedValues()
    {
        var session = new NoteReadingSession();
        Score score = CreateScore([CreateNote(NoteLetter.C, measureIndex: 0, beatOffset: 0)]);

        session.Reset(score, NoteReadingMode.RhythmOnly, TimeSpan.FromMilliseconds(60));
        session.Reset(score, NoteReadingMode.PitchAndRhythm, TimeSpan.FromMilliseconds(60));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => session.Reset(score, (NoteReadingMode)6, TimeSpan.FromMilliseconds(60)));
    }

    private static ScoreNote CreateNote(
        NoteLetter letter,
        int measureIndex,
        double beatOffset,
        Staff staff = Staff.Treble,
        NoteValue? noteValue = null,
        bool tiesToNext = false) =>
        new(
            new Pitch(letter, 0, 4),
            noteValue ?? new NoteValue(4),
            measureIndex,
            beatOffset,
            staff,
            tiesToNext);

    private static Score CreateScore(IReadOnlyList<ScoreNote> notes) =>
        new(
            "test",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

    [Fact]
    public void Reset_TiedNotes_FormOnePromptWithBothSourceNotesAndOnePitch()
    {
        var first = new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 1, Staff.Treble, TiesToNext: true);
        var continuation = new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 2, Staff.Treble);
        var after = new ScoreNote(new Pitch(NoteLetter.G, 0, 4), new NoteValue(4), 0, 3, Staff.Treble);
        var session = new NoteReadingSession();

        session.Reset(CreateScore([first, continuation, after]));
        session.Check(first.Pitch);

        Assert.Equal(2, session.PromptCount);
        NoteReadingPromptResult tiedPrompt = session.PromptResults[0];
        Assert.Equal([first.Pitch], tiedPrompt.ExpectedPitches.ToArray());
        Assert.Equal([first, continuation], tiedPrompt.ExpectedSourceNotes.ToArray());
        Assert.Equal(1, tiedPrompt.OnsetBeats);
    }

    [Fact]
    public void Release_TiedNotesInHoldMode_AreHeldForTheirSummedDuration()
    {
        // CreateScore runs at 120 beats per minute, so each quarter note is 0.5 s and the tied pair is 1.0 s.
        var first = new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 0, Staff.Treble, TiesToNext: true);
        var continuation = new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(4), 0, 1, Staff.Treble);
        Score score = CreateScore([first, continuation]);
        var held = new NoteReadingSession();
        var lifted = new NoteReadingSession();
        held.Reset(score, NoteReadingMode.PitchAndHold, TimeSpan.FromMilliseconds(60));
        lifted.Reset(score, NoteReadingMode.PitchAndHold, TimeSpan.FromMilliseconds(60));

        held.Check(first.Pitch, TimeSpan.Zero);
        held.Release(first.Pitch, TimeSpan.FromSeconds(1));
        lifted.Check(first.Pitch, TimeSpan.Zero);
        lifted.Release(first.Pitch, TimeSpan.FromSeconds(0.5));

        Assert.Equal(Verdict.Correct, held.PromptResults[0].DurationVerdict);
        Assert.Equal(Verdict.TooShort, lifted.PromptResults[0].DurationVerdict);
    }
}
