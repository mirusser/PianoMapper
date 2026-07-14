using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MusicXmlScoreReaderTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Read_SingleStaffMelody_ReturnsDomainScoreWithPitchTimeAndRestData()
    {
        var reader = new MusicXmlScoreReader();

        var score = reader.Read(Fixture("single-staff.musicxml"));

        Assert.Equal(new TimeSignature(4, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(new Tempo(96), score.Tempo);
        Assert.Equal(0, score.KeyFifths);
        var measure = Assert.Single(score.Measures);
        Assert.Collection(
            measure.Notes,
            note =>
            {
                Assert.Equal(new Pitch(NoteLetter.C, 0, 4), note.Pitch);
                Assert.Equal(new NoteValue(4), note.NoteValue);
                Assert.Equal(0, note.BeatOffset);
            },
            note =>
            {
                Assert.Equal(new Pitch(NoteLetter.F, 1, 4), note.Pitch);
                Assert.Equal(new NoteValue(2), note.NoteValue);
                Assert.Equal(2, note.BeatOffset);
            });
        var rest = Assert.Single(measure.Rests);
        Assert.Equal(new NoteValue(4), rest.NoteValue);
        Assert.Equal(1, rest.BeatOffset);
    }

    [Fact]
    public void Read_TrimmedMuseScoreExport_IgnoresPresentationMetadata()
    {
        var reader = new MusicXmlScoreReader();

        var score = reader.Read(Fixture("musescore-export.musicxml"));

        Assert.Equal(new TimeSignature(3, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(new Tempo(72), score.Tempo);
        Assert.Single(Assert.Single(score.Measures).Notes);
    }

    [Fact]
    public void Read_UnsupportedSemanticElement_ThrowsMessageNamingElement()
    {
        var reader = new MusicXmlScoreReader();

        var exception = Assert.Throws<NotSupportedException>(() => reader.Read(Fixture("unsupported-grace.musicxml")));

        Assert.Contains("<grace>", exception.Message);
    }

    [Theory]
    [InlineData("unsupported-time-modification.musicxml", "<time-modification>")]
    [InlineData("unsupported-transpose.musicxml", "<transpose>")]
    [InlineData("unsupported-multipart.musicxml", "<part>")]
    [InlineData("unsupported-timewise.musicxml", "<score-timewise>")]
    [InlineData("unsupported-tempo-change.musicxml", "<sound>")]
    [InlineData("unsupported-time-change.musicxml", "<time>")]
    [InlineData("unsupported-repeat.musicxml", "<repeat>")]
    [InlineData("unsupported-direction-offset.musicxml", "<offset>")]
    [InlineData("unsupported-sound-navigation.musicxml", "<sound@dacapo>")]
    public void Read_UnsupportedScoreSemantics_ThrowsMessageNamingElement(string fixture, string expectedElement)
    {
        var reader = new MusicXmlScoreReader();

        var exception = Assert.Throws<NotSupportedException>(() => reader.Read(Fixture(fixture)));

        Assert.Contains(expectedElement, exception.Message);
    }

    [Fact]
    public void Read_DottedDoubleAccidental_PreservesSpellingAndRhythm()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("dotted-double-accidental.musicxml"));

        var measure = Assert.Single(score.Measures);
        var note = Assert.Single(measure.Notes);
        Assert.Equal(new Pitch(NoteLetter.C, 2, 4), note.Pitch);
        Assert.Equal(new NoteValue(4, 1), note.NoteValue);
        Assert.Equal(new NoteValue(4, 1), Assert.Single(measure.Rests).NoteValue);
    }

    [Fact]
    public void Read_MalformedXml_ThrowsReadableInvalidDataError()
    {
        var reader = new MusicXmlScoreReader();

        var exception = Assert.Throws<InvalidDataException>(() => reader.Read(Fixture("malformed.musicxml")));

        Assert.Contains("Could not parse MusicXML", exception.Message);
    }

    [Fact]
    public void Read_GrandStaffFixture_AssignsStaffsChordsAndCursorMovements()
    {
        var reader = new MusicXmlScoreReader();

        var score = reader.Read(Fixture("grand-staff-demo.musicxml"));

        var firstMeasure = score.Measures[0];
        Assert.Equal(4, firstMeasure.Notes.Count);
        Assert.Equal(2, firstMeasure.Notes.Count(note => note.Staff == Staff.Treble && note.BeatOffset == 0));
        Assert.Contains(firstMeasure.Notes, note => note.Pitch == new Pitch(NoteLetter.D, 0, 4) && note.BeatOffset == 3 && note.TiesToNext);
        Assert.Contains(firstMeasure.Notes, note => note.Pitch == new Pitch(NoteLetter.G, 0, 2) && note.Staff == Staff.Bass && note.BeatOffset == 0);

        var secondMeasure = score.Measures[1];
        Assert.Contains(secondMeasure.Notes, note => note.Pitch == new Pitch(NoteLetter.C, 0, 3) && note.Staff == Staff.Bass && note.BeatOffset == 0);
        Assert.Contains(secondMeasure.Notes, note => note.Pitch == new Pitch(NoteLetter.E, 0, 3) && note.Staff == Staff.Bass && note.BeatOffset == 2);
    }

    [Fact]
    public void Read_GrandStaffFixture_FlattenMergesTieAndPreservesChordOnset()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("grand-staff-demo.musicxml"));

        var events = ScoreDerivation.Flatten(score);

        var tiedD = Assert.Single(events, scoreEvent => scoreEvent.Pitch == new Pitch(NoteLetter.D, 0, 4));
        Assert.Equal(3, tiedD.OnsetBeats);
        Assert.Equal(2, tiedD.DurationBeats);
        Assert.Equal(2, events.Count(scoreEvent => scoreEvent.OnsetBeats == 0 && scoreEvent.Staff == Staff.Treble));
    }
}
