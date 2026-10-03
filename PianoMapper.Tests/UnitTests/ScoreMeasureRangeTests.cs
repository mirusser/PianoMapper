using PianoMapper.Music;
using PianoMapper.Web.Playback;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreMeasureRangeTests
{
    [Fact]
    public void Create_MultipleMeasures_ReturnsInclusiveRangeWithRebasedMeasureIndexes()
    {
        var score = CreateScore(measureCount: 4);

        var selected = ScoreMeasureRange.Create(score, firstMeasureIndex: 1, lastMeasureIndex: 2);

        Assert.Equal(2, selected.Measures.Count);
        Assert.Equal([0, 1], selected.Measures.SelectMany(measure => measure.Notes).Select(note => note.MeasureIndex));
        Assert.Equal([0, 1], selected.Measures.SelectMany(measure => measure.Rests).Select(rest => rest.MeasureIndex));
        Assert.Equal([NoteLetter.D, NoteLetter.E], selected.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch.Letter));
        Assert.Equal(score.Title, selected.Title);
        Assert.Equal(score.TimeSignature, selected.TimeSignature);
        Assert.Equal(score.Tempo, selected.Tempo);
        Assert.Equal(score.KeyFifths, selected.KeyFifths);
    }

    [Fact]
    public void Create_SingleMeasure_ReturnsOneMeasureStartingAtZero()
    {
        var score = CreateScore(measureCount: 3);

        var selected = ScoreMeasureRange.Create(score, firstMeasureIndex: 2, lastMeasureIndex: 2);

        var measure = Assert.Single(selected.Measures);
        Assert.Equal(0, Assert.Single(measure.Notes).MeasureIndex);
        Assert.Equal(NoteLetter.E, Assert.Single(measure.Notes).Pitch.Letter);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    [InlineData(0, 3)]
    public void Create_InvalidRange_Throws(int firstMeasureIndex, int lastMeasureIndex)
    {
        var score = CreateScore(measureCount: 3);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScoreMeasureRange.Create(score, firstMeasureIndex, lastMeasureIndex));
    }

    [Fact]
    public void Create_RangeStartingAfterAKeyChange_OpensInTheKeyThatWasInEffect()
    {
        var score = CreateScore(measureCount: 5) with
        {
            Measures = CreateScore(measureCount: 5).Measures
                .Select((measure, index) => index switch
                {
                    1 => measure with { KeyFifths = -1 },
                    3 => measure with { KeyFifths = 4 },
                    _ => measure,
                })
                .ToArray(),
        };

        var selected = ScoreMeasureRange.Create(score, firstMeasureIndex: 2, lastMeasureIndex: 4);

        Assert.Equal(-1, selected.KeyFifths);
        Assert.Equal([null, 4, null], selected.Measures.Select(measure => measure.KeyFifths));
    }

    [Fact]
    public void Create_RangeStartingOnAKeyChange_OpensInThatKeyWithoutRepeatingTheChange()
    {
        var score = CreateScore(measureCount: 3);
        score = score with
        {
            Measures = [score.Measures[0], score.Measures[1] with { KeyFifths = -4 }, score.Measures[2]],
        };

        var selected = ScoreMeasureRange.Create(score, firstMeasureIndex: 1, lastMeasureIndex: 2);

        Assert.Equal(-4, selected.KeyFifths);
        Assert.All(selected.Measures, measure => Assert.Null(measure.KeyFifths));
    }

    private static Score CreateScore(int measureCount) =>
        new(
            "range",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            2,
            Enumerable.Range(0, measureCount)
                .Select(index => new ScoreMeasure(
                    [new ScoreNote(new Pitch((NoteLetter)index, 0, 4), new NoteValue(4), index, 0, Staff.Treble)],
                    [new ScoreRest(new NoteValue(4), index, 1, Staff.Bass)]))
                .ToArray());
}
