using System.IO.Compression;
using System.Text;
using PianoMapper.Music;
using PianoMapper.Practice;

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
    public void Read_IvanovskayaTranscription_ReturnsCompleteScore()
    {
        var reader = new MusicXmlScoreReader();

        var score = reader.Read(Fixture("mia_sebastians_theme_ivanovskaya_transcription.musicxml"));

        Assert.Equal(new TimeSignature(3, new NoteValue(4)), score.TimeSignature);
        Assert.Equal(3, score.KeyFifths);
        Assert.Equal(24, score.Measures.Count);
        Assert.Equal(150, score.Measures.Sum(measure => measure.Notes.Count));
        Assert.Equal(3, score.Measures.Sum(measure => measure.Rests.Count));
        Assert.Equal(
            4,
            score.Measures.SelectMany(measure => measure.Notes)
                .Count(note => note.Accidental == ScoreAccidental.Sharp));
        Assert.Equal(
            2,
            score.Measures.SelectMany(measure => measure.Notes)
                .Count(note => note.Fermata == ScoreFermata.Upright));
        Assert.Equal(new Pitch(NoteLetter.C, 1, 4), score.Measures[0].Notes[0].Pitch);
        Assert.Equal(new Pitch(NoteLetter.G, 1, 5), score.Measures[10].Notes[0].Pitch);

        var printedBSharp = Assert.Single(
            score.Measures[14].Notes,
            note => note.Pitch == new Pitch(NoteLetter.B, 1, 2));
        Assert.Equal(ScoreAccidental.Sharp, printedBSharp.Accidental);
        Assert.Equal(new Pitch(NoteLetter.C, 1, 4), score.Measures[23].Notes[0].Pitch);
    }

    [Fact]
    public void Read_AccidentalAndInvertedFermata_PreservesNotation()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>B</step><alter>-1</alter><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type><accidental>flat</accidental>
              <notations><fermata type="inverted" /></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(ScoreAccidental.Flat, note.Accidental);
        Assert.Equal(ScoreFermata.Inverted, note.Fermata);
    }

    [Theory]
    [InlineData("staccato", ScoreArticulation.Staccato)]
    [InlineData("tenuto", ScoreArticulation.Tenuto)]
    [InlineData("accent", ScoreArticulation.Accent)]
    [InlineData("staccatissimo", ScoreArticulation.Staccatissimo)]
    public void Read_SupportedArticulation_ImportsMatchingEnumValue(string element, ScoreArticulation expected)
    {
        var score = ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><articulations><{{element}} /></articulations></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(expected, note.Articulation);
    }

    [Fact]
    public void Read_UnsupportedArticulation_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><articulations><strong-accent /></articulations></notations>
            </note>
            """));

        Assert.Contains("<strong-accent>", exception.Message);
    }

    [Fact]
    public void Read_TrillMarkOrnament_ImportsTrillMark()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><ornaments><trill-mark /></ornaments></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(ScoreOrnament.TrillMark, note.Ornament);
    }

    [Fact]
    public void Read_UnsupportedOrnament_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><ornaments><mordent /></ornaments></notations>
            </note>
            """));

        Assert.Contains("<mordent>", exception.Message);
    }

    [Fact]
    public void Read_AccidentalMark_ImportsMatchingAccidentalSeparatelyFromPrintedAccidental()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><accidental-mark>sharp</accidental-mark></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(ScoreAccidental.Sharp, note.AccidentalMark);
        Assert.Null(note.Accidental);
    }

    [Fact]
    public void Read_InvalidAccidentalMarkValue_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><accidental-mark>quarter-sharp</accidental-mark></notations>
            </note>
            """));

        Assert.Contains("<accidental-mark>", exception.Message);
    }

    [Fact]
    public void Read_SlurStartAndStop_ImportsMatchingPairingData()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="start" number="1" /></notations>
            </note>
            <note>
              <pitch><step>D</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="stop" number="1" /></notations>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(new ScoreSlur(IsStart: true, Number: 1), notes[0].Slur);
        Assert.Equal(new ScoreSlur(IsStart: false, Number: 1), notes[1].Slur);
    }

    [Fact]
    public void Read_OverlappingSlursWithDifferentNumbers_ImportEachIndependently()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="start" number="1" /></notations>
            </note>
            <note>
              <pitch><step>D</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="start" number="2" /></notations>
            </note>
            <note>
              <pitch><step>E</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="stop" number="1" /></notations>
            </note>
            <note>
              <pitch><step>F</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="stop" number="2" /></notations>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(new ScoreSlur(true, 1), notes[0].Slur);
        Assert.Equal(new ScoreSlur(true, 2), notes[1].Slur);
        Assert.Equal(new ScoreSlur(false, 1), notes[2].Slur);
        Assert.Equal(new ScoreSlur(false, 2), notes[3].Slur);
    }

    [Fact]
    public void Read_SlurWithoutNumberAttribute_DefaultsToNumberOne()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="start" /></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(new ScoreSlur(true, 1), note.Slur);
    }

    [Fact]
    public void Read_InvalidSlurType_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slur type="continue" number="1" /></notations>
            </note>
            """));

        Assert.Contains("<slur>", exception.Message);
    }

    [Fact]
    public void Read_Arpeggiate_ImportsArpeggiateMark()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><arpeggiate /></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(ScoreArpeggio.Arpeggiate, note.Arpeggio);
    }

    [Fact]
    public void Read_NonArpeggiate_ImportsNonArpeggiateMark()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><non-arpeggiate type="bottom" /></notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(ScoreArpeggio.NonArpeggiate, note.Arpeggio);
    }

    [Theory]
    [InlineData("glissando", ScoreGlissandoKind.Glissando)]
    [InlineData("slide", ScoreGlissandoKind.Slide)]
    public void Read_GlissandoOrSlideStartAndStop_ImportsMatchingPairingDataAndKind(
        string element,
        ScoreGlissandoKind expectedKind)
    {
        var score = ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><{{element}} type="start" number="1" /></notations>
            </note>
            <note>
              <pitch><step>G</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><{{element}} type="stop" number="1" /></notations>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(new ScoreGlissando(true, 1, expectedKind), notes[0].Glissando);
        Assert.Equal(new ScoreGlissando(false, 1, expectedKind), notes[1].Glissando);
    }

    [Fact]
    public void Read_InvalidGlissandoType_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><glissando type="continue" number="1" /></notations>
            </note>
            """));

        Assert.Contains("<glissando>", exception.Message);
    }

    [Fact]
    public void Read_InvalidSlideType_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><slide type="continue" number="1" /></notations>
            </note>
            """));

        Assert.Contains("<slide>", exception.Message);
    }

    [Fact]
    public void Read_UnsupportedSemanticElement_ThrowsMessageNamingElement()
    {
        var reader = new MusicXmlScoreReader();

        var exception = Assert.Throws<NotSupportedException>(() => reader.Read(Fixture("unsupported-grace.musicxml")));

        Assert.Contains("<grace>", exception.Message);
    }

    [Theory]
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
    public void Read_EighthNoteTriplet_PreservesRatioAndFollowingBeatOffset()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("eighth-note-triplet.musicxml"));

        var notes = Assert.Single(score.Measures).Notes;
        Assert.Collection(
            notes,
            note => Assert.Equal(new NoteValue(4), note.NoteValue),
            note =>
            {
                Assert.Equal(new NoteValue(8, tupletActualNotes: 3, tupletNormalNotes: 2), note.NoteValue);
                Assert.Equal(1, note.BeatOffset);
            },
            note => Assert.Equal(4.0 / 3.0, note.BeatOffset, 6),
            note => Assert.Equal(5.0 / 3.0, note.BeatOffset, 6),
            note =>
            {
                Assert.Equal(new NoteValue(4), note.NoteValue);
                Assert.Equal(2, note.BeatOffset);
            },
            note => Assert.Equal(3, note.BeatOffset));
    }

    [Fact]
    public void Read_TimeModificationWithoutTupletMarker_StillImports()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>quarter</type>
              <time-modification><actual-notes>2</actual-notes><normal-notes>1</normal-notes></time-modification>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(new NoteValue(4, tupletActualNotes: 2, tupletNormalNotes: 1), note.NoteValue);
    }

    [Theory]
    [InlineData("<actual-notes>3</actual-notes>", "<normal-notes>")]
    [InlineData("<normal-notes>2</normal-notes>", "<actual-notes>")]
    [InlineData("<actual-notes>0</actual-notes><normal-notes>2</normal-notes>", "<actual-notes>")]
    [InlineData("<actual-notes>3</actual-notes><normal-notes>0</normal-notes>", "<normal-notes>")]
    public void Read_MalformedTimeModification_ThrowsMessageNamingElement(
        string timeModificationChildren,
        string expectedElement)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>quarter</type>
              <time-modification>{{timeModificationChildren}}</time-modification>
            </note>
            """));

        Assert.Contains(expectedElement, exception.Message);
    }

    [Fact]
    public void Read_OtherNotation_IsAcceptedButNeverRendered()
    {
        // <other-notation> is MusicXML's arbitrary vendor-extension escape hatch (a `type`
        // attribute plus free text, no fixed visual meaning) — parsed for validity so a malformed
        // one still errors, but deliberately never given a rendered glyph. See CONTEXT.md/README.md
        // for the documented decision.
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>2</duration><type>quarter</type>
              <notations><other-notation type="single">pedal mark</other-notation></notations>
            </note>
            """);

        Assert.Single(Assert.Single(score.Measures).Notes);
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
        Assert.Equal(new Tempo(240), score.Tempo);
        var note = Assert.Single(Assert.Single(score.Measures).Notes);
        Assert.Equal(BeamState.Begin, note.BeamState);
        Assert.Equal(ScoreStemDirection.Up, note.StemDirection);
    }

    [Fact]
    public void Read_SixEightSoundTempo_ConvertsQuarterNoteRateToEighthNoteBeatRate()
    {
        const string scoreXml = """
            <score-partwise>
              <part-list><score-part id="P1"><part-name /></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>2</divisions>
                    <time><beats>6</beats><beat-type>8</beat-type></time>
                  </attributes>
                  <direction><sound tempo="90" /></direction>
                  <note>
                    <pitch><step>C</step><octave>4</octave></pitch>
                    <duration>1</duration><type>eighth</type>
                  </note>
                </measure>
              </part>
            </score-partwise>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(scoreXml));

        var score = new MusicXmlScoreReader().Read(stream, "six-eight.musicxml");

        Assert.Equal(new Tempo(180), score.Tempo);
    }

    [Fact]
    public void Read_OctaveShift8VaFixture_PreservesSoundingPitchInsideSpan()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("octave-shift-8va.musicxml"));

        var notes = Assert.Single(score.Measures).Notes;
        Assert.Collection(
            notes,
            note => Assert.Equal((new Pitch(NoteLetter.C, 0, 4), 0), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.D, 0, 5), 1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.E, 0, 5), 1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.F, 0, 5), 1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.G, 0, 4), 0), (note.Pitch, note.SoundingOctavesAboveNotated)));
    }

    [Fact]
    public void Read_OctaveShift8VbFixture_PreservesLowerSoundingPitchInsideSpan()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("octave-shift-8vb.musicxml"));

        var notes = Assert.Single(score.Measures).Notes;
        Assert.Collection(
            notes,
            note => Assert.Equal((new Pitch(NoteLetter.C, 0, 4), 0), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.B, 0, 2), -1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.A, 0, 2), -1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.G, 0, 2), -1), (note.Pitch, note.SoundingOctavesAboveNotated)),
            note => Assert.Equal((new Pitch(NoteLetter.F, 0, 3), 0), (note.Pitch, note.SoundingOctavesAboveNotated)));
    }

    [Theory]
    [InlineData("down", 15, 2, 6)]
    [InlineData("down", 22, 3, 7)]
    [InlineData("up", 15, -2, 2)]
    [InlineData("up", 22, -3, 1)]
    public void Read_OctaveShiftSize_MapsOffsetWithoutChangingSoundingPitch(
        string type,
        int size,
        int expectedOffset,
        int soundingOctave)
    {
        var score = ReadNotes($$"""
            <direction>
              <direction-type><octave-shift type="{{type}}" size="{{size}}" number="2" /></direction-type>
            </direction>
            <note>
              <pitch><step>C</step><octave>{{soundingOctave}}</octave></pitch>
              <duration>2</duration><type>quarter</type>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.Equal(expectedOffset, note.SoundingOctavesAboveNotated);
        Assert.Equal(soundingOctave, note.Pitch.Octave);
    }

    [Fact]
    public void Read_UnsupportedOctaveShiftSize_ThrowsMessageNamingAttribute()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <direction>
              <direction-type><octave-shift type="down" size="10" /></direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift@size>", exception.Message);
    }

    [Fact]
    public void Read_UnsupportedOctaveShiftContinue_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <direction>
              <direction-type><octave-shift type="continue" size="8" /></direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift>", exception.Message);
    }

    [Fact]
    public void Read_OctaveShiftStopWithoutStart_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes("""
            <direction>
              <direction-type><octave-shift type="stop" number="1" /></direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift>", exception.Message);
    }

    [Fact]
    public void Read_OctaveShiftStopWithMismatchedNumber_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes("""
            <direction>
              <direction-type><octave-shift type="down" size="8" number="1" /></direction-type>
            </direction>
            <direction>
              <direction-type><octave-shift type="stop" number="2" /></direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift>", exception.Message);
    }

    [Fact]
    public void Read_SecondOctaveShiftStartWhileActive_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <direction>
              <direction-type><octave-shift type="down" size="8" number="1" /></direction-type>
            </direction>
            <direction>
              <direction-type><octave-shift type="up" size="8" number="2" /></direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift>", exception.Message);
    }

    [Fact]
    public void Read_MultipleOctaveShiftsInOneDirection_ThrowsMessageNamingElement()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadNotes("""
            <direction>
              <direction-type>
                <octave-shift type="down" size="8" number="1" />
                <octave-shift type="up" size="8" number="2" />
              </direction-type>
            </direction>
            """));

        Assert.Contains("<octave-shift>", exception.Message);
    }

    [Fact]
    public void Read_OctaveShiftedNote_GradesAgainstSoundingPitch()
    {
        var score = new MusicXmlScoreReader().Read(Fixture("octave-shift-8va.musicxml"));
        var expected = ScoreDerivation.Flatten(score)
            .First(scoreEvent => scoreEvent.SourceNotes[0].SoundingOctavesAboveNotated == 1);
        TimeSpan start = MusicalTime.BeatsToDuration(expected.OnsetBeats, score.Tempo);
        TimeSpan duration = MusicalTime.BeatsToDuration(expected.DurationBeats, score.Tempo);
        var performed = new PerformedNote
        {
            Pitch = expected.Pitch,
            StartTime = start,
            ReleaseTime = start + duration,
        };

        var result = Grader.Grade(
            [expected],
            score.Tempo,
            [performed],
            TimeSpan.Zero,
            start + duration);

        Assert.Equal(Verdict.Correct, Assert.Single(result.Events).Verdict);
        Assert.Equal(1, expected.SourceNotes[0].SoundingOctavesAboveNotated);
        Assert.Equal(new Pitch(NoteLetter.D, 0, 5), expected.Pitch);
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
    [InlineData("above", ScoreFingeringPlacement.Above)]
    [InlineData("below", ScoreFingeringPlacement.Below)]
    [InlineData(null, null)]
    public void Read_Fingering_PreservesFingerNumberAndPlacement(
        string? placement,
        ScoreFingeringPlacement? expectedPlacement)
    {
        string placementAttribute = placement is null ? string.Empty : $" placement=\"{placement}\"";
        var score = ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations>
                <technical><fingering{{placementAttribute}}>3</fingering></technical>
              </notations>
            </note>
            """);

        var fingering = Assert.Single(Assert.Single(score.Measures).Notes).Fingering;

        Assert.Equal(new ScoreFingering(3, expectedPlacement), fingering);
    }

    [Fact]
    public void Read_FingeringAndTie_PreservesBothNotations()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type><tie type="start" />
              <notations>
                <tied type="start" />
                <technical><fingering placement="above">1</fingering></technical>
              </notations>
            </note>
            """);

        var note = Assert.Single(Assert.Single(score.Measures).Notes);

        Assert.True(note.TiesToNext);
        Assert.Equal(new ScoreFingering(1, ScoreFingeringPlacement.Above), note.Fingering);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("thumb")]
    public void Read_InvalidPianoFingering_ThrowsReadableError(string value)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadNotes($$"""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>1</duration><type>eighth</type>
              <notations><technical><fingering>{{value}}</fingering></technical></notations>
            </note>
            """));

        Assert.Contains("<fingering>", exception.Message);
        Assert.Contains(value, exception.Message);
    }

    [Theory]
    [InlineData("none", false, typeof(NotSupportedException))]
    [InlineData("double", false, typeof(NotSupportedException))]
    [InlineData("none", true, typeof(NotSupportedException))]
    [InlineData("double", true, typeof(NotSupportedException))]
    [InlineData("sideways", false, typeof(InvalidDataException))]
    [InlineData("sideways", true, typeof(InvalidDataException))]
    public void Read_BadStemValue_ThrowsReadableError(string stemValue, bool isRest, Type expectedExceptionType)
    {
        string noteKind = isRest
            ? "<rest />"
            : "<pitch><step>C</step><octave>4</octave></pitch>";
        var exception = Assert.Throws(expectedExceptionType, () => ReadNotes($$"""
            <note>
              {{noteKind}}
              <duration>1</duration><type>eighth</type><stem>{{stemValue}}</stem>
            </note>
            """));

        Assert.Contains("<stem>", exception.Message);
        Assert.Contains(stemValue, exception.Message);
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

    [Fact]
    public void Read_ChordNote_MarksSecondNoteAsChordContinuation()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>2</duration><type>quarter</type>
            </note>
            <note>
              <chord />
              <pitch><step>E</step><octave>4</octave></pitch>
              <duration>2</duration><type>quarter</type>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(2, notes.Count);
        Assert.False(notes[0].IsChordContinuation);
        Assert.True(notes[1].IsChordContinuation);
    }

    [Fact]
    public void Read_BackupInterleavedVoiceAtSameOnset_DoesNotMarkChordContinuation()
    {
        var score = ReadNotes("""
            <note>
              <pitch><step>C</step><octave>4</octave></pitch>
              <duration>2</duration><voice>1</voice><type>quarter</type>
            </note>
            <backup><duration>2</duration></backup>
            <note>
              <pitch><step>G</step><octave>4</octave></pitch>
              <duration>2</duration><voice>2</voice><type>quarter</type>
            </note>
            """);

        var notes = Assert.Single(score.Measures).Notes;

        Assert.Equal(2, notes.Count);
        Assert.Equal(0, notes[0].BeatOffset);
        Assert.Equal(0, notes[1].BeatOffset);
        Assert.False(notes[0].IsChordContinuation);
        Assert.False(notes[1].IsChordContinuation);
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
