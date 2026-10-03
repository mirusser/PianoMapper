using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreTimingTests
{
    [Fact]
    public void Apply_ImplicitPickup_MapsFollowingEventsFromItsActualEnd()
    {
        var source = new Score(
            "Pickup",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([], [], LengthInBeats: 1),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(4), 1, 0, Staff.Treble)],
                    []),
            ]);

        Score result = ScoreTiming.Apply(source, new TimeSignature(2, new NoteValue(4)), new Tempo(120));

        ScoreNote note = Assert.Single(result.Measures[0].Notes);
        Assert.Equal(0, note.MeasureIndex);
        Assert.Equal(1, note.BeatOffset);
    }

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

    [Fact]
    public void Apply_ChangedBeatCount_KeepsAKeyChangeAtTheMeasureHoldingItsPosition()
    {
        var source = new Score(
            "Key change",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(1), 0, 0, Staff.Treble)], []),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(1), 1, 0, Staff.Treble)],
                    [],
                    KeyFifths: 2),
                new ScoreMeasure([new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(1), 2, 0, Staff.Treble)], []),
            ]);

        Score result = ScoreTiming.Apply(source, new TimeSignature(2, new NoteValue(4)), new Tempo(120));

        Assert.Equal(0, result.KeyFifths);
        Assert.Equal(2, result.Measures.Single(measure => measure.KeyFifths is not null).KeyFifths);
        Assert.Equal(
            NoteLetter.D,
            result.Measures.Single(measure => measure.KeyFifths is not null).Notes.Single().Pitch.Letter);
    }

    [Fact]
    public void Apply_ChangedBeatCount_MovesBarlineSignsToTheMeasuresStartingAndEndingTheSourceMeasure()
    {
        var forward = new ScoreBarline(Repeat: ScoreRepeatDirection.Forward);
        var backward = new ScoreBarline(Repeat: ScoreRepeatDirection.Backward);
        var source = new Score(
            "Repeat",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(1), 0, 0, Staff.Treble)], []),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(1), 1, 0, Staff.Treble)],
                    [],
                    LeftBarline: forward,
                    RightBarline: backward),
            ]);

        Score result = ScoreTiming.Apply(source, new TimeSignature(2, new NoteValue(4)), new Tempo(120));

        Assert.Equal(4, result.Measures.Count);
        Assert.Equal(forward, result.Measures[2].LeftBarline);
        Assert.Equal(backward, result.Measures[3].RightBarline);
        Assert.Null(result.Measures[0].LeftBarline);
        Assert.Null(result.Measures[1].RightBarline);
    }

    [Fact]
    public void Apply_ChangedBeatCount_MovesDirectionsToTheirMusicalPosition()
    {
        var source = new Score(
            "Directions",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([], []),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(1), 1, 0, Staff.Treble)],
                    [],
                    Directions: [new ScoreDirection(ScoreDirectionKind.Dynamics, 3, Staff.Treble, "p", IsBelow: true)]),
            ]);

        Score result = ScoreTiming.Apply(source, new TimeSignature(2, new NoteValue(4)), new Tempo(120));

        // Source beat 4 + 3 = 7, which is the second beat of the fourth 2/4 measure.
        var mark = Assert.Single(result.Measures[3].Directions!);
        Assert.Equal(1, mark.BeatOffset);
        Assert.Equal("p", mark.Text);
    }
}
