using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Barlines, repeats, voltas, segno/coda signs and jump instructions (docs/research/musicxml-coverage.md, findings 1 and 3).
public sealed partial class MusicXmlScoreReaderTests
{
    private static ScoreMeasure ReadFirstMeasure(string measureContent) =>
        Assert.Single(ReadMeasure(measureContent + QuarterNote()).Score.Measures);

    [Fact]
    public void Read_ForwardRepeatOnTheLeftAndBackwardOnTheRight_AreRecordedOnTheirBarlines()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}" +
            "<barline location=\"left\"><bar-style>heavy-light</bar-style><repeat direction=\"forward\"/></barline>" +
            QuarterNote() +
            "<barline location=\"right\"><bar-style>light-heavy</bar-style><repeat direction=\"backward\"/></barline>" +
            "</measure>");

        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(ScoreRepeatDirection.Forward, measure.LeftBarline?.Repeat);
        Assert.Equal(ScoreRepeatDirection.Backward, measure.RightBarline?.Repeat);
        Assert.Equal(2, measure.RightBarline?.RepeatTimes);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_BackwardRepeatWithTimes_RecordsTheTimes()
    {
        var measure = ReadFirstMeasure("<barline><repeat direction=\"backward\" times=\"3\"/></barline>");

        Assert.Equal(3, measure.RightBarline?.RepeatTimes);
    }

    [Fact]
    public void Read_BarlineWithoutLocation_IsTheRightBarline()
    {
        var measure = ReadFirstMeasure("<barline><bar-style>light-light</bar-style></barline>");

        Assert.Null(measure.LeftBarline);
        Assert.Equal(ScoreBarlineStyle.Double, measure.RightBarline?.Style);
    }

    [Theory]
    [InlineData("regular", ScoreBarlineStyle.Regular)]
    [InlineData("light-light", ScoreBarlineStyle.Double)]
    [InlineData("light-heavy", ScoreBarlineStyle.Final)]
    public void Read_BarStyle_MapsToTheDrawnStyle(string style, ScoreBarlineStyle expected)
    {
        var result = ReadMeasure($"<barline><bar-style>{style}</bar-style></barline>" + QuarterNote());

        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(expected, measure.RightBarline?.Style ?? ScoreBarlineStyle.Regular);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("dashed")]
    [InlineData("dotted")]
    [InlineData("heavy-heavy")]
    [InlineData("none")]
    [InlineData("short")]
    [InlineData("tick")]
    public void Read_BarStyleWithoutADrawing_IsAcceptedAsRegularAndReported(string style)
    {
        var result = ReadMeasure($"<barline><bar-style>{style}</bar-style></barline>" + QuarterNote());

        Assert.Equal(1, WarningCount(result, "bar-style"));
        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(ScoreBarlineStyle.Regular, measure.RightBarline?.Style ?? ScoreBarlineStyle.Regular);
    }

    [Fact]
    public void Read_EndingsStartingAndStopping_AreRecordedWithTheirNumbers()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}" +
            "<barline location=\"left\"><ending number=\"1, 2\" type=\"start\"/></barline>" +
            QuarterNote() +
            "<barline location=\"right\"><ending number=\"1, 2\" type=\"stop\"/><repeat direction=\"backward\"/></barline>" +
            "</measure>" +
            "<measure number=\"2\"><barline location=\"left\"><ending number=\"3\" type=\"start\"/></barline>" +
            QuarterNote() +
            "<barline location=\"right\"><ending number=\"3\" type=\"discontinue\"/></barline></measure>");

        Assert.Equal(new ScoreEnding("1, 2", ScoreEndingType.Start), result.Score.Measures[0].LeftBarline?.Ending);
        Assert.Equal(new ScoreEnding("1, 2", ScoreEndingType.Stop), result.Score.Measures[0].RightBarline?.Ending);
        Assert.Equal(new ScoreEnding("3", ScoreEndingType.Start), result.Score.Measures[1].LeftBarline?.Ending);
        Assert.Equal(new ScoreEnding("3", ScoreEndingType.Discontinue), result.Score.Measures[1].RightBarline?.Ending);
    }

    [Fact]
    public void Read_EndingWithAnUnknownType_ThrowsReadableError()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            ReadMeasure("<barline><ending number=\"1\" type=\"sideways\"/></barline>" + QuarterNote()));

        Assert.Contains("<ending>", exception.Message);
    }

    [Theory]
    [InlineData("<segno/>", ScoreBarlineMark.Segno, 1)]
    [InlineData("<coda/>", ScoreBarlineMark.Coda, 1)]
    [InlineData("<coda/><coda/><coda/>", ScoreBarlineMark.Coda, 3)]
    public void Read_SegnoOrCodaOnABarline_IsRecordedWithItsCount(string markXml, ScoreBarlineMark expectedMark, int expectedCount)
    {
        var measure = ReadFirstMeasure($"<barline location=\"right\">{markXml}</barline>");

        Assert.Equal(expectedMark, measure.RightBarline?.Mark);
        Assert.Equal(expectedCount, measure.RightBarline?.MarkCount);
    }

    [Fact]
    public void Read_SegnoAndCodaInOneBarline_ThrowsReadableError()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            ReadMeasure("<barline><segno/><coda/></barline>" + QuarterNote()));

        Assert.Contains("<segno>", exception.Message);
        Assert.Contains("<coda>", exception.Message);
    }

    [Theory]
    [InlineData("<fermata/>", ScoreFermata.Upright)]
    [InlineData("<fermata type=\"inverted\"/>", ScoreFermata.Inverted)]
    public void Read_FermataOnABarline_IsRecorded(string fermataXml, ScoreFermata expected)
    {
        var measure = ReadFirstMeasure($"<barline>{fermataXml}</barline>");

        Assert.Equal(expected, measure.RightBarline?.Fermata);
    }

    [Fact]
    public void Read_BarlineEditorialAndWavyLine_AreAcceptedAndWavyLineIsReported()
    {
        var result = ReadMeasure(
            "<barline><footnote>x</footnote><level>y</level><wavy-line type=\"start\"/></barline>" + QuarterNote());

        Assert.Equal(1, WarningCount(result, "wavy-line"));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_BarlineInTheMiddleOfAMeasure_IsReportedAndNotRecorded()
    {
        var result = ReadMeasure(
            QuarterNote() + "<barline location=\"middle\"><bar-style>light-light</bar-style></barline>" + QuarterNote());

        Assert.Equal(1, WarningCount(result, "mid-measure barline"));
        var measure = Assert.Single(result.Score.Measures);
        Assert.Null(measure.RightBarline);
        Assert.Null(measure.LeftBarline);
    }

    [Fact]
    public void Read_PlainMeasure_HasNoBarlineData()
    {
        var measure = ReadFirstMeasure(string.Empty);

        Assert.Null(measure.LeftBarline);
        Assert.Null(measure.RightBarline);
    }

    [Theory]
    [InlineData("dacapo=\"yes\"")]
    [InlineData("fine=\"yes\"")]
    [InlineData("forward-repeat=\"yes\"")]
    public void Read_JumpInstruction_IsAcceptedAndReportedAsNotFollowed(string soundAttribute)
    {
        var result = ReadMeasure($"<direction><direction-type><words>x</words></direction-type><sound {soundAttribute}/></direction>" + QuarterNote());

        Assert.Equal(1, WarningCount(result, "jump instruction"));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_ToCodaAndDalSegnoWithTheirTargets_AreAcceptedAndReported()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}<sound segno=\"s1\"/>{QuarterNote()}</measure>" +
            "<measure number=\"2\"><sound tocoda=\"c1\"/><sound dalsegno=\"s1\"/>" + QuarterNote() + "</measure>" +
            "<measure number=\"3\"><sound coda=\"c1\"/>" + QuarterNote() + "</measure>");

        Assert.Equal(2, WarningCount(result, "jump instruction"));
        Assert.Equal(3, result.Score.Measures.Count);
    }

    [Theory]
    [InlineData("dalsegno=\"segno2\"", "segno2")]
    [InlineData("tocoda=\"coda2\"", "coda2")]
    public void Read_JumpToATargetThatIsNotThere_ThrowsReadableError(string soundAttribute, string target)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadMeasure($"<sound {soundAttribute}/>" + QuarterNote()));

        Assert.Contains(target, exception.Message);
    }
}
