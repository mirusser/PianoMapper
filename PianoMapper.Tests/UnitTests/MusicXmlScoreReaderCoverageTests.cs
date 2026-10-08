using System.Text;
using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// MusicXML coverage gaps from docs/research/musicxml-coverage.md. The seam is the reader's public
// Read/ReadWithWarnings surface: a spec-valid snippet either imports (with any ignored
// presentation-only constructs reported as warnings) or fails with a readable error.
public sealed partial class MusicXmlScoreReaderTests
{
    private const string CommonAttributes =
        "<attributes><divisions>2</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>";

    private static string QuarterNote(string step = "C", string extra = "", string notations = "") =>
        $"<note><pitch><step>{step}</step><octave>4</octave></pitch><duration>2</duration><type>quarter</type>{extra}" +
        (notations.Length == 0 ? string.Empty : $"<notations>{notations}</notations>") +
        "</note>";

    private static MusicXmlReadResult ReadMeasures(string measuresXml, string rootChildren = "")
    {
        string scoreXml = $"""
            <score-partwise version="4.0">
              {rootChildren}
              <part-list><score-part id="P1"><part-name /></score-part></part-list>
              <part id="P1">{measuresXml}</part>
            </score-partwise>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(scoreXml));
        return new MusicXmlScoreReader().ReadWithWarnings(stream, "test.musicxml");
    }

    private static MusicXmlReadResult ReadMeasure(string measureContent, string rootChildren = "") =>
        ReadMeasures($"""<measure number="1">{CommonAttributes}{measureContent}</measure>""", rootChildren);

    private static int WarningCount(MusicXmlReadResult result, string construct) =>
        result.Warnings.SingleOrDefault(warning => warning.Construct == construct)?.Count ?? 0;

    [Fact]
    public void ReadWithWarnings_PlainScore_ReportsNoWarnings()
    {
        var result = new MusicXmlScoreReader().ReadWithWarnings(Fixture("w3c/tutorial-hello-world.musicxml"));

        Assert.Empty(result.Warnings);
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void ReadWithWarnings_Score_EqualsRead()
    {
        var reader = new MusicXmlScoreReader();
        string path = Fixture("grand-staff-demo.musicxml");

        Assert.Equivalent(reader.Read(path), reader.ReadWithWarnings(path).Score, strict: true);
    }

    [Fact]
    public void ReadWithWarnings_IgnoredDirectionContent_CountsEachOccurrence()
    {
        var result = ReadMeasure("""
            <direction><direction-type><damp/></direction-type></direction>
            <direction><direction-type><damp/></direction-type></direction>
            <direction><direction-type><eyeglasses/></direction-type></direction>
            """ + QuarterNote());

        Assert.Equal(2, WarningCount(result, "damp"));
        Assert.Equal(1, WarningCount(result, "eyeglasses"));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_MeasureLevelSoundTempo_SetsScoreTempo()
    {
        var result = ReadMeasure("""<sound tempo="40"/>""" + QuarterNote());

        Assert.Equal(new Tempo(40), result.Score.Tempo);
    }

    [Fact]
    public void Read_SoundPlaybackHints_AreIgnoredAndReported()
    {
        var result = ReadMeasure(
            """
            <direction><direction-type><words>x</words></direction-type><sound dynamics="112" damper-pedal="yes"/></direction>
            <sound soft-pedal="yes"><midi-instrument id="P1-I1"><midi-program>1</midi-program></midi-instrument></sound>
            """ + QuarterNote());

        Assert.True(WarningCount(result, "sound playback hints") >= 1);
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_DirectionOffset_IsAccepted()
    {
        var result = ReadMeasure(
            """<direction><direction-type><words>x</words></direction-type><offset>1</offset></direction>""" +
            QuarterNote());

        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_MovementNumber_IsAccepted()
    {
        var result = ReadMeasure(
            QuarterNote(),
            "<movement-number>2</movement-number><movement-title>Second</movement-title>");

        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_OfficialChopinPrelude_ImportsTwoStaffPianoPiece()
    {
        var result = new MusicXmlScoreReader().ReadWithWarnings(Fixture("w3c/tutorial-chopin-prelude.musicxml"));

        Assert.Equal(-3, result.Score.KeyFifths);
        Assert.Equal(new TimeSignature(4, new NoteValue(4)), result.Score.TimeSignature);
        Assert.Equal(new Tempo(40), result.Score.Tempo);
        Assert.Equal(
            [Staff.Treble, Staff.Bass],
            result.Score.Measures.SelectMany(measure => measure.Notes).Select(note => note.Staff).Distinct().Order());
        Assert.Contains(
            result.Score.Measures.SelectMany(measure => measure.Notes),
            note => note.Beams.SequenceEqual(
            [
                new ScoreBeam(1, ScoreBeamKind.End),
                new ScoreBeam(2, ScoreBeamKind.BackwardHook),
            ]) && note.StemEndYInTenths == 60);
    }

    [Theory]
    [InlineData("<staff-details number=\"1\"><staff-lines>5</staff-lines></staff-details>", "staff-details")]
    [InlineData("<part-symbol>brace</part-symbol>", "part-symbol")]
    [InlineData("<measure-style><multiple-rest>4</multiple-rest></measure-style>", "multiple-rest")]
    [InlineData("<directive>Allegro</directive>", "directive")]
    [InlineData("<instruments>1</instruments>", "instruments")]
    public void Read_PresentationOnlyAttributes_AreAcceptedAndReported(string attributeContent, string construct)
    {
        var result = ReadMeasures($"""
            <measure number="1">
              <attributes><divisions>2</divisions>{attributeContent}</attributes>
              {QuarterNote()}
            </measure>
            """);

        Assert.Equal(1, WarningCount(result, construct));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Theory]
    [InlineData("<measure-style><measure-repeat type=\"start\">1</measure-repeat></measure-style>", "<measure-repeat>")]
    [InlineData("<measure-style><slash type=\"start\"/></measure-style>", "<slash>")]
    [InlineData("<transpose><chromatic>-2</chromatic></transpose>", "<transpose>")]
    public void Read_AttributesThatChangeWhatIsPlayed_ThrowReadableError(string attributeContent, string expected)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasures($"""
            <measure number="1">
              <attributes><divisions>2</divisions>{attributeContent}</attributes>
              {QuarterNote()}
            </measure>
            """));

        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void Read_LinkBookmarkAndGrouping_AreIgnoredWithoutWarning()
    {
        var result = ReadMeasure(
            """<grouping type="single" number="1"/><link xlink:href="x" xmlns:xlink="http://www.w3.org/1999/xlink"/><bookmark id="b"/>""" +
            QuarterNote());

        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("<notehead>x</notehead>", "notehead")]
    [InlineData("<notehead-text><display-text>x</display-text></notehead-text>", "notehead-text")]
    public void Read_NoteheadShape_IsAcceptedAndReported(string extra, string construct)
    {
        string note = $"""
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration><type>quarter</type>{extra}</note>
            """;

        var result = ReadMeasure(note);

        Assert.Equal(1, WarningCount(result, construct));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_InstrumentPlayAndEditorialNoteChildren_AreAcceptedWithoutWarning()
    {
        string note = """
            <note>
              <pitch><step>C</step><octave>4</octave></pitch><duration>2</duration>
              <instrument id="P1-I1"/><type>quarter</type><footnote>x</footnote><level>y</level>
              <play><mute>straight</mute></play>
            </note>
            """;

        var result = ReadMeasure(note);

        Assert.Empty(result.Warnings);
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_NonFingeringTechnicalMarks_AreAcceptedAndReported()
    {
        var result = ReadMeasure(QuarterNote(
            notations: "<technical><fingering>3</fingering><up-bow/><down-bow/></technical>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(3, note.Fingering?.Number);
        Assert.Equal(2, WarningCount(result, "technical"));
    }

    [Theory]
    [InlineData("3-2", 3)]
    [InlineData("1-2-3", 1)]
    public void Read_FingerChange_UsesFirstFingerAndReports(string text, int expectedFinger)
    {
        var result = ReadMeasure(QuarterNote(
            notations: $"<technical><fingering substitution=\"yes\">{text}</fingering></technical>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(expectedFinger, note.Fingering?.Number);
        Assert.Equal(1, WarningCount(result, "finger change"));
    }

    [Theory]
    [InlineData("6")]
    [InlineData("0")]
    [InlineData("x")]
    [InlineData("3-9")]
    public void Read_InvalidFingeringText_StillThrowsReadableError(string text)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadMeasure(QuarterNote(
            notations: $"<technical><fingering>{text}</fingering></technical>")));

        Assert.Contains("<fingering>", exception.Message);
    }

    [Fact]
    public void Read_WholeMeasureRestWithoutType_ImportsAsMeasureRest()
    {
        var result = ReadMeasures("""
            <measure number="1">
              <attributes><divisions>8</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
              <note><rest measure="yes"/><duration>32</duration><voice>1</voice></note>
            </measure>
            """);

        var rest = Assert.Single(Assert.Single(result.Score.Measures).Rests);
        Assert.True(rest.IsMeasureRest);
        Assert.Equal(new NoteValue(1), rest.NoteValue);
        Assert.Equal(0, rest.BeatOffset);
    }

    [Fact]
    public void Read_WholeMeasureRestTypedWholeInThreeFour_UsesTheMeasureLength()
    {
        var result = ReadMeasures("""
            <measure number="1">
              <attributes><divisions>8</divisions><time><beats>3</beats><beat-type>4</beat-type></time></attributes>
              <note><rest measure="yes"/><duration>24</duration><voice>1</voice><type>whole</type></note>
            </measure>
            """);

        var rest = Assert.Single(Assert.Single(result.Score.Measures).Rests);
        Assert.True(rest.IsMeasureRest);
        Assert.Equal(new NoteValue(2, dots: 1), rest.NoteValue);
    }

    [Fact]
    public void Read_MeasureRestLongerThanOneNoteValue_FallsBackToWholeRest()
    {
        var result = ReadMeasures("""
            <measure number="1">
              <attributes><divisions>8</divisions><time><beats>5</beats><beat-type>4</beat-type></time></attributes>
              <note><rest measure="yes"/><duration>40</duration><voice>1</voice></note>
            </measure>
            """);

        var rest = Assert.Single(Assert.Single(result.Score.Measures).Rests);
        Assert.True(rest.IsMeasureRest);
        Assert.Equal(new NoteValue(1), rest.NoteValue);
    }

    [Theory]
    [InlineData(8, 4, 0)]
    [InlineData(12, 4, 1)]
    [InlineData(4, 8, 0)]
    [InlineData(28, 2, 2)]
    public void Read_NoteWithoutType_DerivesTheValueFromDuration(int duration, int expectedDenominator, int expectedDots)
    {
        var result = ReadMeasures($"""
            <measure number="1">
              <attributes><divisions>8</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
              <note><pitch><step>C</step><octave>4</octave></pitch><duration>{duration}</duration><voice>1</voice></note>
            </measure>
            """);

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(new NoteValue(expectedDenominator, expectedDots), note.NoteValue);
    }

    [Fact]
    public void Read_NoteWithoutTypeInATuplet_DerivesBaseValueFromTimeModification()
    {
        var result = ReadMeasures("""
            <measure number="1">
              <attributes><divisions>3</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
              <note>
                <pitch><step>C</step><octave>4</octave></pitch><duration>1</duration><voice>1</voice>
                <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
              </note>
            </measure>
            """);

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(new NoteValue(8, 0, 3, 2), note.NoteValue);
    }

    [Fact]
    public void Read_RestWithoutType_DerivesTheValueFromDuration()
    {
        var result = ReadMeasure("<note><rest/><duration>4</duration><voice>1</voice></note>");

        var rest = Assert.Single(Assert.Single(result.Score.Measures).Rests);
        Assert.False(rest.IsMeasureRest);
        Assert.Equal(new NoteValue(2), rest.NoteValue);
    }

    [Fact]
    public void Read_NoteWithoutTypeAndNoSingleValueForItsDuration_ThrowsReadableError()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadMeasures("""
            <measure number="1">
              <attributes><divisions>2</divisions></attributes>
              <note><pitch><step>C</step><octave>4</octave></pitch><duration>10</duration><voice>1</voice></note>
            </measure>
            """));

        Assert.Contains("<type>", exception.Message);
    }

    [Fact]
    public void Read_HiddenRestAndNote_AreNotImportedAndReported()
    {
        var result = ReadMeasure(
            """<note print-object="no"><rest/><duration>2</duration><voice>2</voice><type>quarter</type></note>""" +
            """<note print-object="no"><pitch><step>E</step><octave>4</octave></pitch><duration>2</duration><type>quarter</type></note>""" +
            QuarterNote("G"));

        var measure = Assert.Single(result.Score.Measures);
        Assert.Empty(measure.Rests);
        var note = Assert.Single(measure.Notes);
        Assert.Equal(NoteLetter.G, note.Pitch.Letter);
        Assert.Equal(2, note.BeatOffset);
        Assert.Equal(1, WarningCount(result, "hidden rest"));
        Assert.Equal(1, WarningCount(result, "hidden note"));
    }
}
