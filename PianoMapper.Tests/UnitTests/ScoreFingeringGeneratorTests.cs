using PianoMapper.Music;

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

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ScoreFingeringGenerator.Generate(score));

        Assert.Contains("more than five", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("right hand", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static ScoreNote CreateNote(
        NoteLetter letter,
        int octave,
        Staff staff,
        double beatOffset,
        ScoreFingering? fingering = null) =>
        new(
            new Pitch(letter, 0, octave),
            new NoteValue(4),
            MeasureIndex: 0,
            beatOffset,
            staff,
            Fingering: fingering);

    private static Score CreateScore(IReadOnlyList<ScoreNote> notes) =>
        new(
            "Generated fingerings",
            new TimeSignature(12, new NoteValue(4)),
            new Tempo(120),
            0,
            [new ScoreMeasure(notes, [])]);

    private static int[] FingeringNumbers(Score score) =>
        score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => Assert.IsType<ScoreFingering>(note.Fingering).Number)
            .ToArray();
}
