using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Several notations on one note, and the articulations and ornaments beyond the original few (see
// docs/research/musicxml-coverage.md, findings 1 and 3). Anything presentation-only that stays unshown is reported as a
// warning instead of failing the import.
public sealed partial class MusicXmlScoreReaderTests
{
    [Fact]
    public void Read_TwoArticulationsOnOneNote_ImportsBoth()
    {
        var result = ReadMeasure(QuarterNote(notations: "<articulations><staccato/><accent/></articulations>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(ScoreArticulation.Staccato | ScoreArticulation.Accent, note.Articulation);
    }

    [Fact]
    public void Read_TwoSeparateArticulationsElements_ImportsBoth()
    {
        var result = ReadMeasure(QuarterNote(
            notations: "<articulations><tenuto/></articulations><articulations><accent/></articulations>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(ScoreArticulation.Tenuto | ScoreArticulation.Accent, note.Articulation);
    }

    [Theory]
    [InlineData("strong-accent", ScoreArticulation.StrongAccent)]
    [InlineData("breath-mark", ScoreArticulation.BreathMark)]
    [InlineData("caesura", ScoreArticulation.Caesura)]
    [InlineData("detached-legato", ScoreArticulation.Staccato | ScoreArticulation.Tenuto)]
    [InlineData("spiccato", ScoreArticulation.Staccatissimo)]
    public void Read_MoreArticulations_ImportsMatchingFlags(string element, ScoreArticulation expected)
    {
        var result = ReadMeasure(QuarterNote(notations: $"<articulations><{element}/></articulations>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(expected, note.Articulation);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("soft-accent")]
    [InlineData("scoop")]
    [InlineData("plop")]
    [InlineData("doit")]
    [InlineData("falloff")]
    [InlineData("stress")]
    [InlineData("unstress")]
    [InlineData("other-articulation")]
    public void Read_ArticulationWithoutAGlyph_IsAcceptedAndReported(string element)
    {
        var result = ReadMeasure(QuarterNote(notations: $"<articulations><{element}/></articulations>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Null(note.Articulation);
        Assert.Equal(1, WarningCount(result, element));
    }

    [Fact]
    public void Read_TrillMarkWithWavyLine_ImportsTrillAndReportsTheLine()
    {
        var result = ReadMeasure(QuarterNote(
            notations: "<ornaments><trill-mark/><wavy-line type=\"start\"/></ornaments>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(ScoreOrnament.TrillMark, note.Ornament);
        Assert.Equal(1, WarningCount(result, "wavy-line"));
    }

    [Theory]
    [InlineData("turn", ScoreOrnament.Turn)]
    [InlineData("inverted-turn", ScoreOrnament.InvertedTurn)]
    [InlineData("mordent", ScoreOrnament.Mordent)]
    [InlineData("inverted-mordent", ScoreOrnament.InvertedMordent)]
    [InlineData("shake", ScoreOrnament.Shake)]
    public void Read_MoreOrnaments_ImportsMatchingFlags(string element, ScoreOrnament expected)
    {
        var result = ReadMeasure(QuarterNote(notations: $"<ornaments><{element}/></ornaments>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(expected, note.Ornament);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_TwoOrnamentsOnOneNote_ImportsBoth()
    {
        var result = ReadMeasure(QuarterNote(notations: "<ornaments><trill-mark/><turn/></ornaments>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(ScoreOrnament.TrillMark | ScoreOrnament.Turn, note.Ornament);
    }

    [Theory]
    [InlineData("delayed-turn")]
    [InlineData("vertical-turn")]
    [InlineData("schleifer")]
    [InlineData("tremolo")]
    [InlineData("haydn")]
    [InlineData("other-ornament")]
    public void Read_OrnamentWithoutAGlyph_IsAcceptedAndReported(string element)
    {
        var result = ReadMeasure(QuarterNote(notations: $"<ornaments><{element}/></ornaments>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Null(note.Ornament);
        Assert.Equal(1, WarningCount(result, element));
    }

    [Fact]
    public void Read_SlurStopAndStartOnOneNote_ImportsBothEnds()
    {
        var result = ReadMeasure(QuarterNote(
            notations: "<slur type=\"stop\" number=\"1\"/><slur type=\"start\" number=\"1\"/>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal(
            [new ScoreSlur(false, 1), new ScoreSlur(true, 1)],
            note.Slur!.Chain().Select(slur => slur with { Next = null }));
    }

    [Fact]
    public void Read_TwoSlurStartsOnOneNote_ImportsBothInDocumentOrder()
    {
        var result = ReadMeasure(QuarterNote(
            notations: "<slur type=\"start\" number=\"1\"/><slur type=\"start\" number=\"2\"/>"));

        var note = Assert.Single(Assert.Single(result.Score.Measures).Notes);
        Assert.Equal([1, 2], note.Slur!.Chain().Select(slur => slur.Number));
        Assert.All(note.Slur.Chain(), slur => Assert.True(slur.IsStart));
    }

    [Fact]
    public void Read_NoteWithoutSlur_HasNoSlurData()
    {
        var result = ReadMeasure(QuarterNote());

        Assert.Null(Assert.Single(Assert.Single(result.Score.Measures).Notes).Slur);
    }

    [Fact]
    public void Read_SlurContinue_IsAcceptedAndAddsNoSlurEnd()
    {
        var result = ReadMeasure(QuarterNote(notations: "<slur type=\"continue\" number=\"1\"/>"));

        Assert.Null(Assert.Single(Assert.Single(result.Score.Measures).Notes).Slur);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("let-ring")]
    public void Read_TiedContinueOrLetRing_IsAcceptedWithoutStartingATie(string type)
    {
        var result = ReadMeasure(QuarterNote(notations: $"<tied type=\"{type}\"/>"));

        Assert.False(Assert.Single(Assert.Single(result.Score.Measures).Notes).TiesToNext);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("32nd", 32)]
    [InlineData("64th", 64)]
    public void Read_ShortNoteTypes_ImportWithTheirDenominator(string type, int expectedDenominator)
    {
        // divisions 16 per quarter: a 32nd is 2 divisions, a 64th is 1.
        var result = ReadMeasures($"""
            <measure number="1">
              <attributes><divisions>16</divisions><time><beats>4</beats><beat-type>4</beat-type></time></attributes>
              <note><pitch><step>C</step><octave>4</octave></pitch><duration>{(expectedDenominator == 32 ? 2 : 1)}</duration><type>{type}</type></note>
              <note><pitch><step>D</step><octave>4</octave></pitch><duration>{(expectedDenominator == 32 ? 2 : 1)}</duration><type>{type}</type></note>
            </measure>
            """);

        var notes = Assert.Single(result.Score.Measures).Notes;
        Assert.All(notes, note => Assert.Equal(new NoteValue(expectedDenominator), note.NoteValue));
        Assert.Equal(expectedDenominator == 32 ? 0.125 : 0.0625, notes[1].BeatOffset);
    }

    [Fact]
    public void Read_ThirtySecondNoteWithoutType_DerivesTheValueFromItsDuration()
    {
        var result = ReadMeasures("""
            <measure number="1">
              <attributes><divisions>8</divisions></attributes>
              <note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration></note>
            </measure>
            """);

        Assert.Equal(new NoteValue(32), Assert.Single(Assert.Single(result.Score.Measures).Notes).NoteValue);
    }

    [Theory]
    [InlineData("128th")]
    [InlineData("breve")]
    [InlineData("long")]
    public void Read_NoteTypeBeyondTheSupportedRange_ThrowsReadableError(string type)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasure(
            $"<note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration><type>{type}</type></note>"));

        Assert.Contains(type, exception.Message);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("quarter-sharp")]
    [InlineData("slash-flat")]
    [InlineData("three-quarters-flat")]
    public void Read_AccidentalSymbolWithoutAGlyph_IsAcceptedAndReported(string accidental)
    {
        var result = ReadMeasure(
            $"<note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration><type>quarter</type><accidental>{accidental}</accidental></note>");

        Assert.Null(Assert.Single(Assert.Single(result.Score.Measures).Notes).Accidental);
        Assert.Equal(1, WarningCount(result, "microtonal or special accidental"));
    }

    [Fact]
    public void Read_TwoAccidentalsOnOneNote_KeepsTheSupportedOne()
    {
        var result = ReadMeasure(
            "<note><pitch><step>B</step><alter>-1</alter><octave>4</octave></pitch><duration>2</duration><type>quarter</type>" +
            "<accidental smufl=\"accSagittal7CommaDown\">other</accidental><accidental>flat</accidental></note>");

        Assert.Equal(ScoreAccidental.Flat, Assert.Single(Assert.Single(result.Score.Measures).Notes).Accidental);
        Assert.Equal(1, WarningCount(result, "microtonal or special accidental"));
    }

    [Fact]
    public void Read_AccidentalThatIsNotAMusicXmlValue_ThrowsReadableError()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadMeasure(
            "<note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration><type>quarter</type><accidental>bogus</accidental></note>"));

        Assert.Contains("<accidental>", exception.Message);
    }

    [Fact]
    public void Read_MicrotonalAlter_StillThrowsReadableError()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadMeasure(
            "<note><pitch><step>C</step><alter>0.5</alter><octave>4</octave></pitch><duration>2</duration><type>quarter</type><accidental>quarter-sharp</accidental></note>"));

        Assert.Contains("<alter>", exception.Message);
    }

    [Theory]
    [InlineData("harmonic")]
    [InlineData("bend")]
    public void Read_TechnicalMarkThatChangesThePitch_ThrowsReadableError(string technical)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasure(
            QuarterNote(notations: $"<technical><{technical}/></technical>")));

        Assert.Contains($"<{technical}>", exception.Message);
    }

    [Fact]
    public void Read_VoiceDirections_AreAcceptedAndReported()
    {
        var read = ReadMeasures($"""
            <measure number="1">
              <attributes>
                <divisions>2</divisions>
                <voice-directions><voice-direction voice="3" staff="1">down</voice-direction></voice-directions>
              </attributes>
              {QuarterNote()}
            </measure>
            """);

        Assert.Equal(1, WarningCount(read, "voice-directions"));
    }
}
