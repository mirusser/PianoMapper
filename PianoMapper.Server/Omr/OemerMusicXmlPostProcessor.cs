using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PianoMapper.Music;

namespace PianoMapper.Server.Omr;

/// <summary>
/// Makes oemer's MusicXML importable and says which measures to distrust. It is separate from
/// <see cref="AudiverisMusicXmlNormalizer"/> because oemer's output has no layout positions and a different set of
/// defects: a tempo placed directly under <c>&lt;part&gt;</c>, and rhythm it cannot close into whole bars.
/// </summary>
internal sealed class OemerMusicXmlPostProcessor
{
    private const string ContainerEntryName = "META-INF/container.xml";
    private const string ScoreEntryName = "score.xml";
    private const string AttributesElementName = "attributes";
    private const string BackupElementName = "backup";
    private const string ChordElementName = "chord";
    private const string DurationElementName = "duration";
    private const string ForwardElementName = "forward";
    private const string MeasureElementName = "measure";
    private const string NoteElementName = "note";
    private const string PartElementName = "part";
    private const string SoundElementName = "sound";
    private const string StaffElementName = "staff";
    private const string DefaultStaff = "1";
    private const int DefaultBeats = 4;
    private const int DefaultBeatType = 4;
    private const int QuarterNotesPerWholeNote = 4;
    private const double DurationTolerance = 1e-9;

    internal OemerPostProcessResult Process(byte[] musicXml)
    {
        ArgumentNullException.ThrowIfNull(musicXml);

        XDocument score = LoadScore(musicXml);
        XElement root = score.Root ??
            throw new InvalidDataException("The alternate recognizer produced an empty MusicXML document.");
        MoveStraySoundIntoFirstMeasure(root);
        int[] suspectMeasures = FindSuspectMeasures(root);

        byte[] compressed = SaveScore(score);
        using var validationStream = new MemoryStream(compressed);
        _ = new MusicXmlScoreReader().Read(validationStream, "recognized.mxl");
        return new OemerPostProcessResult(compressed, suspectMeasures);
    }

    private static XDocument LoadScore(byte[] musicXml)
    {
        try
        {
            using var stream = new MemoryStream(musicXml);
            return XDocument.Load(stream);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                $"The alternate recognizer produced MusicXML that could not be parsed: {exception.Message}",
                exception);
        }
    }

    // oemer writes <sound tempo="..."/> as a child of <part>, which MusicXML only allows inside a measure. Its tempo is
    // a fixed default rather than something it read, so moving it keeps oemer's value without claiming more.
    private static void MoveStraySoundIntoFirstMeasure(XElement root)
    {
        foreach (var part in Children(root, PartElementName))
        {
            XElement[] straySounds = Children(part, SoundElementName).ToArray();
            foreach (var sound in straySounds)
            {
                sound.Remove();
            }

            var tempoSound = straySounds.FirstOrDefault(sound => sound.Attribute("tempo") is not null);
            var firstMeasure = Children(part, MeasureElementName).FirstOrDefault();
            if (tempoSound is null || firstMeasure is null)
            {
                continue;
            }

            var attributes = Children(firstMeasure, AttributesElementName).FirstOrDefault();
            if (attributes is null)
            {
                firstMeasure.AddFirst(tempoSound);
            }
            else
            {
                attributes.AddAfterSelf(tempoSound);
            }
        }
    }

    private static int[] FindSuspectMeasures(XElement root)
    {
        var suspectMeasures = new SortedSet<int>();
        foreach (var part in Children(root, PartElementName))
        {
            int divisions = 0;
            int beats = DefaultBeats;
            int beatType = DefaultBeatType;
            int position = 0;
            foreach (var measure in Children(part, MeasureElementName))
            {
                position++;
                ReadAttributes(measure, ref divisions, ref beats, ref beatType);
                bool isPartialByDesign = measure.Attribute("implicit")?.Value == "yes";
                if (!isPartialByDesign && divisions > 0 &&
                    HasStaffThatDoesNotFillTheBar(measure, divisions * QuarterNotesPerWholeNote * (double)beats / beatType))
                {
                    suspectMeasures.Add(position);
                }
            }
        }

        return [.. suspectMeasures];
    }

    private static void ReadAttributes(XElement measure, ref int divisions, ref int beats, ref int beatType)
    {
        foreach (var attributes in Children(measure, AttributesElementName))
        {
            if (Child(attributes, "divisions") is { } divisionsElement &&
                int.TryParse(divisionsElement.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDivisions))
            {
                divisions = parsedDivisions;
            }

            if (Child(attributes, "time") is { } time &&
                int.TryParse(Child(time, "beats")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedBeats) &&
                int.TryParse(Child(time, "beat-type")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedBeatType) &&
                parsedBeats > 0 &&
                parsedBeatType > 0)
            {
                beats = parsedBeats;
                beatType = parsedBeatType;
            }
        }
    }

    // Follows the MusicXML cursor: a note starts where the cursor is, a chord member starts with the note before it,
    // and <backup>/<forward> move the cursor. Each staff must then cover the bar from its start to its end, however many
    // voices share it; a staff with no notes at all is left alone.
    private static bool HasStaffThatDoesNotFillTheBar(XElement measure, double barLength)
    {
        var spansByStaff = new Dictionary<string, List<(double Start, double End)>>(StringComparer.Ordinal);
        double cursor = 0;
        double lastStart = 0;
        foreach (var element in measure.Elements())
        {
            double duration = ParseDuration(element);
            switch (element.Name.LocalName)
            {
                case NoteElementName:
                    bool isChordMember = Child(element, ChordElementName) is not null;
                    double start = isChordMember ? lastStart : cursor;
                    if (!isChordMember)
                    {
                        lastStart = cursor;
                        cursor += duration;
                    }

                    if (duration > 0)
                    {
                        string staff = Child(element, StaffElementName)?.Value.Trim() ?? DefaultStaff;
                        if (!spansByStaff.TryGetValue(staff, out var spans))
                        {
                            spans = [];
                            spansByStaff[staff] = spans;
                        }

                        spans.Add((start, start + duration));
                    }

                    break;
                case BackupElementName:
                    cursor -= duration;
                    break;
                case ForwardElementName:
                    cursor += duration;
                    break;
            }
        }

        return spansByStaff.Values.Any(spans => !CoversBar(spans, barLength));
    }

    private static bool CoversBar(List<(double Start, double End)> spans, double barLength)
    {
        spans.Sort();
        if (spans[0].Start < -DurationTolerance)
        {
            return false;
        }

        double covered = 0;
        double reach = 0;
        foreach (var (start, end) in spans)
        {
            covered += Math.Max(0, end - Math.Max(start, reach));
            reach = Math.Max(reach, end);
        }

        return reach <= barLength + DurationTolerance && covered >= barLength - DurationTolerance;
    }

    private static double ParseDuration(XElement element) =>
        double.TryParse(Child(element, DurationElementName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double duration)
            ? duration
            : 0;

    private static IEnumerable<XElement> Children(XElement parent, string name) =>
        parent.Elements().Where(element => element.Name.LocalName == name);

    private static XElement? Child(XElement parent, string name) => Children(parent, name).FirstOrDefault();

    private static byte[] SaveScore(XDocument score)
    {
        const string containerXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <container>
              <rootfiles>
                <rootfile full-path="score.xml" media-type="application/vnd.recordare.musicxml+xml" />
              </rootfiles>
            </container>
            """;
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var containerEntry = archive.CreateEntry(ContainerEntryName);
            using (var writer = new StreamWriter(containerEntry.Open(), Encoding.UTF8))
            {
                writer.Write(containerXml);
            }

            var scoreEntry = archive.CreateEntry(ScoreEntryName);
            using var scoreStream = scoreEntry.Open();
            score.Save(scoreStream);
        }

        return output.ToArray();
    }
}
