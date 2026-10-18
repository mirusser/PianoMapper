using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PianoMapper.Music;
using PianoMapper.Server.Omr;

namespace PianoMapper.Tests.UnitTests;

public sealed class OemerMusicXmlPostProcessorTests
{
    // Measures 1, 2 and 9 of a real oemer reading of a phone photo (Passacaglia, 4/4): measure 1 is underfull and
    // steps back before its own start, measure 2 is complete, measure 9 is overfull. The file carries oemer's
    // <sound tempo="90"/> directly under <part>, which MusicXML does not allow there.
    private const string ExcerptFixture = "oemer-passacaglia-excerpt.musicxml";

    private readonly OemerMusicXmlPostProcessor processor = new();

    [Fact]
    public void Read_OemerOutputWithStraySound_FailsBeforeRepair()
    {
        byte[] oemerOutput = File.ReadAllBytes(Fixture(ExcerptFixture));
        using var stream = new MemoryStream(oemerOutput);

        var exception = Assert.Throws<NotSupportedException>(
            () => new MusicXmlScoreReader().Read(stream, "oemer.musicxml"));

        Assert.Contains("<sound>", exception.Message);
    }

    [Fact]
    public void Process_OemerOutputWithStraySound_ImportsWithItsTempoMovedIntoTheFirstMeasure()
    {
        byte[] oemerOutput = File.ReadAllBytes(Fixture(ExcerptFixture));

        var result = processor.Process(oemerOutput);

        using var repaired = new MemoryStream(result.CompressedMusicXml);
        var score = new MusicXmlScoreReader().Read(repaired, "recognized.mxl");
        Assert.Equal(3, score.Measures.Count);
        Assert.Equal(90, score.Tempo.BeatsPerMinute);
    }

    [Fact]
    public void Process_OemerOutputWithStraySound_LeavesNoSoundUnderThePart()
    {
        byte[] oemerOutput = File.ReadAllBytes(Fixture(ExcerptFixture));

        var result = processor.Process(oemerOutput);

        var part = ReadScore(result).Root!.Elements().Single(element => element.Name.LocalName == "part");
        Assert.DoesNotContain(part.Elements(), element => element.Name.LocalName == "sound");
        var firstMeasure = part.Elements().First(element => element.Name.LocalName == "measure");
        Assert.Contains(firstMeasure.Elements(), element => element.Name.LocalName == "sound");
    }

    [Fact]
    public void Process_OemerOutputWithUnderfullAndOverfullMeasures_FlagsThem()
    {
        byte[] oemerOutput = File.ReadAllBytes(Fixture(ExcerptFixture));

        var result = processor.Process(oemerOutput);

        Assert.Equal([1, 3], result.SuspectMeasures);
    }

    [Fact]
    public void Process_MeasuresThatFillTheBar_FlagsNothing()
    {
        var result = processor.Process(CreateScore("""
            <measure number="1">
              <attributes><divisions>4</divisions><staves>2</staves></attributes>
              <note><pitch><step>C</step><octave>5</octave></pitch><duration>16</duration><staff>1</staff></note>
              <backup><duration>16</duration></backup>
              <note><pitch><step>C</step><octave>3</octave></pitch><duration>8</duration><staff>2</staff></note>
              <note><pitch><step>G</step><octave>3</octave></pitch><duration>8</duration><staff>2</staff></note>
            </measure>
            """));

        Assert.Empty(result.SuspectMeasures);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(8, true)]
    [InlineData(4, true)]
    public void Process_ThreeFourTime_ComparesTheStaffWithTheDeclaredBar(int staffDuration, bool isFlagged)
    {
        string notes = string.Concat(Enumerable.Repeat(
            "<note><pitch><step>C</step><octave>4</octave></pitch><duration>2</duration></note>",
            staffDuration / 2));
        var result = processor.Process(CreateScore($"""
            <measure number="1">
              <attributes><divisions>2</divisions><time><beats>3</beats><beat-type>4</beat-type></time></attributes>
              {notes}
            </measure>
            """));

        int[] expected = isFlagged ? [1] : [];
        Assert.Equal(expected, result.SuspectMeasures);
    }

    [Fact]
    public void Process_TwoVoicesOnOneStaff_DoesNotCountTheOverlapTwice()
    {
        var result = processor.Process(CreateScore("""
            <measure number="1">
              <attributes><divisions>4</divisions></attributes>
              <note><pitch><step>E</step><octave>5</octave></pitch><duration>16</duration><voice>1</voice></note>
              <backup><duration>16</duration></backup>
              <note><pitch><step>C</step><octave>5</octave></pitch><duration>8</duration><voice>2</voice></note>
              <note><pitch><step>G</step><octave>4</octave></pitch><duration>8</duration><voice>2</voice></note>
            </measure>
            """));

        Assert.Empty(result.SuspectMeasures);
    }

    [Fact]
    public void Process_ImplicitFirstMeasure_IsNotFlaggedAsPartial()
    {
        var result = processor.Process(CreateScore("""
            <measure number="0" implicit="yes">
              <attributes><divisions>4</divisions></attributes>
              <note><pitch><step>G</step><octave>4</octave></pitch><duration>4</duration></note>
            </measure>
            <measure number="1">
              <note><pitch><step>C</step><octave>5</octave></pitch><duration>16</duration></note>
            </measure>
            """));

        Assert.Empty(result.SuspectMeasures);
    }

    [Fact]
    public void Process_ScoreTheReaderRejects_ThrowsTheReadersError()
    {
        byte[] timewise = Encoding.UTF8.GetBytes("<score-timewise />");

        Assert.Throws<NotSupportedException>(() => processor.Process(timewise));
    }

    [Fact]
    public void Process_MalformedXml_ThrowsReadableError()
    {
        byte[] malformed = Encoding.UTF8.GetBytes("<score-partwise><part>");

        var exception = Assert.Throws<InvalidDataException>(() => processor.Process(malformed));

        Assert.Contains("alternate recognizer", exception.Message);
    }

    private static byte[] CreateScore(string measures) => Encoding.UTF8.GetBytes($"""
        <score-partwise version="4.0">
          <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
          <part id="P1">
            {measures}
          </part>
        </score-partwise>
        """);

    private static XDocument ReadScore(OemerPostProcessResult result)
    {
        using var source = new MemoryStream(result.CompressedMusicXml);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        using var score = archive.GetEntry("score.xml")!.Open();
        return XDocument.Load(score);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
