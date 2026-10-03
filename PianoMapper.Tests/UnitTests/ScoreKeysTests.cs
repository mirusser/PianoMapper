using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreKeysTests
{
    [Fact]
    public void GetKeyFifthsByMeasure_KeyChanges_CarriesEachKeyForwardUntilTheNextChange()
    {
        var score = new Score(
            "Keys",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            1,
            [
                new ScoreMeasure([], []),
                new ScoreMeasure([], [], KeyFifths: -2),
                new ScoreMeasure([], []),
                new ScoreMeasure([], [], KeyFifths: 0),
            ]);

        Assert.Equal([1, -2, -2, 0], ScoreKeys.GetKeyFifthsByMeasure(score));
    }

    [Fact]
    public void GetKeyFifthsByMeasure_NoChanges_RepeatsTheScoreKey()
    {
        var score = new Score(
            "Keys",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(100),
            3,
            [new ScoreMeasure([], []), new ScoreMeasure([], [])]);

        Assert.Equal([3, 3], ScoreKeys.GetKeyFifthsByMeasure(score));
    }
}
