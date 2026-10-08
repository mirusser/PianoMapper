using PianoMapper.Music;
using static PianoMapper.Tests.UnitTests.FingeringTestScores;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreFingeringAlternativesTests
{
    [Fact]
    public void GenerateAlternatives_MelodicPassage_ReturnsFeasibleDistinctPathsInCostOrder()
    {
        Score score = CreateMelody();

        ScoreFingeringGenerationResult result = ScoreFingeringGenerator.GenerateAlternatives(
            score,
            new ScoreFingeringGenerationOptions(maximumAlternatives: 3));

        Assert.Equal(3, result.Alternatives.Count);
        Assert.Equal(
            result.Alternatives.Count,
            result.Alternatives.Select(alternative => string.Join(',', FingeringNumbers(alternative.Score))).Distinct().Count());
        Assert.Equal(result.Alternatives.Select(alternative => alternative.Cost).Order(), result.Alternatives.Select(alternative => alternative.Cost));
        Assert.All(
            result.Alternatives,
            alternative => Assert.Empty(FingeringConstraintValidator.Validate(alternative.Score, ScoreFingeringProfile.Default)));
        Assert.Equal(FingeringNumbers(ScoreFingeringGenerator.Generate(score)), FingeringNumbers(result.BestScore));
    }

    [Fact]
    public void GenerateAlternatives_RepeatedCalls_AreDeterministic()
    {
        Score score = CreateMelody();
        var options = new ScoreFingeringGenerationOptions(maximumAlternatives: 3);

        string[] first = Describe(ScoreFingeringGenerator.GenerateAlternatives(score, options));
        string[] second = Describe(ScoreFingeringGenerator.GenerateAlternatives(score, options));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateAlternatives_OnlyOnePathIsFeasible_ReturnsFewerThanRequested()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 0),
        ]);

        ScoreFingeringGenerationResult result = ScoreFingeringGenerator.GenerateAlternatives(
            score,
            new ScoreFingeringGenerationOptions(maximumAlternatives: 3));

        Assert.Single(result.Alternatives);
        Assert.Equal([1, 2, 3, 4, 5], FingeringNumbers(result.BestScore));
    }

    [Fact]
    public void GenerateAlternatives_WithLocksAndALimitedReach_EveryAlternativeHonoursBoth()
    {
        Score score = CreateScore(
        [
            CreateNote(NoteLetter.C, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
            CreateNote(NoteLetter.D, 4, Staff.Treble, beatOffset: 1),
            CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 2),
            CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 3),
        ]);
        ScoreFingeringProfile profile = CreateProfile(5);
        var options = new ScoreFingeringGenerationOptions(
            profile,
            [new ScoreFingeringLock(new ScoreFingeringNoteAddress(0, 2), 2)],
            maximumAlternatives: 3);

        ScoreFingeringGenerationResult result = ScoreFingeringGenerator.GenerateAlternatives(score, options);

        Assert.True(result.Alternatives.Count > 1);
        Assert.All(
            result.Alternatives,
            alternative =>
            {
                Assert.Equal(2, alternative.Score.Measures[0].Notes[2].Fingering?.Number);
                Assert.Empty(FingeringConstraintValidator.Validate(alternative.Score, profile));
            });
    }

    [Fact]
    public void GenerateAlternatives_FasterTempo_CostsMoreForTheSameHandMovement()
    {
        double slowCost = BestCost(beatsPerMinute: 30);
        double fastCost = BestCost(beatsPerMinute: 480);

        Assert.True(fastCost > slowCost, "hand movement should cost more when there is less time to make it");
    }

    private static double BestCost(double beatsPerMinute)
    {
        Score score = CreateScore(
            12,
            beatsPerMinute,
            [
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
                CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 1),
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 2),
                CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 3),
                CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 4),
            ]);

        return ScoreFingeringGenerator.GenerateAlternatives(score).Alternatives[0].Cost;
    }

    private static Score CreateMelody() => CreateScore(
    [
        CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 0),
        CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 1),
        CreateNote(NoteLetter.C, 5, Staff.Treble, beatOffset: 2),
        CreateNote(NoteLetter.B, 4, Staff.Treble, beatOffset: 3),
        CreateNote(NoteLetter.A, 4, Staff.Treble, beatOffset: 4),
        CreateNote(NoteLetter.G, 4, Staff.Treble, beatOffset: 5),
        CreateNote(NoteLetter.F, 4, Staff.Treble, beatOffset: 6),
        CreateNote(NoteLetter.E, 4, Staff.Treble, beatOffset: 7),
    ]);

    private static string[] Describe(ScoreFingeringGenerationResult result) => result.Alternatives
        .Select(alternative => $"{string.Join(',', FingeringNumbers(alternative.Score))}|{alternative.Cost:R}|{alternative.Explanation}")
        .ToArray();
}
