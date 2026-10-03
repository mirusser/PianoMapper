using PianoMapper.Music;

namespace PianoMapper.Tests.UnitTests;

// Piano music written as two parts, one per hand (docs/research/musicxml-coverage.md, finding 1): each part is one staff
// and together they are the two staves of the grand staff.
public sealed partial class MusicXmlScoreReaderTests
{
    private static string PartXml(string id, string measures, int divisions = 2, string attributesExtra = "") =>
        $"""
        <part id="{id}">
          <measure number="1">
            <attributes>
              <divisions>{divisions}</divisions>{attributesExtra}
              <time><beats>4</beats><beat-type>4</beat-type></time>
            </attributes>
            {measures}
          </measure>
        </part>
        """;

    private static string Note(string step, int octave, int duration) =>
        $"<note><pitch><step>{step}</step><octave>{octave}</octave></pitch><duration>{duration}</duration></note>";

    private static MusicXmlReadResult ReadParts(params string[] partsXml)
    {
        string scoreParts = string.Concat(partsXml.Select((_, index) =>
            $"<score-part id=\"P{index + 1}\"><part-name/></score-part>"));
        string xml = $"<score-partwise version=\"4.0\"><part-list>{scoreParts}</part-list>{string.Concat(partsXml)}</score-partwise>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return new MusicXmlScoreReader().ReadWithWarnings(stream, "parts.musicxml");
    }

    [Fact]
    public void Read_TwoSingleStaffParts_BecomeTheUpperAndLowerStaffOfOneScore()
    {
        var result = ReadParts(
            PartXml("P1", Note("C", 5, 4) + Note("D", 5, 4), divisions: 2),
            PartXml("P2", Note("C", 3, 8), divisions: 4));

        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(
            [(NoteLetter.C, Staff.Treble, 0.0), (NoteLetter.D, Staff.Treble, 2.0), (NoteLetter.C, Staff.Bass, 0.0)],
            measure.Notes.Select(note => (note.Pitch.Letter, note.Staff, note.BeatOffset)));
    }

    [Fact]
    public void Read_TwoParts_KeepRestsAndDirectionsOnTheirOwnStaff()
    {
        var result = ReadParts(
            PartXml("P1", Note("C", 5, 8)),
            PartXml(
                "P2",
                Direction("<dynamics><p/></dynamics>") + "<note><rest/><duration>8</duration></note>"));

        var measure = Assert.Single(result.Score.Measures);
        Assert.Equal(Staff.Bass, Assert.Single(measure.Rests).Staff);
        Assert.Equal(Staff.Bass, Assert.Single(measure.Directions!).Staff);
    }

    [Fact]
    public void Read_TwoPartsWithImplicitPickup_KeepItsActualLength()
    {
        string PickupPart(string id, string note) => PartXml(id, note, divisions: 2)
            .Replace("<measure number=\"1\">", "<measure number=\"1\" implicit=\"yes\">", StringComparison.Ordinal);

        var result = ReadParts(
            PickupPart("P1", Note("C", 5, 2)),
            PickupPart("P2", Note("C", 3, 2)));

        Assert.Equal(1, Assert.Single(result.Score.Measures).LengthInBeats);
    }

    [Fact]
    public void Read_TwoPartsWithDifferentImplicitPickupLengths_ThrowsReadableError()
    {
        string PickupPart(string id, string note) => PartXml(id, note, divisions: 2)
            .Replace("<measure number=\"1\">", "<measure number=\"1\" implicit=\"yes\">", StringComparison.Ordinal);

        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(
            PickupPart("P1", Note("C", 5, 2)),
            PickupPart("P2", Note("C", 3, 4))));

        Assert.Contains("<measure>", exception.Message);
    }

    [Fact]
    public void Read_TwoParts_TakeTheTempoFromWhicheverStatesOne()
    {
        var result = ReadParts(
            PartXml("P1", Note("C", 5, 8)),
            PartXml("P2", "<sound tempo=\"60\"/>" + Note("C", 3, 8)));

        Assert.Equal(new Tempo(60), result.Score.Tempo);
    }

    [Fact]
    public void Read_TwoPartsWithDifferentMeasureCounts_ThrowsReadableError()
    {
        string secondPart = PartXml("P2", Note("C", 3, 8)).Replace("</part>", "<measure number=\"2\"/></part>", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => ReadParts(PartXml("P1", Note("C", 5, 8)), secondPart));

        Assert.Contains("measures", exception.Message);
    }

    [Fact]
    public void Read_TwoPartsWithDifferentTimeSignatures_ThrowsReadableError()
    {
        string secondPart = PartXml("P2", Note("C", 3, 6)).Replace("<beats>4</beats>", "<beats>3</beats>", StringComparison.Ordinal);

        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(PartXml("P1", Note("C", 5, 8)), secondPart));

        Assert.Contains("<time>", exception.Message);
    }

    [Fact]
    public void Read_TwoPartsWithDifferentKeys_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(
            PartXml("P1", Note("C", 5, 8), attributesExtra: "<key><fifths>2</fifths></key>"),
            PartXml("P2", Note("C", 3, 8), attributesExtra: "<key><fifths>0</fifths></key>")));

        Assert.Contains("<key>", exception.Message);
    }

    [Fact]
    public void Read_TwoPartsWithTheSameKey_KeepTheKey()
    {
        var result = ReadParts(
            PartXml("P1", Note("C", 5, 8), attributesExtra: "<key><fifths>2</fifths></key>"),
            PartXml("P2", Note("C", 3, 8), attributesExtra: "<key><fifths>2</fifths></key>"));

        Assert.Equal(2, result.Score.KeyFifths);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void Read_PartsAddingUpToMoreThanTwoStaves_ThrowReadableError(int firstPartStaves, int secondPartStaves)
    {
        string StavesExtra(int staves) => staves == 1 ? string.Empty : $"<staves>{staves}</staves>";

        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(
            PartXml("P1", Note("C", 5, 8), attributesExtra: StavesExtra(firstPartStaves)),
            PartXml("P2", Note("C", 3, 8), attributesExtra: StavesExtra(secondPartStaves))));

        Assert.Contains("<part>", exception.Message);
        Assert.Contains("staves", exception.Message);
    }

    [Fact]
    public void Read_ThreeSingleStaffParts_ThrowReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(
            PartXml("P1", Note("C", 5, 8)),
            PartXml("P2", Note("C", 4, 8)),
            PartXml("P3", Note("C", 3, 8))));

        Assert.Contains("<part>", exception.Message);
    }

    [Fact]
    public void Read_ScoreWithoutAnyPart_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadParts());

        Assert.Contains("<part>", exception.Message);
    }

    [Fact]
    public void Read_UnpitchedPartAmongTwo_ThrowsReadableError()
    {
        var exception = Assert.Throws<NotSupportedException>(() => ReadParts(
            PartXml("P1", "<note><unpitched><display-step>C</display-step><display-octave>5</display-octave></unpitched><duration>8</duration></note>"),
            PartXml("P2", Note("C", 3, 8))));

        Assert.Contains("<unpitched>", exception.Message);
    }
}
