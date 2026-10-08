using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringGeneratorTests
{
    [Fact]
    public void Generate_RightHandAscendingScale_ReplacesEveryFingering()
    {
        int[] existingFingerings = [5, 5, 5, 5, 5, 5, 5, 5];
        Score score = CreateScale(Staff.Treble, existingFingerings);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal([1, 2, 3, 1, 2, 3, 4, 5], FingeringNumbers(generated));
        Assert.Equal(existingFingerings, FingeringNumbers(score));
    }

    [Fact]
    public void Generate_LeftHandAscendingScale_AssignsConventionalFingering()
    {
        Score score = CreateScale(Staff.Bass, []);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal([5, 4, 3, 2, 1, 3, 2, 1], FingeringNumbers(generated));
    }

    [Theory]
    [InlineData(Staff.Treble, new[] { 5, 4, 3, 2, 1, 3, 2, 1 })]
    [InlineData(Staff.Bass, new[] { 1, 2, 3, 1, 2, 3, 4, 5 })]
    public void Generate_DescendingScale_AssignsConventionalFingering(
        Staff hand,
        int[] expectedFingerings)
    {
        Score ascending = CreateScale(hand, []);
        Score score = ascending with
        {
            Measures =
            [
                ascending.Measures[0] with
                {
                    Notes = ascending.Measures[0].Notes
                        .Reverse()
                        .Select((note, index) => note with { BeatOffset = index })
                        .ToArray(),
                },
            ],
        };

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal(expectedFingerings, FingeringNumbers(generated));
    }

    [Fact]
    public void Generate_MajorTriads_AssignsDistinctFingersAndPreservesHands()
    {
        var notes = new[]
        {
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.C, 3, Staff.Bass, beatOffset: 0),
            CreateNote(NoteLetter.E, 3, Staff.Bass, beatOffset: 0),
            CreateNote(NoteLetter.G, 3, Staff.Bass, beatOffset: 0),
        };
        Score score = CreateScore(notes);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal([1, 3, 5, 5, 3, 1], FingeringNumbers(generated));
        Assert.Equal(notes.Select(note => note.Staff), generated.Measures[0].Notes.Select(note => note.Staff));
    }

    [Fact]
    public void Generate_ExistingPlacement_ReplacesNumberAndPreservesPlacement()
    {
        Score score = CreateScore(
        [
            CreateNote(
                NoteLetter.C,
                4,
                Staff.Treble,
                beatOffset: 0,
                new ScoreFingering(5, ScoreFingeringPlacement.Above)),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.NotEqual(5, generated.Measures[0].Notes[0].Fingering?.Number);
        Assert.Equal(ScoreFingeringPlacement.Above, generated.Measures[0].Notes[0].Fingering?.Placement);
        Assert.All(generated.Measures[0].Notes, note => Assert.InRange(note.Fingering!.Number, 1, 5));
    }

    [Fact]
    public void Generate_DoubledUnison_AssignsTheSameFingerToBothNotes()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal(
            generated.Measures[0].Notes[0].Fingering,
            generated.Measures[0].Notes[1].Fingering);
    }

    [Fact]
    public void Generate_MoreThanFiveDistinctSameHandChordNotes_ThrowsReadableError()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 0),
        ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score));

        Assert.Equal(new ScoreFingeringNoteAddress(0, 5), exception.Address);
        Assert.Contains("more than five", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("right hand", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // The default is nine white-key centre spacings between thumb and little finger. C4 to D5 (the learner's reach, a
    // ninth by interval name) is eight spacings and C4 to E5 (a tenth) is nine: the default takes D5 and E5 and
    // rejects F5.
    [Theory]
    [InlineData(Staff.Treble, NoteLetter.D, 5, true)]
    [InlineData(Staff.Treble, NoteLetter.E, 5, true)]
    [InlineData(Staff.Treble, NoteLetter.F, 5, false)]
    [InlineData(Staff.Bass, NoteLetter.E, 5, true)]
    [InlineData(Staff.Bass, NoteLetter.F, 5, false)]
    public void Generate_DefaultProfile_AcceptsThumbToLittleFingerSpansUpToNineWhiteKeySpacings(
        Staff hand,
        NoteLetter topLetter,
        int topOctave,
        bool isAccepted)
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, hand, beatOffset: 0),
            CreateNote(topLetter, topOctave, hand, beatOffset: 0),
        ]);

        if (!isAccepted)
        {
            Assert.Throws<ScoreFingeringGenerationException>(() => ScoreFingeringGenerator.Generate(score));
            return;
        }

        Score generated = ScoreFingeringGenerator.Generate(score);

        Assert.Equal(hand == Staff.Treble ? [1, 5] : [5, 1], FingeringNumbers(generated));
    }

    [Fact]
    public void Generate_HeldFingerLockedForAnotherAttack_ReportsInfeasiblePassage()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, noteValue: new NoteValue(2)),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
        ]);
        var options = new ScoreFingeringGenerationOptions(
            locks:
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 1),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 1),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Equal(new ScoreFingeringNoteAddress(0, 1), exception.Address);
        Assert.Contains("held", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_TiedNotesWithDifferentLocks_ReportsConflictingLock()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, tiesToNext: true),
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 1),
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
        Assert.Contains("tie", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_NarrowerConfiguredTwoToFiveReach_IsEnforced()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 0),
        ]);
        var hand = new FingeringHandProfile(
            new FingeringReach(8, 8),
            [new FingeringFingerPairLimit(2, 5, new FingeringReach(7, 7))]);
        var options = new ScoreFingeringGenerationOptions(
            new ScoreFingeringProfile(hand, hand),
            [
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 0), 2),
                new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 1), 5),
            ]);

        ScoreFingeringGenerationException exception = Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(score, options));

        Assert.Contains("reach", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_HandsWithDifferentLimits_AreEnforcedIndependently()
    {
        ScoreFingeringProfile profile = CreateProfile(whiteKeySpan: 8, leftHandWhiteKeySpan: 5);
        Score rightHandNinth = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 0),
        ]);
        Score leftHandNinth = CreateScore(
        [
            CreateNote(NoteLetter.C, 3, Staff.Bass, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Bass, beatOffset: 0),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(rightHandNinth, profile);
        Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(leftHandNinth, profile));

        Assert.Equal([1, 5], FingeringNumbers(generated));
    }

    [Fact]
    public void Generate_FiveNoteChordOnBlackKeys_AllowsTheThumbOnABlackKey()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, alter: 1),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 0, alter: 1),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 0, alter: 1),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0, alter: 1),
            CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 0, alter: 1),
        ]);

        Assert.Equal([1, 2, 3, 4, 5], FingeringNumbers(ScoreFingeringGenerator.Generate(score)));
    }

    [Fact]
    public void Generate_BlackKeyAtEitherEndOfTheSpan_CountsTowardTheMaximum()
    {
        ScoreFingeringProfile profile = CreateProfile(8);
        Score blackLowerEnd = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0, alter: 1),
            CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 0),
        ]);
        Score blackUpperEnd = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 5, Staff.Treble, beatOffset: 0, alter: 1),
        ]);

        Score generated = ScoreFingeringGenerator.Generate(blackLowerEnd, profile);
        Assert.Throws<ScoreFingeringGenerationException>(
            () => ScoreFingeringGenerator.Generate(blackUpperEnd, profile));

        Assert.Equal([1, 5], FingeringNumbers(generated));
    }

    [Fact]
    public void Generate_Sixth_UsesThumbAndLittleFingerUnlessThatPairIsUncomfortable()
    {
        var uncomfortableHand = new FingeringHandProfile(
            new FingeringReach(6, 8),
            [new FingeringFingerPairLimit(1, 5, new FingeringReach(2, 8))]);
        var uncomfortableProfile = new ScoreFingeringProfile(uncomfortableHand, uncomfortableHand);

        Score byDefault = ScoreFingeringGenerator.Generate(CreateSixth());
        Score whenUncomfortable = ScoreFingeringGenerator.Generate(CreateSixth(), uncomfortableProfile);

        Assert.Equal([1, 5], FingeringNumbers(byDefault));
        Assert.NotEqual([1, 5], FingeringNumbers(whenUncomfortable));
        Assert.Empty(FingeringConstraintValidator.Validate(whenUncomfortable, uncomfortableProfile));
    }

    private static Score CreateScale(Staff staff, IReadOnlyList<int> fingerings)
    {
        NoteLetter[] letters =
        [
            NoteLetter.C,
            NoteLetter.D,
            NoteLetter.E,
            NoteLetter.F,
            NoteLetter.G,
            NoteLetter.A,
            NoteLetter.B,
            NoteLetter.C,
        ];
        ScoreNote[] notes = letters
            .Select((letter, index) => CreateNote(
                letter,
                index == letters.Length - 1 ? 5 : 4,
                staff,
                index,
                fingerings.Count == 0 ? null : new ScoreFingering(fingerings[index])))
            .ToArray();
        return CreateScore(notes);
    }

    private static Score CreateSixth() => CreateScore(
    [
        CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 0),
    ]);
}
