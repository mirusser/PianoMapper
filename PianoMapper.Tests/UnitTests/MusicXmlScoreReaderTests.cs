using System.IO.Compression;
using System.Text;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

public sealed class MusicXmlScoreReaderTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Theory]
    [InlineData("single-staff.musicxml")]
    [InlineData("musescore-export.musicxml")]
    [InlineData("dotted-double-accidental.musicxml")]
    [InlineData("grand-staff-demo.musicxml")]
    public void Read_Stream_ProducesSameScoreAsPath(string fixture)
    {
        var reader = new MusicXmlScoreReader();
        string path = Fixture(fixture);
        var pathScore = reader.Read(path);
        using var stream = File.OpenRead(path);

        var streamScore = reader.Read(stream, fixture);

        Assert.Equivalent(pathScore, streamScore, strict: true);
    }

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
    public void Read_CompressedMusicXml_PreservesStemAndPrimaryBeams()
    {
        const string containerXml = """
            <container>
              <rootfiles>
                <rootfile full-path="scores/humpty.xml" media-type="application/vnd.recordare.musicxml+xml" />
              </rootfiles>
            </container>
            """;
        const string scoreXml = """
            <score-partwise>
              <part-list><score-part id="P1"><part-name /></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>2</divisions>
                    <key><fifths>2</fifths><mode>major</mode></key>
                    <time><beats>6</beats><beat-type>8</beat-type></time>
                  </attributes>
                  <barline location="left"><bar-style>heavy-light</bar-style><repeat direction="forward" /></barline>
                  <note>
                    <pitch><step>D</step><octave>4</octave></pitch>
                    <duration>1</duration><voice>1</voice><type>eighth</type><stem>up</stem><beam number="1">begin</beam>
                  </note>
                </measure>
              </part>
            </score-partwise>
            """;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "META-INF/container.xml", containerXml);
            WriteEntry(archive, "scores/humpty.xml", scoreXml);
        }

        stream.Position = 0;
        var score = new MusicXmlScoreReader().Read(stream, "Humpty-Dumpty.mxl");

        Assert.Equal("Humpty-Dumpty", score.Title);
        Assert.Equal(2, score.KeyFifths);
        Assert.Equal(new TimeSignature(6, new NoteValue(8)), score.TimeSignature);
        var note = Assert.Single(Assert.Single(score.Measures).Notes);
        Assert.Equal(BeamState.Begin, note.BeamState);
        Assert.Equal(ScoreStemDirection.Up, note.StemDirection);
    }

    [Theory]
    [InlineData("<stem>down</stem>", ScoreStemDirection.Down)]
    [InlineData("", null)]
    public void Read_StemDirectionDownOrMissing_PreservesValue(string stemXml, ScoreStemDirection? expectedDirection)
    {
        var score = ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>{{stemXml}}
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(expectedDirection, note.StemDirection);
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("double", false)]
    [InlineData("none", true)]
    [InlineData("double", true)]
    public void Read_UnsupportedStemValue_ThrowsReadableError(string stemValue, bool isRest)
    {
        string noteKind = isRest
            ? "<rest />"
            : "<pitch><step>C</step><octave>4</octave></pitch>";
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes($$"""
            <note>
              {{noteKind}}
              <duration>1</duration><type>eighth</type><stem>{{stemValue}}</stem>
            </note>
            """));

        Assert.Contains("<stem>", exception.Message);
        Assert.Contains(stemValue, exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_InvalidStemValue_ThrowsReadableError(bool isRest)
    {
        string noteKind = isRest
            ? "<rest />"
            : "<pitch><step>C</step><octave>4</octave></pitch>";
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes($$"""
            <note>
              {{noteKind}}
              <duration>1</duration><type>eighth</type><stem>sideways</stem>
            </note>
            """));

        Assert.Contains("<stem>", exception.Message);
        Assert.Contains("sideways", exception.Message);
    }

    [Theory]
    [InlineData("<stem>up</stem>", ScoreStemDirection.Up)]
    [InlineData("", null)]
    public void Read_BeamGroupWithCompatibleStemDirections_PreservesValues(
        string endStemXml,
        ScoreStemDirection? expectedEndDirection)
    {
        var score = ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type><stem>up</stem><beam number="1">begin</beam>
            </note>
            <note>
              <pitch><step>D</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>{{endStemXml}}<beam number="1">end</beam>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(2, notes.Count);
        Assert.Equal(BeamState.Begin, notes[0].BeamState);
        Assert.Equal(ScoreStemDirection.Up, notes[0].StemDirection);
        Assert.Equal(BeamState.End, notes[1].BeamState);
        Assert.Equal(expectedEndDirection, notes[1].StemDirection);
    }

    [Fact]
    public void Read_BeamGroupWithConflictingStemDirections_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type><stem>up</stem><beam number="1">begin</beam>
            </note>
            <note>
              <pitch><step>D</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type><stem>down</stem><beam number="1">end</beam>
            </note>
            """));

        Assert.Contains("<stem>", exception.Message);
        Assert.Contains("up", exception.Message);
        Assert.Contains("down", exception.Message);
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

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }

    private static Score ReadNotes(string notesXml)
    {
        string scoreXml = $$"""
            <score-partwise>
              <part-list><score-part id="P1"><part-name /></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes><divisions>2</divisions></attributes>
                  {{notesXml}}
                </measure>
              </part>
            </score-partwise>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(scoreXml));
        return new MusicXmlScoreReader().Read(stream, "test.musicxml");
    }
}
