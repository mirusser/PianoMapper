using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class ScoreTests
{
    [Fact]
    public void Flatten_FourFourMeasures_ReturnsAbsoluteOnsetsAndDurations()
    {
        var score = new Score(
            "Onsets",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            KeyFifths: 0,
            [
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(4), 0, 0, Staff.Treble)],
                    []),
                new ScoreMeasure(
                    [new ScoreNote(new Pitch(NoteLetter.D, 0, 4), new NoteValue(2), 1, 1, Staff.Treble)],
                    []),
            ]);

        var events = ScoreDerivation.Flatten(score);

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(0, first.OnsetBeats);
                Assert.Equal(1, first.DurationBeats);
            },
            second =>
            {
                Assert.Equal(5, second.OnsetBeats);
                Assert.Equal(2, second.DurationBeats);
            });
    }

    [Fact]
    public void Flatten_ThreeFourDottedNote_ReturnsMeterRelativeOnsetAndDuration()
    {
        var note = new ScoreNote(
            new Pitch(NoteLetter.E, 0, 4),
            new NoteValue(4, dots: 1),
            MeasureIndex: 1,
            BeatOffset: 0.5,
            Staff.Treble);
        var score = new Score(
            "Dotted",
            new TimeSignature(3, new NoteValue(4)),
            new Tempo(90),
            0,
            [new ScoreMeasure([], []), new ScoreMeasure([note], [])]);

        var scoreEvent = Assert.Single(ScoreDerivation.Flatten(score));

        Assert.Equal(3.5, scoreEvent.OnsetBeats);
        Assert.Equal(1.5, scoreEvent.DurationBeats);
    }

    [Fact]
    public void Flatten_TieAcrossBarline_MergesIntoOneSoundingEvent()
    {
        var pitch = new Pitch(NoteLetter.G, 0, 4);
        var score = new Score(
            "Tie",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure([new ScoreNote(pitch, new NoteValue(4), 0, 3, Staff.Treble, TiesToNext: true)], []),
                new ScoreMeasure([new ScoreNote(pitch, new NoteValue(4), 1, 0, Staff.Treble)], []),
            ]);

        var scoreEvent = Assert.Single(ScoreDerivation.Flatten(score));

        Assert.Equal(3, scoreEvent.OnsetBeats);
        Assert.Equal(2, scoreEvent.DurationBeats);
        Assert.Equal(
            [score.Measures[0].Notes[0], score.Measures[1].Notes[0]],
            scoreEvent.SourceNotes);
    }

    [Fact]
    public void Flatten_TwoNoteChord_PreservesSharedOnset()
    {
        var score = new Score(
            "Chord",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [
                        new ScoreNote(new Pitch(NoteLetter.C, 0, 4), new NoteValue(2), 0, 1, Staff.Treble),
                        new ScoreNote(new Pitch(NoteLetter.E, 0, 4), new NoteValue(2), 0, 1, Staff.Treble),
                    ],
                    []),
            ]);

        var events = ScoreDerivation.Flatten(score);

        Assert.Equal(2, events.Count);
        Assert.All(events, scoreEvent => Assert.Equal(1, scoreEvent.OnsetBeats));
    }

    [Fact]
    public void CreateSchedule_AnchorTempoChordsAndTie_ReturnsAbsoluteDueTimesAndDurations()
    {
        var pitchC = new Pitch(NoteLetter.C, 0, 4);
        var pitchE = new Pitch(NoteLetter.E, 0, 4);
        var pitchG = new Pitch(NoteLetter.G, 0, 4);
        var score = new Score(
            "Playback",
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            0,
            [
                new ScoreMeasure(
                    [
                        new ScoreNote(pitchC, new NoteValue(4), 0, 0, Staff.Treble),
                        new ScoreNote(pitchE, new NoteValue(4), 0, 0, Staff.Treble),
                        new ScoreNote(pitchG, new NoteValue(4), 0, 3, Staff.Treble, TiesToNext: true),
                    ],
                    []),
                new ScoreMeasure([new ScoreNote(pitchG, new NoteValue(4), 1, 0, Staff.Treble)], []),
            ]);
        var anchor = TimeSpan.FromSeconds(5);

        var schedule = ScorePlayback.CreateSchedule(score, anchor);

        Assert.Equal(3, schedule.Count);
        Assert.Equal(2, schedule.Count(item => item.DueTime == anchor));
        var tied = Assert.Single(schedule, item => item.Event.Pitch == pitchG);
        Assert.Equal(TimeSpan.FromSeconds(6.5), tied.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(1), tied.Duration);
    }
}
