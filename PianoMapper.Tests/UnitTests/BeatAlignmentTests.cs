using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class BeatAlignmentTests
{
    [Theory]
    [InlineData(-60, (int)Verdict.Correct)]
    [InlineData(60, (int)Verdict.Correct)]
    [InlineData(-61, (int)Verdict.Early)]
    [InlineData(61, (int)Verdict.Late)]
    public void Classify_OnTimeBoundary_IsInclusive(int deviationMilliseconds, int expectedVerdictValue)
    {
        var grid = new MetronomeGrid(
            TimeSpan.FromSeconds(10),
            new Tempo(60),
            new TimeSignature(4, new NoteValue(4)));
        TimeSpan onset = grid.Anchor + TimeSpan.FromMilliseconds(deviationMilliseconds);

        var alignment = BeatAlignment.Classify(onset, grid, TimeSpan.FromMilliseconds(60));

        Assert.Equal((Verdict)expectedVerdictValue, alignment.Verdict);
        Assert.Equal(TimeSpan.FromMilliseconds(deviationMilliseconds), alignment.Deviation);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(-4, true)]
    public void Classify_BeatIndex_IdentifiesDownbeat(long beatIndex, bool expectedIsDownbeat)
    {
        var grid = new MetronomeGrid(
            TimeSpan.Zero,
            new Tempo(60),
            new TimeSignature(4, new NoteValue(4)));

        var alignment = BeatAlignment.Classify(
            grid.GetBeatTime(beatIndex),
            grid,
            TimeSpan.FromMilliseconds(60));

        Assert.Equal(beatIndex, alignment.BeatIndex);
        Assert.Equal(expectedIsDownbeat, alignment.IsDownbeat);
    }
}
