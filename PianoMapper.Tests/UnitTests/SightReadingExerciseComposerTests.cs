using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingExerciseComposerTests
{
    [Fact]
    public void Compose_StarterTrebleExercise_CreatesBalancedQuarterNoteScore()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingDifficulty.Starter,
            MeasureCount: 2);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(42));

        Assert.Equal("Treble starter note reading", score.Title);
        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(new Tempo(120), score.Tempo);
        Assert.Equal(0, score.KeyFifths);
        Assert.Equal(2, score.Measures.Count);

        ScoreNote[] notes = score.Measures.SelectMany(measure => measure.Notes).ToArray();
        Assert.Equal(8, notes.Length);
        Assert.All(notes, note =>
        {
            Assert.Equal(new NoteValue(4), note.NoteValue);
            Assert.Equal(Staff.Treble, note.Staff);
            Assert.Equal(0, note.Pitch.Alter);
            Assert.InRange(note.Pitch.DiatonicIndex, new Pitch(NoteLetter.C, 0, 4).DiatonicIndex,
                new Pitch(NoteLetter.G, 0, 4).DiatonicIndex);
        });

        for (int measureIndex = 0; measureIndex < score.Measures.Count; measureIndex++)
        {
            Assert.Equal([0d, 1d, 2d, 3d], score.Measures[measureIndex].Notes.Select(note => note.BeatOffset));
            Assert.All(score.Measures[measureIndex].Notes, note => Assert.Equal(measureIndex, note.MeasureIndex));
            Assert.Empty(score.Measures[measureIndex].Rests);
        }

        int[] occurrenceCounts = notes
            .GroupBy(note => note.Pitch)
            .Select(group => group.Count())
            .ToArray();
        Assert.Equal(5, occurrenceCounts.Length);
        Assert.InRange(occurrenceCounts.Max() - occurrenceCounts.Min(), 0, 1);
    }

    [Fact]
    public void Compose_SameSeed_ReturnsSamePitchSequence()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingDifficulty.OneOctave,
            MeasureCount: 4);

        Score first = SightReadingExerciseComposer.Compose(options, new Random(8675309));
        Score second = SightReadingExerciseComposer.Compose(options, new Random(8675309));

        Assert.Equal(
            first.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch),
            second.Measures.SelectMany(measure => measure.Notes).Select(note => note.Pitch));
    }

    [Fact]
    public void Compose_MultiplePitchPalette_AvoidsAdjacentRepetitions()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Bass,
            SightReadingDifficulty.Starter,
            MeasureCount: 8);

        Score score = SightReadingExerciseComposer.Compose(options, new Random(7));
        Pitch[] pitches = score.Measures
            .SelectMany(measure => measure.Notes)
            .Select(note => note.Pitch)
            .ToArray();

        Assert.All(
            pitches.Zip(pitches.Skip(1)),
            pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public void Compose_NonPositiveMeasureCount_Throws()
    {
        var options = new SightReadingExerciseOptions(
            Staff.Treble,
            SightReadingDifficulty.Starter,
            MeasureCount: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SightReadingExerciseComposer.Compose(options, new Random(42)));
    }
}
