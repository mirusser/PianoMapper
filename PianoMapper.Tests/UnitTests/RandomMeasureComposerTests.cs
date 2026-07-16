using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class RandomMeasureComposerTests
{
    [Fact]
    public void Compose_FixedMeterAndTempo_FillsMeasureWithTypedNoteValues()
    {
        Pitch[] palette = [new Pitch(NoteLetter.A, 0, 4)];

        var measure = RandomMeasureComposer.Compose(
            palette,
            minNumerator: 4,
            maxNumerator: 4,
            minBeatsPerMinute: 90,
            maxBeatsPerMinute: 90,
            new Random(42));

        Assert.Equal(new TimeSignature(4, new NoteValue(4)), measure.TimeSignature);
        Assert.Equal(new Tempo(90), measure.Tempo);
        Assert.Equal(4, measure.Events.Sum(note => note.Beats));
        Assert.All(measure.Events, note => Assert.Contains(note.NoteValue.Denominator, new[] { 1, 2, 4, 8, 16 }));
    }

    [Fact]
    public void Compose_SelectedTiming_UsesBeatNoteAndTempo()
    {
        Pitch[] palette = [new Pitch(NoteLetter.A, 0, 4)];
        var timeSignature = new TimeSignature(6, new NoteValue(8));
        var tempo = new Tempo(90);

        RandomMeasure measure = RandomMeasureComposer.Compose(palette, timeSignature, tempo, new Random(42));

        Assert.Equal(timeSignature, measure.TimeSignature);
        Assert.Equal(tempo, measure.Tempo);
        Assert.Equal(6, measure.Events.Sum(note => note.Beats));
    }
}
