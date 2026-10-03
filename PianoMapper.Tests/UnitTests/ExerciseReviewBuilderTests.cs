using System.Collections.Immutable;
using PianoMapper.Music;
using PianoMapper.Practice;
using PianoMapper.Web.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class ExerciseReviewBuilderTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch D4 = new(NoteLetter.D, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);
    private static readonly Pitch G4 = new(NoteLetter.G, 0, 4);

    [Fact]
    public void Build_AllPromptsClean_HasNoLines()
    {
        ExerciseReview review = ExerciseReviewBuilder.Build(
            [Result(0, 0, 0, C4), Result(1, 0, 1, D4)],
            FourFour);

        Assert.Empty(review.Lines);
        Assert.Equal(0, review.MoreCount);
    }

    [Fact]
    public void Build_WrongPitch_ReportsBarBeatExpectedPlayedAndDistance()
    {
        NoteReadingPromptResult wrong = Result(1, measureIndex: 1, beatOffset: 2, C4) with
        {
            IsFirstTryCorrect = false,
            WrongAttemptCount = 1,
            WrongPlayedPitches = [D4],
            IsPitchFirstTryCorrect = false,
        };

        ExerciseReview review = ExerciseReviewBuilder.Build([wrong], FourFour);

        ExerciseReviewLine line = Assert.Single(review.Lines);
        Assert.Equal(2, line.BarNumber);
        Assert.Equal(3, line.BeatNumber);
        Assert.Equal([C4], line.ExpectedPitches);
        Assert.Equal(D4, line.PlayedPitch);
        Assert.Equal(PitchDifferenceKind.Interval, line.PitchDifference?.Kind);
        Assert.True(line.PitchDifference?.IsHigher);
        Assert.Equal(1, line.PitchDifference?.DiatonicSteps);
        Assert.False(line.WasMissed);
        Assert.Null(line.OnsetDeviation);
    }

    [Theory]
    [InlineData(Verdict.Late, 85)]
    [InlineData(Verdict.Early, -70)]
    public void Build_TimingMistake_ReportsTheSignedDeviation(Verdict verdict, int deviationMilliseconds)
    {
        NoteReadingPromptResult timing = Result(0, 0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            WrongAttemptCount = 1,
            OnsetVerdict = verdict,
            OnsetDeviation = TimeSpan.FromMilliseconds(deviationMilliseconds),
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([timing], FourFour).Lines);

        Assert.Equal(verdict, line.OnsetVerdict);
        Assert.Equal(TimeSpan.FromMilliseconds(deviationMilliseconds), line.OnsetDeviation);
        Assert.Null(line.PlayedPitch);
    }

    [Theory]
    [InlineData(Verdict.TooShort)]
    [InlineData(Verdict.TooLong)]
    public void Build_DurationMistake_ReportsTheDurationVerdict(Verdict verdict)
    {
        NoteReadingPromptResult duration = Result(0, 0, 0, C4) with
        {
            IsFirstTryCorrect = false,
            WrongAttemptCount = 1,
            DurationVerdict = verdict,
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([duration], FourFour).Lines);

        Assert.Equal(verdict, line.DurationVerdict);
    }

    [Fact]
    public void Build_MissedPrompt_IsReportedAsNotPlayed()
    {
        NoteReadingPromptResult missed = Result(0, 0, 1, C4) with
        {
            IsFirstTryCorrect = false,
            WasMissed = true,
            IsPitchFirstTryCorrect = false,
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([missed], FourFour).Lines);

        Assert.True(line.WasMissed);
        Assert.Null(line.PlayedPitch);
        Assert.Null(line.PitchDifference);
    }

    [Fact]
    public void Build_ChordWithAWrongKey_DescribesItAgainstTheNearestExpectedPitch()
    {
        ScoreNote[] notes = [Note(C4, 0, 0), Note(E4, 0, 0), Note(G4, 0, 0)];
        var chord = new NoteReadingPromptResult(
            0,
            0,
            [.. notes],
            [C4, E4, G4],
            [D4],
            1,
            IsFirstTryCorrect: false,
            IsComplete: true,
            CompletedAt: null)
        {
            IsPitchFirstTryCorrect = false,
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([chord], FourFour).Lines);

        Assert.Equal([C4, E4, G4], line.ExpectedPitches);
        Assert.Equal(D4, line.PlayedPitch);
        Assert.Equal(1, line.PitchDifference?.DiatonicSteps);
    }

    [Fact]
    public void Build_SixEightBeats_AreCountedFromTheMeasureStart()
    {
        var sixEight = new TimeSignature(6, new NoteValue(8));
        NoteReadingPromptResult late = Result(2, measureIndex: 3, beatOffset: 3, C4, beatsPerMeasure: 6) with
        {
            IsFirstTryCorrect = false,
            OnsetVerdict = Verdict.Late,
            OnsetDeviation = TimeSpan.FromMilliseconds(90),
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([late], sixEight).Lines);

        Assert.Equal(4, line.BarNumber);
        Assert.Equal(4, line.BeatNumber);
    }

    [Theory]
    [InlineData(3, 2, 4, 3)]
    [InlineData(2, 1, 4, 2)]
    public void Build_ThreeFourAndTwoFourBeats_AreCountedFromTheMeasureStart(
        int beatsPerMeasure,
        int beatOffset,
        int expectedBar,
        int expectedBeat)
    {
        var timeSignature = new TimeSignature(beatsPerMeasure, new NoteValue(4));
        NoteReadingPromptResult late = Result(2, measureIndex: 3, beatOffset, C4, beatsPerMeasure) with
        {
            IsFirstTryCorrect = false,
            OnsetVerdict = Verdict.Late,
            OnsetDeviation = TimeSpan.FromMilliseconds(90),
        };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([late], timeSignature).Lines);

        Assert.Equal(expectedBar, line.BarNumber);
        Assert.Equal(expectedBeat, line.BeatNumber);
    }

    [Fact]
    public void Build_FractionalBeat_KeepsTheFraction()
    {
        NoteReadingPromptResult eighth = Result(0, 0, 1.5, C4) with { IsFirstTryCorrect = false, WasMissed = true };

        ExerciseReviewLine line = Assert.Single(ExerciseReviewBuilder.Build([eighth], FourFour).Lines);

        Assert.Equal(2.5, line.BeatNumber);
    }

    [Fact]
    public void Build_MoreThanEightMistakes_ListsTheFirstEightAndCountsTheRest()
    {
        NoteReadingPromptResult[] results = Enumerable.Range(0, 12)
            .Select(index => Result(index, index / 4, index % 4, C4) with { IsFirstTryCorrect = false, WasMissed = true })
            .ToArray();

        ExerciseReview review = ExerciseReviewBuilder.Build(results, FourFour);

        Assert.Equal(ExerciseReviewBuilder.MaximumLines, review.Lines.Count);
        Assert.Equal(8, ExerciseReviewBuilder.MaximumLines);
        Assert.Equal(4, review.MoreCount);
        Assert.Equal(
            Enumerable.Range(0, 8).Select(index => (index / 4) + 1),
            review.Lines.Select(line => line.BarNumber));
    }

    [Fact]
    public void Build_LinesFollowPromptOrderEvenIfResultsArriveUnordered()
    {
        NoteReadingPromptResult later = Result(5, 1, 1, C4) with { IsFirstTryCorrect = false, WasMissed = true };
        NoteReadingPromptResult earlier = Result(1, 0, 1, C4) with { IsFirstTryCorrect = false, WasMissed = true };

        ExerciseReview review = ExerciseReviewBuilder.Build([later, earlier], FourFour);

        Assert.Equal([1, 2], review.Lines.Select(line => line.BarNumber));
    }

    [Fact]
    public void Describe_AWrongNoteThatWasAlsoLate_JoinsTheFindingsWithTheDirectionWording()
    {
        NoteReadingPromptResult both = Result(1, measureIndex: 1, beatOffset: 2, C4) with
        {
            IsFirstTryCorrect = false,
            WrongPlayedPitches = [D4],
            IsPitchFirstTryCorrect = false,
            OnsetVerdict = Verdict.Late,
            OnsetDeviation = TimeSpan.FromMilliseconds(85),
        };

        string text = ExerciseReviewBuilder.Describe(Assert.Single(ExerciseReviewBuilder.Build([both], FourFour).Lines));

        Assert.Contains("Bar 2", text);
        Assert.Contains("beat 3", text);
        Assert.Contains("C4", text);
        Assert.Contains("D4", text);
        Assert.Contains(PitchDistance.Describe(C4, D4), text);
        Assert.Contains("85 ms", text);
    }

    private static readonly TimeSignature FourFour = new(4, new NoteValue(4));

    private static ScoreNote Note(Pitch pitch, int measureIndex, double beatOffset) =>
        new(pitch, new NoteValue(4), measureIndex, beatOffset, Staff.Treble);

    private static NoteReadingPromptResult Result(
        int promptIndex,
        int measureIndex,
        double beatOffset,
        Pitch pitch,
        int beatsPerMeasure = 4) =>
        new(
            promptIndex,
            (measureIndex * beatsPerMeasure) + beatOffset,
            [Note(pitch, measureIndex, beatOffset)],
            [pitch],
            ImmutableArray<Pitch>.Empty,
            0,
            IsFirstTryCorrect: true,
            IsComplete: true,
            CompletedAt: null);
}
