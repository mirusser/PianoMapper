using System.IO.Compression;
using System.Text;
using PianoMapper.Music;
using PianoMapper.Server.Omr;

namespace PianoMapper.Tests.UnitTests;

public sealed class AudiverisMusicXmlNormalizerTests
{
    [Fact]
    public void Normalize_AudiverisMistakesFingeringThreesForTuplets_ReturnsStraightNotesWithFingerings()
    {
        byte[] source = CreateCompressedMusicXml("""
            <score-partwise>
              <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>6</divisions>
                    <time><beats>4</beats><beat-type>4</beat-type></time>
                    <staves>2</staves>
                    <staff-details print-object="yes" />
                  </attributes>
                  <note default-x="10">
                    <rest /><duration>4</duration><voice>1</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>1</staff><notations><tuplet type="start" number="1" placement="above" /></notations>
                  </note>
                  <note default-x="20">
                    <pitch><step>G</step><octave>4</octave></pitch>
                    <duration>4</duration><voice>1</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>1</staff>
                  </note>
                  <note default-x="40">
                    <pitch><step>G</step><octave>4</octave></pitch>
                    <duration>6</duration><voice>1</voice><type>quarter</type><staff>1</staff>
                  </note>
                  <backup><duration>14</duration></backup>
                  <forward><duration>6</duration><voice>2</voice><staff>1</staff></forward>
                  <note default-x="30">
                    <rest /><duration>4</duration><voice>2</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>1</staff><notations><tuplet type="stop" number="1" /></notations>
                  </note>
                  <backup><duration>10</duration></backup>
                  <note default-x="10">
                    <pitch><step>C</step><octave>4</octave></pitch>
                    <duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff><notations><tuplet type="start" number="1" placement="below" /></notations>
                  </note>
                  <note default-x="20">
                    <pitch><step>D</step><octave>4</octave></pitch>
                    <duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff>
                  </note>
                  <note default-x="30">
                    <rest /><duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff><notations><tuplet type="stop" number="1" /></notations>
                  </note>
                  <note default-x="40">
                    <pitch><step>E</step><octave>4</octave></pitch>
                    <duration>6</duration><voice>5</voice><type>quarter</type><staff>2</staff>
                  </note>
                  <direction placement="below">
                    <direction-type><dynamics><f /></dynamics></direction-type>
                    <staff>2</staff><sound dynamics="100" />
                  </direction>
                </measure>
              </part>
            </score-partwise>
            """);

        byte[] normalized = new AudiverisMusicXmlNormalizer().Normalize(source);
        using var stream = new MemoryStream(normalized);
        var score = new MusicXmlScoreReader().Read(stream, "recognized.mxl");

        var measure = Assert.Single(score.Measures);
        Assert.Equal(5, measure.Notes.Count);
        Assert.Equal(3, measure.Rests.Count);
        var upperFingering = Assert.Single(
            measure.Notes,
            note => note.Staff == Staff.Treble && note.Fingering is not null).Fingering;
        var lowerFingering = Assert.Single(
            measure.Notes,
            note => note.Staff == Staff.Bass && note.Fingering is not null).Fingering;
        Assert.Equal(new ScoreFingering(3, ScoreFingeringPlacement.Above), upperFingering);
        Assert.Equal(new ScoreFingering(3, ScoreFingeringPlacement.Below), lowerFingering);
        Assert.All(measure.Notes, note => Assert.Equal(Math.Floor(note.BeatOffset), note.BeatOffset));
        Assert.Contains(measure.Notes, note => note.Staff == Staff.Treble && note.BeatOffset == 3);
        Assert.Contains(measure.Notes, note => note.Staff == Staff.Bass && note.BeatOffset == 3);
    }

    [Fact]
    public void Normalize_MeasureHasOnlyAnOrphanedBackup_RemovesItInsteadOfThrowing()
    {
        byte[] source = CreateCompressedMusicXml("""
            <score-partwise>
              <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>1</divisions>
                    <time><beats>4</beats><beat-type>4</beat-type></time>
                  </attributes>
                  <note default-x="10">
                    <pitch><step>C</step><octave>4</octave></pitch>
                    <duration>4</duration><type>whole</type>
                  </note>
                </measure>
                <measure number="2" width="83">
                  <backup><duration>12</duration></backup>
                </measure>
              </part>
            </score-partwise>
            """);

        byte[] normalized = new AudiverisMusicXmlNormalizer().Normalize(source);
        using var stream = new MemoryStream(normalized);
        var score = new MusicXmlScoreReader().Read(stream, "recognized.mxl");

        Assert.Equal(2, score.Measures.Count);
        var phantomMeasure = score.Measures[1];
        Assert.Empty(phantomMeasure.Notes);
        Assert.Empty(phantomMeasure.Rests);
    }

    // Audiveris gives a whole-measure rest no default-x (it has no horizontal position: it fills the
    // measure), unlike every pitched note and ordinary rest it exports.
    [Fact]
    public void Normalize_WholeMeasureRestHasNoDefaultX_KeepsTheRestInsteadOfThrowing()
    {
        byte[] source = CreateCompressedMusicXml("""
            <score-partwise>
              <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>6</divisions>
                    <time><beats>4</beats><beat-type>4</beat-type></time>
                  </attributes>
                  <note>
                    <rest measure="yes" /><duration>24</duration><voice>1</voice>
                  </note>
                </measure>
                <measure number="2">
                  <note default-x="10">
                    <pitch><step>C</step><octave>4</octave></pitch>
                    <duration>24</duration><voice>1</voice><type>whole</type>
                  </note>
                </measure>
              </part>
            </score-partwise>
            """);

        byte[] normalized = new AudiverisMusicXmlNormalizer().Normalize(source);
        using var stream = new MemoryStream(normalized);
        var score = new MusicXmlScoreReader().Read(stream, "recognized.mxl");

        Assert.Equal(2, score.Measures.Count);
        Assert.Empty(score.Measures[0].Notes);
        Assert.Single(score.Measures[0].Rests);
        Assert.Single(score.Measures[1].Notes);
    }

    [Fact]
    public void Normalize_WholeMeasureRestSharesMeasureWithFingeringTuplets_KeepsTheRestAndRebuildsTheTuplets()
    {
        byte[] source = CreateCompressedMusicXml("""
            <score-partwise>
              <part-list><score-part id="P1"><part-name>Piano</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes>
                    <divisions>6</divisions>
                    <time><beats>4</beats><beat-type>4</beat-type></time>
                    <staves>2</staves>
                  </attributes>
                  <note>
                    <rest measure="yes" /><duration>24</duration><voice>1</voice><staff>1</staff>
                  </note>
                  <backup><duration>24</duration></backup>
                  <note default-x="10">
                    <pitch><step>C</step><octave>3</octave></pitch>
                    <duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff><notations><tuplet type="start" number="1" placement="below" /></notations>
                  </note>
                  <note default-x="20">
                    <pitch><step>D</step><octave>3</octave></pitch>
                    <duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff>
                  </note>
                  <note default-x="30">
                    <rest /><duration>4</duration><voice>5</voice><type>quarter</type>
                    <time-modification><actual-notes>3</actual-notes><normal-notes>2</normal-notes></time-modification>
                    <staff>2</staff><notations><tuplet type="stop" number="1" /></notations>
                  </note>
                  <note default-x="40">
                    <pitch><step>E</step><octave>3</octave></pitch>
                    <duration>6</duration><voice>5</voice><type>quarter</type><staff>2</staff>
                  </note>
                </measure>
              </part>
            </score-partwise>
            """);

        byte[] normalized = new AudiverisMusicXmlNormalizer().Normalize(source);
        using var stream = new MemoryStream(normalized);
        var score = new MusicXmlScoreReader().Read(stream, "recognized.mxl");

        var measure = Assert.Single(score.Measures);
        Assert.Single(measure.Rests, rest => rest.Staff == Staff.Treble);
        Assert.Single(measure.Rests, rest => rest.Staff == Staff.Bass);
        Assert.Equal(3, measure.Notes.Count(note => note.Staff == Staff.Bass));
        Assert.All(measure.Notes, note => Assert.Equal(Math.Floor(note.BeatOffset), note.BeatOffset));
        Assert.Single(measure.Notes, note => note.Fingering is not null);
    }

    private static byte[] CreateCompressedMusicXml(string scoreXml)
    {
        const string containerXml = """
            <container>
              <rootfiles>
                <rootfile full-path="score.xml" media-type="application/vnd.recordare.musicxml+xml" />
              </rootfiles>
            </container>
            """;
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "META-INF/container.xml", containerXml);
            WriteEntry(archive, "score.xml", scoreXml);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }
}
