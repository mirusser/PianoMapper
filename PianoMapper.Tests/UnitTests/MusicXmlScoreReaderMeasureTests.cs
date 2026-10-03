using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Per-measure state read from <attributes>: key changes first (docs/research/musicxml-coverage.md, finding 2).
public sealed partial class MusicXmlScoreReaderTests
{
    private static string KeyAttributes(int fifths) =>
        $"<attributes><key><fifths>{fifths}</fifths></key></attributes>";

    private static string Measure(string content) => $"<measure number=\"1\">{content}</measure>";

    [Fact]
    public void Read_KeyChangeMidPiece_RecordsTheKeyOnTheMeasureWhereItChanges()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{KeyAttributes(0)}{QuarterNote()}</measure>" +
            $"<measure number=\"2\">{KeyAttributes(2)}{QuarterNote()}</measure>" +
            $"<measure number=\"3\">{KeyAttributes(2)}{QuarterNote()}</measure>");

        Assert.Equal(0, result.Score.KeyFifths);
        Assert.Equal([null, 2, null], result.Score.Measures.Select(measure => measure.KeyFifths));
    }

    [Fact]
    public void Read_KeyOnTheFirstMeasure_IsTheScoreKeyNotAChange()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{KeyAttributes(-3)}{QuarterNote()}</measure>" +
            $"<measure number=\"2\">{QuarterNote()}</measure>");

        Assert.Equal(-3, result.Score.KeyFifths);
        Assert.All(result.Score.Measures, measure => Assert.Null(measure.KeyFifths));
    }

    [Fact]
    public void Read_KeyFirstGivenInALaterMeasure_StartsInCMajor()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{QuarterNote()}</measure>" +
            $"<measure number=\"2\">{KeyAttributes(4)}{QuarterNote()}</measure>");

        Assert.Equal(0, result.Score.KeyFifths);
        Assert.Equal([null, 4], result.Score.Measures.Select(measure => measure.KeyFifths));
    }

    [Fact]
    public void Read_KeyChangeAfterTheNotesOfAMeasure_TakesEffectInTheNextMeasure()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{QuarterNote()}{KeyAttributes(1)}</measure>" +
            $"<measure number=\"2\">{QuarterNote()}</measure>");

        Assert.Equal([null, 1], result.Score.Measures.Select(measure => measure.KeyFifths));
    }

    [Fact]
    public void Read_KeyChangeBetweenTheNotesOfAMeasure_TakesEffectInTheNextMeasureAndIsReported()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{QuarterNote()}{KeyAttributes(1)}{QuarterNote()}</measure>" +
            $"<measure number=\"2\">{QuarterNote()}</measure>");

        Assert.Equal([null, 1], result.Score.Measures.Select(measure => measure.KeyFifths));
        Assert.Equal(1, WarningCount(result, "mid-measure key change"));
    }

    [Fact]
    public void Read_KeyWithoutFifths_KeepsTheKeyAndReportsIt()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{CommonAttributes}{KeyAttributes(2)}" +
            "<attributes><key><key-step>B</key-step><key-alter>-1.5</key-alter></key></attributes>" +
            $"{QuarterNote()}</measure>");

        Assert.Equal(2, result.Score.KeyFifths);
        Assert.Equal(1, WarningCount(result, "non-traditional key"));
    }

    [Fact]
    public void Read_DifferentKeysForTheTwoStaves_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasure(
            "<attributes><key number=\"1\"><fifths>2</fifths></key><key number=\"2\"><fifths>0</fifths></key></attributes>" +
            QuarterNote()));

        Assert.Contains("<key>", exception.Message);
    }

    [Fact]
    public void Read_SameKeyStatedForBothStaves_IsOneKey()
    {
        var result = ReadMeasure(
            "<attributes><key number=\"1\"><fifths>2</fifths></key><key number=\"2\"><fifths>2</fifths></key></attributes>" +
            QuarterNote());

        Assert.Equal(2, result.Score.KeyFifths);
    }

    [Fact]
    public void Read_KeyWithModeAndCancel_ReadsTheFifths()
    {
        var result = ReadMeasure(
            "<attributes><key><cancel>3</cancel><fifths>-1</fifths><mode>minor</mode></key></attributes>" + QuarterNote());

        Assert.Equal(-1, result.Score.KeyFifths);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_BackupBeyondTheMeasureStart_StopsAtTheStartAndIsReported()
    {
        // The official voice-direction-element sample backs up twice in a row after the last voice of a measure, so the
        // note that follows would start before its own measure; it is read as starting on the first beat.
        var result = ReadMeasure(
            QuarterNote() + "<backup><duration>4</duration></backup>" + QuarterNote("E"));

        var notes = Assert.Single(result.Score.Measures).Notes;
        Assert.Equal([0, 0], notes.Select(note => note.BeatOffset));
        Assert.Equal(1, WarningCount(result, "backup before the measure start"));
    }

    [Fact]
    public void Read_BackupToExactlyTheMeasureStart_IsNotReported()
    {
        var result = ReadMeasure(QuarterNote() + "<backup><duration>2</duration></backup>" + QuarterNote("E"));

        Assert.Empty(result.Warnings);
    }

    private static string TimeAttributes(string timeXml) =>
        $"<attributes><divisions>2</divisions>{timeXml}</attributes>";

    [Fact]
    public void Read_AdditiveMeter_SumsTheBeatsAndReportsIt()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes("<time><beats>3+2</beats><beat-type>8</beat-type></time>")}{QuarterNote()}</measure>");

        Assert.Equal(new TimeSignature(5, new NoteValue(8)), result.Score.TimeSignature);
        Assert.Equal(1, WarningCount(result, "additive meter"));
    }

    [Fact]
    public void Read_SeveralBeatPairsWithOneBeatType_SumTheBeats()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes("<time><beats>3</beats><beat-type>8</beat-type><beats>2</beats><beat-type>8</beat-type></time>")}{QuarterNote()}</measure>");

        Assert.Equal(new TimeSignature(5, new NoteValue(8)), result.Score.TimeSignature);
        Assert.Equal(1, WarningCount(result, "additive meter"));
    }

    [Fact]
    public void Read_CompositeMeterWithDifferentBeatTypes_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes("<time><beats>3</beats><beat-type>4</beat-type><beats>2</beats><beat-type>8</beat-type></time>")}{QuarterNote()}</measure>"));

        Assert.Contains("<time>", exception.Message);
    }

    [Fact]
    public void Read_SenzaMisura_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes("<time><senza-misura/></time>")}{QuarterNote()}</measure>"));

        Assert.Contains("<time>", exception.Message);
    }

    [Theory]
    [InlineData("symbol=\"common\"")]
    [InlineData("symbol=\"cut\"")]
    [InlineData("symbol=\"single-number\"")]
    public void Read_TimeSymbol_IsAcceptedAndReportedBecauseNumeralsAreDrawn(string attribute)
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes($"<time {attribute}><beats>4</beats><beat-type>4</beat-type></time>")}{QuarterNote()}</measure>");

        Assert.Equal(1, WarningCount(result, "time symbol"));
    }

    [Fact]
    public void Read_HiddenTimeSignature_StillSetsTheMeterAndIsReported()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{TimeAttributes("<time print-object=\"no\"><beats>3</beats><beat-type>4</beat-type></time>")}{QuarterNote()}</measure>");

        Assert.Equal(new TimeSignature(3, new NoteValue(4)), result.Score.TimeSignature);
        Assert.Equal(1, WarningCount(result, "hidden time signature"));
    }

    [Fact]
    public void Read_ImplicitPickupMeasure_RecordsItsActualLength()
    {
        var result = ReadMeasures(
            $"<measure number=\"0\" implicit=\"yes\">{CommonAttributes}{QuarterNote()}</measure>" +
            $"<measure number=\"1\">{QuarterNote("D")}</measure>");

        Assert.Equal(1, result.Score.Measures[0].LengthInBeats);
        Assert.Null(result.Score.Measures[1].LengthInBeats);
    }
}
