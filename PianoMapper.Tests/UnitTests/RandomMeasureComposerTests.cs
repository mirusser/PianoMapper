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
}
