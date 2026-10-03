using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Dynamics, words, hairpins, pedal marks, tempo marks and chord symbols (docs/research/musicxml-coverage.md,
// findings 1 and 3): they are kept as measure data instead of being parsed away.
public sealed partial class MusicXmlScoreReaderTests
{
    private static string Direction(string directionTypeContent, string directionAttributes = "", string extra = "") =>
        $"<direction {directionAttributes}><direction-type>{directionTypeContent}</direction-type>{extra}</direction>";

    private static IReadOnlyList<ScoreDirection> ReadDirections(string measureContent) =>
        Assert.Single(ReadMeasure(measureContent + QuarterNote()).Score.Measures).Directions ?? [];

    [Fact]
    public void Read_Dynamics_KeepsTheMarkAtItsBeatBelowTheStaff()
    {
        var directions = ReadDirections(QuarterNote() + Direction("<dynamics><mf/></dynamics>"));

        var mark = Assert.Single(directions);
        Assert.Equal(ScoreDirectionKind.Dynamics, mark.Kind);
        Assert.Equal("mf", mark.Text);
        Assert.Equal(1, mark.BeatOffset);
        Assert.Equal(Staff.Treble, mark.Staff);
        Assert.True(mark.IsBelow);
    }

    [Fact]
    public void Read_SeveralDynamicsInOneElement_KeepsEach()
    {
        var directions = ReadDirections(Direction("<dynamics><p/><f/></dynamics>"));

        Assert.Equal(["p", "f"], directions.Select(mark => mark.Text));
    }

    [Fact]
    public void Read_OtherDynamics_KeepsItsText()
    {
        var directions = ReadDirections(Direction("<dynamics><other-dynamics>sfff</other-dynamics></dynamics>"));

        Assert.Equal("sfff", Assert.Single(directions).Text);
    }

    [Fact]
    public void Read_DirectionOnTheSecondStaff_BelongsToTheBassStaff()
    {
        var directions = ReadDirections(Direction("<dynamics><p/></dynamics>", extra: "<staff>2</staff>"));

        Assert.Equal(Staff.Bass, Assert.Single(directions).Staff);
    }

    [Theory]
    [InlineData("placement=\"above\"", false)]
    [InlineData("placement=\"below\"", true)]
    [InlineData("", true)]
    public void Read_DynamicsPlacement_FollowsTheAttributeElseGoesBelow(string attributes, bool expectedIsBelow)
    {
        var directions = ReadDirections(Direction("<dynamics><p/></dynamics>", attributes));

        Assert.Equal(expectedIsBelow, Assert.Single(directions).IsBelow);
    }

    [Theory]
    [InlineData("placement=\"above\"", false)]
    [InlineData("placement=\"below\"", true)]
    [InlineData("", false)]
    public void Read_WordsPlacement_FollowsTheAttributeElseGoesAbove(string attributes, bool expectedIsBelow)
    {
        var directions = ReadDirections(Direction("<words>dolce</words>", attributes));

        var words = Assert.Single(directions);
        Assert.Equal(ScoreDirectionKind.Words, words.Kind);
        Assert.Equal("dolce", words.Text);
        Assert.Equal(expectedIsBelow, words.IsBelow);
    }

    [Fact]
    public void Read_WordsWithOnlyWhitespace_AreDropped()
    {
        Assert.Empty(ReadDirections(Direction("<words>   </words>")));
    }

    [Fact]
    public void Read_DirectionOffset_ShiftsThePositionByThatManyDivisions()
    {
        // divisions = 2, so an offset of 1 is half a beat.
        var directions = ReadDirections(Direction("<words>x</words>", extra: "<offset>1</offset>"));

        Assert.Equal(0.5, Assert.Single(directions).BeatOffset);
    }

    [Fact]
    public void Read_DirectionOffsetForSoundOnly_DoesNotMoveThePrintedPosition()
    {
        var directions = ReadDirections(Direction("<words>x</words>", extra: "<offset sound=\"yes\">1</offset>"));

        Assert.Equal(0, Assert.Single(directions).BeatOffset);
    }

    [Fact]
    public void Read_NegativeOffsetBeforeTheMeasureStart_ClampsToTheStart()
    {
        var directions = ReadDirections(Direction("<words>x</words>", extra: "<offset>-3</offset>"));

        Assert.Equal(0, Assert.Single(directions).BeatOffset);
    }

    [Theory]
    [InlineData("<wedge type=\"crescendo\"/>", ScoreDirectionKind.CrescendoStart, 1)]
    [InlineData("<wedge type=\"diminuendo\" number=\"2\"/>", ScoreDirectionKind.DiminuendoStart, 2)]
    [InlineData("<wedge type=\"stop\" number=\"3\"/>", ScoreDirectionKind.WedgeStop, 3)]
    public void Read_Wedge_KeepsItsKindAndNumber(string wedge, ScoreDirectionKind expectedKind, int expectedNumber)
    {
        var directions = ReadDirections(Direction(wedge));

        var mark = Assert.Single(directions);
        Assert.Equal(expectedKind, mark.Kind);
        Assert.Equal(expectedNumber, mark.Number);
        Assert.True(mark.IsBelow);
    }

    [Fact]
    public void Read_WedgeContinue_IsIgnored()
    {
        var result = ReadMeasure(Direction("<wedge type=\"continue\"/>") + QuarterNote());

        Assert.Null(Assert.Single(result.Score.Measures).Directions);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("start", ScoreDirectionKind.PedalStart)]
    [InlineData("stop", ScoreDirectionKind.PedalStop)]
    [InlineData("change", ScoreDirectionKind.PedalChange)]
    public void Read_Pedal_KeepsItsKind(string type, ScoreDirectionKind expectedKind)
    {
        var directions = ReadDirections(Direction($"<pedal type=\"{type}\" line=\"yes\"/>"));

        var mark = Assert.Single(directions);
        Assert.Equal(expectedKind, mark.Kind);
        Assert.True(mark.IsBelow);
    }

    [Theory]
    [InlineData("continue")]
    [InlineData("sostenuto")]
    public void Read_PedalWithoutAMark_IsAcceptedAndReported(string type)
    {
        var result = ReadMeasure(Direction($"<pedal type=\"{type}\"/>") + QuarterNote());

        Assert.Equal(1, WarningCount(result, "pedal"));
        Assert.Null(Assert.Single(result.Score.Measures).Directions);
    }

    [Theory]
    [InlineData("<metronome><beat-unit>quarter</beat-unit><per-minute>120</per-minute></metronome>", "𝅘𝅥 = 120")]
    [InlineData("<metronome><beat-unit>eighth</beat-unit><beat-unit-dot/><per-minute>66</per-minute></metronome>", "𝅘𝅥𝅮. = 66")]
    [InlineData("<metronome><beat-unit>half</beat-unit><beat-unit>quarter</beat-unit></metronome>", "𝅗𝅥 = 𝅘𝅥")]
    public void Read_Metronome_KeepsTheTempoMarkText(string metronome, string expectedText)
    {
        var directions = ReadDirections(Direction(metronome));

        var mark = Assert.Single(directions);
        Assert.Equal(ScoreDirectionKind.Metronome, mark.Kind);
        Assert.Equal(expectedText, mark.Text);
        Assert.False(mark.IsBelow);
    }

    [Fact]
    public void Read_RehearsalSegnoAndCoda_AreKept()
    {
        var directions = ReadDirections(
            Direction("<rehearsal>A</rehearsal>") + Direction("<segno/>") + Direction("<coda/>"));

        Assert.Equal(
            [ScoreDirectionKind.Rehearsal, ScoreDirectionKind.Segno, ScoreDirectionKind.Coda],
            directions.Select(mark => mark.Kind));
        Assert.Equal("A", directions[0].Text);
    }

    [Theory]
    [InlineData("<bracket type=\"start\" line-end=\"none\"/>", "bracket")]
    [InlineData("<dashes type=\"start\"/>", "dashes")]
    [InlineData("<damp/>", "damp")]
    [InlineData("<eyeglasses/>", "eyeglasses")]
    [InlineData("<other-direction>x</other-direction>", "other-direction")]
    [InlineData("<scordatura><accord string=\"1\"><tuning-step>E</tuning-step><tuning-octave>4</tuning-octave></accord></scordatura>", "scordatura")]
    public void Read_DirectionWithoutAMark_IsAcceptedAndReported(string content, string construct)
    {
        var result = ReadMeasure(Direction(content) + QuarterNote());

        Assert.Equal(1, WarningCount(result, construct));
        Assert.Null(Assert.Single(result.Score.Measures).Directions);
    }

    [Fact]
    public void Read_DirectionsKeepDocumentOrder()
    {
        var directions = ReadDirections(
            Direction("<words>a</words>") + QuarterNote() + Direction("<words>b</words>"));

        Assert.Equal(["a", "b"], directions.Select(mark => mark.Text));
    }

    [Fact]
    public void Read_MeasureWithoutDirections_HasNone()
    {
        Assert.Null(Assert.Single(ReadMeasure(QuarterNote()).Score.Measures).Directions);
    }

    [Fact]
    public void Read_MetronomeTempoStillSetsTheScoreTempo()
    {
        var result = ReadMeasure(
            Direction("<metronome><beat-unit>quarter</beat-unit><per-minute>90</per-minute></metronome>") + QuarterNote());

        Assert.Equal(new Tempo(90), result.Score.Tempo);
    }

    private static string Harmony(string content) => $"<harmony>{content}</harmony>";

    [Theory]
    [InlineData("<root><root-step>C</root-step></root><kind>major</kind>", "C")]
    [InlineData("<root><root-step>C</root-step></root><kind>minor-seventh</kind>", "Cm7")]
    [InlineData("<root><root-step>B</root-step><root-alter>-1</root-alter></root><kind>dominant</kind>", "B♭7")]
    [InlineData("<root><root-step>F</root-step><root-alter>1</root-alter></root><kind>diminished</kind>", "F♯dim")]
    [InlineData("<root><root-step>C</root-step></root><kind>major-seventh</kind><bass><bass-step>E</bass-step></bass>", "Cmaj7/E")]
    [InlineData("<root><root-step>G</root-step></root><kind text=\"6\">major-sixth</kind><bass><bass-step>D</bass-step></bass>", "G6/D")]
    [InlineData("<root><root-step>A</root-step></root><kind text=\"\">major</kind>", "A")]
    [InlineData("<root><root-step>D</root-step></root><kind>suspended-fourth</kind>", "Dsus4")]
    [InlineData("<root><root-step>E</root-step></root><kind>half-diminished</kind>", "Em7♭5")]
    [InlineData("<root><root-step>C</root-step></root><kind text=\"+\">augmented</kind>", "C+")]
    [InlineData("<root><root-step>C</root-step></root><kind text=\"N.C.\">none</kind>", "N.C.")]
    [InlineData("<root><root-step>C</root-step></root><kind>major</kind><bass><bass-step>B</bass-step><bass-alter>-1</bass-alter></bass>", "C/B♭")]
    public void Read_Harmony_BecomesAChordSymbol(string content, string expectedText)
    {
        var directions = ReadDirections(Harmony(content));

        var symbol = Assert.Single(directions);
        Assert.Equal(ScoreDirectionKind.ChordSymbol, symbol.Kind);
        Assert.Equal(expectedText, symbol.Text);
        Assert.False(symbol.IsBelow);
    }

    [Fact]
    public void Read_HarmonyDegrees_AreAppended()
    {
        var directions = ReadDirections(Harmony(
            "<root><root-step>A</root-step></root><kind parentheses-degrees=\"yes\">major</kind>" +
            "<degree><degree-value>9</degree-value><degree-alter>0</degree-alter><degree-type text=\"\">add</degree-type></degree>"));

        Assert.Equal("A(9)", Assert.Single(directions).Text);
    }

    [Fact]
    public void Read_HarmonyDegreesWithWords_UseTheirDefaultWords()
    {
        var directions = ReadDirections(Harmony(
            "<root><root-step>C</root-step></root><kind>dominant</kind>" +
            "<degree><degree-value>9</degree-value><degree-alter>-1</degree-alter><degree-type>alter</degree-type></degree>" +
            "<degree><degree-value>5</degree-value><degree-alter>0</degree-alter><degree-type>subtract</degree-type></degree>"));

        Assert.Equal("C7♭9no5", Assert.Single(directions).Text);
    }

    [Fact]
    public void Read_HarmonyWithAFunction_PrintsTheFunction()
    {
        var directions = ReadDirections(Harmony("<function>V</function><kind>dominant</kind>"));

        Assert.Equal("V7", Assert.Single(directions).Text);
    }

    [Fact]
    public void Read_HarmonyAtALaterBeat_SitsAtThatBeat()
    {
        var directions = ReadDirections(
            QuarterNote() + Harmony("<root><root-step>G</root-step></root><kind>major</kind>"));

        Assert.Equal(1, Assert.Single(directions).BeatOffset);
    }

    [Fact]
    public void Read_HarmonyWithAGuitarFrame_KeepsTheSymbolAndReportsTheDiagram()
    {
        var result = ReadMeasure(
            Harmony(
                "<root><root-step>G</root-step></root><kind>major</kind>" +
                "<frame><frame-strings>6</frame-strings><frame-frets>4</frame-frets></frame>") + QuarterNote());

        Assert.Equal("G", Assert.Single(Assert.Single(result.Score.Measures).Directions!).Text);
        Assert.Equal(1, WarningCount(result, "frame"));
    }

    [Fact]
    public void Read_HarmonyWithoutARootOrFunction_IsAcceptedAndReported()
    {
        var result = ReadMeasure(Harmony("<kind>major</kind>") + QuarterNote());

        Assert.Null(Assert.Single(result.Score.Measures).Directions);
        Assert.Equal(1, WarningCount(result, "harmony"));
    }

    [Fact]
    public void Read_FiguredBass_IsStillAcceptedAndReported()
    {
        var result = ReadMeasure(
            "<figured-bass><figure><figure-number>6</figure-number></figure><duration>2</duration></figured-bass>" + QuarterNote());

        Assert.Equal(1, WarningCount(result, "figured-bass"));
    }
}
