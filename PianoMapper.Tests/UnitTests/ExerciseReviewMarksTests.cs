using System.Collections.Immutable;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;
using PianoMapper.Web.Rendering;

namespace PianoMapper.Tests.UnitTests;

public sealed class ExerciseReviewMarksTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly Pitch G4 = new(NoteLetter.G, 0, 4);

    [Fact]
    public void Classify_CleanPrompt_IsClean()
    {
        Assert.Equal(ReviewMark.Clean, ExerciseReviewMarks.Classify(Result(0, 0, C4)));
    }

    [Fact]
    public void Classify_WrongKeyBeforeTheRightOne_IsPitch()
    {
        NoteReadingPromptResult wrong = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WrongPlayedPitches = [D4],
            WrongAttemptCount = 1,
        };

        Assert.Equal(ReviewMark.Pitch, ExerciseReviewMarks.Classify(wrong));
    }

    [Theory]
    [InlineData(Verdict.Early)]
    [InlineData(Verdict.Late)]
    public void Classify_RightPitchOffTheBeat_IsTiming(Verdict onsetVerdict)
    {
        NoteReadingPromptResult off = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = true,
            OnsetVerdict = onsetVerdict,
            OnsetDeviation = TimeSpan.FromMilliseconds(onsetVerdict == Verdict.Late ? 90 : -90),
        };

        Assert.Equal(ReviewMark.Timing, ExerciseReviewMarks.Classify(off));
    }

    [Theory]
    [InlineData(Verdict.TooShort)]
    [InlineData(Verdict.TooLong)]
    public void Classify_RightPitchHeldTheWrongLength_IsTiming(Verdict durationVerdict)
    {
        NoteReadingPromptResult held = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = true,
            DurationVerdict = durationVerdict,
        };

        Assert.Equal(ReviewMark.Timing, ExerciseReviewMarks.Classify(held));
    }

    [Fact]
    public void Classify_PromptNeverPlayedInAModeThatGradesPitch_IsMissedNotPitch()
    {
        // The play-along mapper reports a skipped prompt as a failed pitch too (nothing was found), but no wrong
        // key was pressed, so the mark must say "missed".
        NoteReadingPromptResult skipped = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WasMissed = true,
            OnsetVerdict = Verdict.Missed,
        };

        Assert.Equal(ReviewMark.Missed, ExerciseReviewMarks.Classify(skipped));
    }

    [Fact]
    public void Classify_WrongKeyAndLate_PitchWinsOverTiming()
    {
        NoteReadingPromptResult both = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WrongPlayedPitches = [D4],
            OnsetVerdict = Verdict.Late,
        };

        Assert.Equal(ReviewMark.Pitch, ExerciseReviewMarks.Classify(both));
    }

    [Fact]
    public void Classify_ChordWithAWrongKeyAndAnUnplayedMember_PitchWinsOverMissed()
    {
        NoteReadingPromptResult chord = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WasMissed = true,
            WrongPlayedPitches = [D4],
        };

        Assert.Equal(ReviewMark.Pitch, ExerciseReviewMarks.Classify(chord));
    }

    [Fact]
    public void Classify_MissedAndOffTheBeat_MissedWinsOverTiming()
    {
        NoteReadingPromptResult chord = Result(0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = true,
            WasMissed = true,
            OnsetVerdict = Verdict.Late,
        };

        Assert.Equal(ReviewMark.Missed, ExerciseReviewMarks.Classify(chord));
    }

    [Fact]
    public void Classify_NotCleanWithNoStatedCause_IsPitchLikeTheOldFusedFlag()
    {
        // A result built before pitch and timing were separated only says "not first-try correct".
        NoteReadingPromptResult fused = Result(0, 0, C4) with { IsFirstTryCorrect = false };

        Assert.Equal(ReviewMark.Pitch, ExerciseReviewMarks.Classify(fused));
    }

    [Fact]
    public void Build_ChordMembersAndTiedSourceNotesAllShareTheirPromptsMark()
    {
        ScoreNote[] chordNotes = [Note(C4, 0, 0), Note(E4, 0, 0), Note(G4, 0, 0)];
        NoteReadingPromptResult chord = Result(0, 0, C4) with
        {
            ExpectedSourceNotes = [.. chordNotes],
            ExpectedPitches = [C4, E4, G4],
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WrongPlayedPitches = [D4],
        };
        ScoreNote clean = Note(D4, 0, 1);
        NoteReadingPromptResult cleanResult = Result(1, 1, D4) with { ExpectedSourceNotes = [clean] };

        IReadOnlyDictionary<ScoreNote, ReviewMark> marks = ExerciseReviewMarks.Build([chord, cleanResult]);

        Assert.Equal(4, marks.Count);
        Assert.All(chordNotes, note => Assert.Equal(ReviewMark.Pitch, marks[note]));
        Assert.Equal(ReviewMark.Clean, marks[clean]);
    }

    [Fact]
    public void Build_PromptStillInProgress_HasNoMarkYet()
    {
        ScoreNote note = Note(C4, 0, 0);
        NoteReadingPromptResult unfinished = Result(0, 0, C4) with
        {
            ExpectedSourceNotes = [note],
            IsComplete = false,
            IsFirstTryCorrect = false,
            IsPitchFirstTryCorrect = false,
            WrongPlayedPitches = [D4],
        };

        Assert.Empty(ExerciseReviewMarks.Build([unfinished]));
    }

    private static ScoreNote Note(Pitch pitch, int measureIndex, double beatOffset) =>
        new(pitch, new NoteValue(4), measureIndex, beatOffset, Staff.Treble);

    private static NoteReadingPromptResult Result(int promptIndex, double beatOffset, Pitch pitch) =>
        new(
            promptIndex,
            beatOffset,
            [Note(pitch, 0, beatOffset)],
            [pitch],
            ImmutableArray<Pitch>.Empty,
            0,
            IsFirstTryCorrect: true,
            IsComplete: true,
            CompletedAt: null);
}
