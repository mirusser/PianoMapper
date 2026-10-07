using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MetronomeAccentPatternTests
{
    [Theory]
    [InlineData(4, 4, new[] { 0 })]
    [InlineData(6, 8, new[] { 0, 3 })]
    [InlineData(9, 8, new[] { 0, 3, 6 })]
    public void Create_DefaultGrouping_UsesTheMetersNaturalAccentStarts(
        int numerator,
        int denominator,
        int[] expectedGroupStarts)
    {
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(120),
            new TimeSignature(numerator, new NoteValue(denominator)));

        MetronomeAccentPattern pattern = MetronomeAccentPattern.Create(grid);

        Assert.Equal(expectedGroupStarts, pattern.GroupStartBeatIndices);
    }

    [Theory]
    [InlineData(5, new[] { 2, 3 }, new[] { 0, 2 })]
    [InlineData(5, new[] { 3, 2 }, new[] { 0, 3 })]
    [InlineData(7, new[] { 2, 2, 3 }, new[] { 0, 2, 4 })]
    public void Create_CustomGrouping_AccentsTheStartOfEachDeclaredGroup(
        int numerator,
        int[] groupLengths,
        int[] expectedGroupStarts)
    {
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(120),
            new TimeSignature(numerator, new NoteValue(8)));

        MetronomeAccentPattern pattern = MetronomeAccentPattern.Create(grid, groupLengths);

        Assert.Equal(expectedGroupStarts, pattern.GroupStartBeatIndices);
    }

    [Fact]
    public void Create_GroupingThatDoesNotFillTheMeasure_Throws()
    {
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(120),
            new TimeSignature(5, new NoteValue(8)));

        Assert.Throws<ArgumentException>(() => MetronomeAccentPattern.Create(grid, [2, 2]));
    }
}
