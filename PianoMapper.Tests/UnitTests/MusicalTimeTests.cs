using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MusicalTimeTests
{
    [Theory]
    [InlineData(4, 4, 1.0)]
    [InlineData(6, 8, 2.0)]
    public void GetBeats_QuarterNote_ReturnsMeterRelativeBeatCount(
        int numerator,
        int beatDenominator,
        double expectedBeats)
    {
        var noteValue = new NoteValue(4);
        var timeSignature = new TimeSignature(numerator, new NoteValue(beatDenominator));

        double beats = MusicalTime.GetBeats(noteValue, timeSignature);

        Assert.Equal(expectedBeats, beats, 6);
    }

    [Fact]
    public void ToDuration_WholeNoteAtNinetyBpm_ReturnsCurrentRhythmicDuration()
    {
        var noteValue = new NoteValue(1);
        var timeSignature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(90);

        var duration = MusicalTime.ToDuration(noteValue, timeSignature, tempo);

        Assert.Equal(2.667, duration.TotalSeconds, 3);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.5)]
    [InlineData(2, 1.75)]
    public void GetBeats_DottedQuarter_ReturnsExpandedBeatCount(int dots, double expectedBeats)
    {
        var noteValue = new NoteValue(4, dots);
        var timeSignature = new TimeSignature(3, new NoteValue(4));

        double beats = MusicalTime.GetBeats(noteValue, timeSignature);

        Assert.Equal(expectedBeats, beats, 6);
    }

    [Theory]
    [InlineData(0.5, 60)]
    [InlineData(3.0, 90)]
    [InlineData(12.5, 144)]
    public void BeatsAndDuration_ValidValues_RoundTrip(double beats, double beatsPerMinute)
    {
        var tempo = new Tempo(beatsPerMinute);

        var duration = MusicalTime.BeatsToDuration(beats, tempo);
        double actualBeats = MusicalTime.DurationToBeats(duration, tempo);

        Assert.Equal(beats, actualBeats, 6);
    }
}
