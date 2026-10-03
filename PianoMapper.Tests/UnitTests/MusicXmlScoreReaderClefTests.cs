using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Clefs (docs/research/musicxml-coverage.md, findings 2 and 8). The grand staff draws a note on the treble or bass staff by
// its pitch, so a clef never moves a pitch; it only decides the staff of a one-staff part, and a change or a clef this
// app does not draw is reported.
public sealed partial class MusicXmlScoreReaderTests
{
    private static string Clef(string sign, int line, string extra = "", string number = "") =>
        $"<clef{(number.Length == 0 ? string.Empty : $" number=\"{number}\"")}><sign>{sign}</sign><line>{line}</line>{extra}</clef>";

    private static string ClefAttributes(string clefXml, string staves = "") =>
        $"<attributes><divisions>2</divisions>{staves}{clefXml}<time><beats>4</beats><beat-type>4</beat-type></time></attributes>";

    [Fact]
    public void Read_OneStaffPartWithABassClef_PutsItsNotesOnTheBassStaff()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("F", 4))}{QuarterNote("C")}</measure>");

        Assert.Equal(Staff.Bass, Assert.Single(Assert.Single(result.Score.Measures).Notes).Staff);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_OneStaffPartWithATrebleClef_KeepsItsNotesOnTheTrebleStaff()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("G", 2))}{QuarterNote("C")}</measure>");

        Assert.Equal(Staff.Treble, Assert.Single(Assert.Single(result.Score.Measures).Notes).Staff);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_OneStaffPartWithoutAClef_KeepsItsNotesOnTheTrebleStaff()
    {
        var result = ReadMeasure(QuarterNote());

        Assert.Equal(Staff.Treble, Assert.Single(Assert.Single(result.Score.Measures).Notes).Staff);
    }

    [Fact]
    public void Read_OneStaffPartChangingToABassClef_MovesTheLaterNotesToTheBassStaffAndReportsTheChange()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("G", 2))}{QuarterNote("C")}</measure>" +
            $"<measure number=\"2\"><attributes>{Clef("F", 4)}</attributes>{QuarterNote("C")}</measure>");

        Assert.Equal(
            [Staff.Treble, Staff.Bass],
            result.Score.Measures.Select(measure => Assert.Single(measure.Notes).Staff));
        Assert.Equal(1, WarningCount(result, "clef change"));
    }

    [Fact]
    public void Read_OneStaffPartRepeatingTheSameClef_ReportsNothing()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("G", 2))}{QuarterNote("C")}</measure>" +
            $"<measure number=\"2\"><attributes>{Clef("G", 2)}</attributes>{QuarterNote("C")}</measure>");

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_TwoStaffPartWithTheUsualClefs_AssignsStavesByTheStaffElementAndReportsNothing()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("G", 2, number: "1") + Clef("F", 4, number: "2"), "<staves>2</staves>")}" +
            QuarterNote("C", extra: "<staff>2</staff>") + "</measure>");

        Assert.Equal(Staff.Bass, Assert.Single(Assert.Single(result.Score.Measures).Notes).Staff);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Read_TwoStaffPartWhoseUpperStaffTakesABassClef_KeepsTheStaffElementsStaff()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("F", 4, number: "1") + Clef("F", 4, number: "2"), "<staves>2</staves>")}" +
            QuarterNote("C", extra: "<staff>1</staff>") + "</measure>");

        Assert.Equal(Staff.Treble, Assert.Single(Assert.Single(result.Score.Measures).Notes).Staff);
    }

    [Theory]
    [InlineData("C", 3)]
    [InlineData("C", 4)]
    [InlineData("F", 3)]
    [InlineData("G", 1)]
    public void Read_ClefThisAppDoesNotDraw_IsAcceptedAndReported(string sign, int line)
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef(sign, line))}{QuarterNote("C")}</measure>");

        Assert.Equal(1, WarningCount(result, "non-standard clef"));
        Assert.Single(Assert.Single(result.Score.Measures).Notes);
    }

    [Fact]
    public void Read_EighthVaClef_IsReportedAsNonStandard()
    {
        var result = ReadMeasures(
            $"<measure number=\"1\">{ClefAttributes(Clef("G", 2, "<clef-octave-change>-1</clef-octave-change>"))}{QuarterNote("C")}</measure>");

        Assert.Equal(1, WarningCount(result, "non-standard clef"));
    }

    [Theory]
    [InlineData("percussion")]
    [InlineData("TAB")]
    [InlineData("jianpu")]
    public void Read_ClefForUnpitchedOrTabNotation_ThrowsReadableError(string sign)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadMeasures(
            $"<measure number=\"1\"><attributes><divisions>2</divisions><clef><sign>{sign}</sign></clef></attributes>{QuarterNote()}</measure>"));

        Assert.Contains("<clef>", exception.Message);
        Assert.Contains(sign, exception.Message);
    }

    [Fact]
    public void Read_TwoPartsListedLowerHandFirst_PutTheBassClefPartOnTheLowerStaff()
    {
        var result = ReadParts(
            PartXml("P1", Note("C", 3, 8), attributesExtra: Clef("F", 4)),
            PartXml("P2", Note("C", 5, 8), attributesExtra: Clef("G", 2)));

        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(
            [(NoteLetter.C, 5, Staff.Treble), (NoteLetter.C, 3, Staff.Bass)],
            measure.Notes.Select(note => (note.Pitch.Letter, note.Pitch.Octave, note.Staff)));
    }
}
