using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreTimingTests
{
    [Fact]
    public void Apply_ChangedBeatNote_RebarsEventsWithoutChangingMusicalPosition()
    {
        var source = new Score(
            "Retimed score",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [],
                    [new ScoreRest(new NoteValue(4), 0, 2, Staff.Bass)]),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 1, 1, Staff.Treble)],
                    []),
            ]);
        var selectedTimeSignature = new TimeSignature(6, new NoteValue(8));

        Score result = ScoreTiming.Apply(source, selectedTimeSignature, new Tempo(90));

        Assert.Equal(selectedTimeSignature, result.TimeSignature);
        Assert.Equal(new Tempo(90), result.Tempo);
        Assert.Equal(3, result.Measures.Count);
        ScoreRest rest = Assert.Single(result.Measures[0].Rests);
        Assert.Equal(4, rest.BeatOffset);
        ScoreNote note = Assert.Single(result.Measures[1].Notes);
        Assert.Equal(4, note.BeatOffset);
        Assert.Equal(10, ScoreDerivation.GetOnsetBeats(note, result.TimeSignature));
    }

    [Fact]
    public void Apply_TempoOnly_PreservesMeasures()
    {
        var measures = new[] { new ScoreMeasure([], []) };
        var source = new Score(
            "Tempo change",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            measures);

        Score result = ScoreTiming.Apply(source, source.TimeSignature, new Tempo(80));

        Assert.Equal(new Tempo(80), result.Tempo);
        Assert.Same(measures, result.Measures);
    }
}
