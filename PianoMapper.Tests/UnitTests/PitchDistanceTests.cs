using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class PitchDistanceTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);

    [Theory]
    [InlineData(NoteLetter.D, 0, 4, true, 1)]
    [InlineData(NoteLetter.E, 0, 4, true, 2)]
    [InlineData(NoteLetter.F, 0, 4, true, 3)]
    [InlineData(NoteLetter.G, 0, 4, true, 4)]
    [InlineData(NoteLetter.A, 0, 4, true, 5)]
    [InlineData(NoteLetter.B, 0, 4, true, 6)]
    [InlineData(NoteLetter.B, 0, 3, false, 1)]
    [InlineData(NoteLetter.A, 0, 3, false, 2)]
    [InlineData(NoteLetter.F, 0, 3, false, 4)]
    public void Measure_DifferentLetter_IsAnIntervalWithDirectionAndSteps(
        NoteLetter letter,
        int alter,
        int octave,
        bool isHigher,
        int diatonicSteps)
    {
        PitchDifference difference = PitchDistance.Measure(C4, new Pitch(letter, alter, octave));

        Assert.Equal(PitchDifferenceKind.Interval, difference.Kind);
        Assert.Equal(isHigher, difference.IsHigher);
        Assert.Equal(diatonicSteps, difference.DiatonicSteps);
        Assert.Equal(0, difference.Octaves);
        Assert.Equal(diatonicSteps, difference.IntervalSteps);
    }

    [Theory]
    [InlineData(5, true, 1)]
    [InlineData(3, false, 1)]
    [InlineData(6, true, 2)]
    [InlineData(2, false, 2)]
    public void Measure_SameLetterOtherOctave_IsOctaves(int octave, bool isHigher, int octaves)
    {
        PitchDifference difference = PitchDistance.Measure(C4, new Pitch(NoteLetter.C, 0, octave));

        Assert.Equal(PitchDifferenceKind.Octaves, difference.Kind);
        Assert.Equal(isHigher, difference.IsHigher);
        Assert.Equal(octaves, difference.Octaves);
        Assert.Equal(0, difference.IntervalSteps);
    }

    [Fact]
    public void Measure_IntervalWiderThanAnOctave_KeepsOctavesAndTheRemainder()
    {
        PitchDifference difference = PitchDistance.Measure(C4, new Pitch(NoteLetter.E, 0, 5));

        Assert.Equal(PitchDifferenceKind.Interval, difference.Kind);
        Assert.True(difference.IsHigher);
        Assert.Equal(1, difference.Octaves);
        Assert.Equal(2, difference.IntervalSteps);
        Assert.Equal(9, difference.DiatonicSteps);
    }

    [Theory]
    [InlineData(1, true, 1)]
    [InlineData(-1, false, 1)]
    [InlineData(2, true, 2)]
    [InlineData(-2, false, 2)]
    public void Measure_SameLetterAndOctaveOtherAccidental_IsAnAccidental(int alter, bool isSharper, int amount)
    {
        PitchDifference difference = PitchDistance.Measure(C4, new Pitch(NoteLetter.C, alter, 4));

        Assert.Equal(PitchDifferenceKind.Accidental, difference.Kind);
        Assert.Equal(isSharper, difference.IsHigher);
        Assert.Equal(amount, Math.Abs(difference.AlterDelta));
        Assert.Equal(0, difference.DiatonicSteps);
    }

    [Fact]
    public void Measure_EnharmonicSpelling_IsTheSameKey()
    {
        var expected = new Pitch(NoteLetter.C, 1, 4);
        var played = new Pitch(NoteLetter.D, -1, 4);

        PitchDifference difference = PitchDistance.Measure(expected, played);

        Assert.Equal(PitchDifferenceKind.SameKey, difference.Kind);
    }

    [Fact]
    public void Measure_EnharmonicAcrossAnOctaveBoundary_IsTheSameKey()
    {
        var expected = new Pitch(NoteLetter.B, 1, 3);

        PitchDifference difference = PitchDistance.Measure(expected, C4);

        Assert.Equal(PitchDifferenceKind.SameKey, difference.Kind);
    }

    [Fact]
    public void Measure_AccidentalNotesAreMeasuredByLetterPositionNotBySemitones()
    {
        // C4 to D#4 is a step on the staff (the accidental does not change which line or space it sits on).
        PitchDifference difference = PitchDistance.Measure(C4, new Pitch(NoteLetter.D, 1, 4));

        Assert.Equal(PitchDifferenceKind.Interval, difference.Kind);
        Assert.Equal(1, difference.DiatonicSteps);
    }

    [Fact]
    public void Measure_IdenticalPitches_IsTheSameKeyWithNoSteps()
    {
        PitchDifference difference = PitchDistance.Measure(C4, C4);

        Assert.Equal(PitchDifferenceKind.SameKey, difference.Kind);
        Assert.Equal(0, difference.DiatonicSteps);
        Assert.Equal(0, difference.AlterDelta);
    }

    [Fact]
    public void Measure_DoesNotDependOnTheStaff_OnlyOnThePitches()
    {
        // Treble and bass notes of the same pitches have the same distance: there is no staff in the input.
        var bassExpected = new Pitch(NoteLetter.C, 0, 3);
        var bassPlayed = new Pitch(NoteLetter.E, 0, 3);

        PitchDifference trebleDifference = PitchDistance.Measure(C4, new Pitch(NoteLetter.E, 0, 4));
        PitchDifference bassDifference = PitchDistance.Measure(bassExpected, bassPlayed);

        Assert.Equal(trebleDifference, bassDifference);
    }

    [Theory]
    [InlineData(NoteLetter.D, 0, 4)]
    [InlineData(NoteLetter.A, 0, 3)]
    [InlineData(NoteLetter.C, 0, 5)]
    [InlineData(NoteLetter.C, 1, 4)]
    [InlineData(NoteLetter.E, 0, 5)]
    public void Describe_EveryCategory_ReturnsNonBlankText(NoteLetter letter, int alter, int octave)
    {
        Assert.False(string.IsNullOrWhiteSpace(PitchDistance.Describe(C4, new Pitch(letter, alter, octave))));
    }

    [Fact]
    public void Describe_HigherAndLowerOfTheSameInterval_Differ()
    {
        string higher = PitchDistance.Describe(C4, new Pitch(NoteLetter.E, 0, 4));
        string lower = PitchDistance.Describe(C4, new Pitch(NoteLetter.A, 0, 3));

        Assert.NotEqual(higher, lower);
        Assert.Contains("higher", higher);
        Assert.Contains("lower", lower);
    }

    [Fact]
    public void Describe_StepAndOctaveAndSharp_UseTheDocumentedPhrases()
    {
        // The three phrases the plan names; the rest of the wording is intentionally not pinned.
        Assert.Equal("a step higher", PitchDistance.Describe(C4, new Pitch(NoteLetter.D, 0, 4)));
        Assert.Equal("same note, an octave higher", PitchDistance.Describe(C4, new Pitch(NoteLetter.C, 0, 5)));
        Assert.Equal("same note, sharp", PitchDistance.Describe(C4, new Pitch(NoteLetter.C, 1, 4)));
    }
}
