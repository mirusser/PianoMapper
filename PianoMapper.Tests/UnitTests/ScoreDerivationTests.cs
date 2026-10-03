using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreDerivationTests
{
    private static readonly Pitch C4 = new(NoteLetter.C, 0, 4);
    private static readonly Pitch E4 = new(NoteLetter.E, 0, 4);

    [Fact]
    public void GroupByOnset_EventsOnTheSameBeat_FormOneChordGroup()
    {
        ScoreEvent[] events =
        [
            new(E4, 1, 1, Staff.Treble, []),
            new(C4, 0, 1, Staff.Treble, []),
            new(C4, 1, 1, Staff.Bass, []),
        ];

        IReadOnlyList<IReadOnlyList<ScoreEvent>> groups = ScoreDerivation.GroupByOnset(events);

        Assert.Equal(2, groups.Count);
        Assert.Equal([0.0], groups[0].Select(scoreEvent => scoreEvent.OnsetBeats));
        Assert.Equal([1.0, 1.0], groups[1].Select(scoreEvent => scoreEvent.OnsetBeats));
    }

    [Theory]
    [InlineData(1e-9, 1)]
    [InlineData(2e-9, 2)]
    public void GroupByOnset_OnsetsAroundTheBeatTolerance_JoinOrSplit(double secondOnset, int expectedGroupCount)
    {
        ScoreEvent[] events =
        [
            new(C4, 0, 1, Staff.Treble, []),
            new(E4, secondOnset, 1, Staff.Treble, []),
        ];

        Assert.Equal(expectedGroupCount, ScoreDerivation.GroupByOnset(events).Count);
    }

    [Fact]
    public void GroupByOnset_NoEvents_ReturnsNoGroups()
    {
        Assert.Empty(ScoreDerivation.GroupByOnset([]));
    }
}
