using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PianoMapper.Music;

namespace PianoMapper.Server.Omr;

internal sealed class AudiverisMusicXmlNormalizer
{
    private const string ContainerEntryName = "META-INF/container.xml";
    private const string ScoreEntryName = "score.xml";

    internal byte[] Normalize(byte[] compressedMusicXml)
    {
        ArgumentNullException.ThrowIfNull(compressedMusicXml);

        var score = LoadScore(compressedMusicXml);
        int divisions = 1;
        foreach (var measure in score.Descendants().Where(element => element.Name.LocalName == "measure"))
        {
            var divisionsElement = measure
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName == "attributes")?
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName == "divisions");
            if (divisionsElement is not null)
            {
                divisions = ParsePositiveInt(divisionsElement, "divisions");
            }

            RemovePresentationOnlyElements(measure);
            if (NormalizeFingeringTuplets(measure, divisions))
            {
                RebuildMonophonicStaffTimelines(measure);
            }
        }

        byte[] normalized = SaveScore(score);
        using var validationStream = new MemoryStream(normalized);
        _ = new MusicXmlScoreReader().Read(validationStream, "recognized.mxl");
        return normalized;
    }

    private static XDocument LoadScore(byte[] compressedMusicXml)
    {
        using var source = new MemoryStream(compressedMusicXml);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        var containerEntry = archive.GetEntry(ContainerEntryName) ??
            throw new InvalidDataException($"Audiveris output does not contain {ContainerEntryName}.");
        XDocument container;
        using (var containerStream = containerEntry.Open())
        {
            container = XDocument.Load(containerStream);
        }

        string? scorePath = container
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "rootfile")?
            .Attribute("full-path")?
            .Value;
        if (string.IsNullOrWhiteSpace(scorePath))
        {
            throw new InvalidDataException("Audiveris output does not declare a MusicXML score.");
        }

        var scoreEntry = archive.GetEntry(scorePath) ??
            throw new InvalidDataException($"Audiveris output does not contain '{scorePath}'.");
        using var scoreStream = scoreEntry.Open();
        return XDocument.Load(scoreStream);
    }

    private static void RemovePresentationOnlyElements(XElement measure)
    {
        foreach (var staffDetails in measure
                     .Descendants()
                     .Where(element => element.Name.LocalName == "staff-details")
                     .ToArray())
        {
            staffDetails.Remove();
        }

        foreach (var direction in measure
                     .Elements()
                     .Where(element => element.Name.LocalName == "direction")
                     .ToArray())
        {
            var sound = direction.Elements().FirstOrDefault(element => element.Name.LocalName == "sound");
            var tempo = sound?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "tempo");
            if (tempo is null)
            {
                direction.Remove();
                continue;
            }

            foreach (var attribute in sound!.Attributes().Where(attribute => attribute != tempo).ToArray())
            {
                attribute.Remove();
            }

            sound.RemoveNodes();
        }
    }

    private static bool NormalizeFingeringTuplets(XElement measure, int divisions)
    {
        bool normalized = false;
        foreach (var staffNotes in GetNotesByStaff(measure))
        {
            XElement[] orderedNotes = staffNotes.ToArray();
            for (int index = 0; index <= orderedNotes.Length - 3; index++)
            {
                XElement[] sequence = orderedNotes.Skip(index).Take(3).ToArray();
                if (!IsFingeringTupletSequence(sequence))
                {
                    continue;
                }

                foreach (var note in sequence)
                {
                    SetCanonicalDuration(note, divisions);
                    RemoveChild(note, "time-modification");
                    RemoveTupletNotation(note);
                }

                AddFingering(sequence[1], staffNotes.Key == 1 ? "above" : "below");
                normalized = true;
                index += 2;
            }
        }

        return normalized;
    }

    private static bool IsFingeringTupletSequence(IReadOnlyList<XElement> notes) =>
        HasTupletType(notes[0], "start") &&
        HasTupletType(notes[2], "stop") &&
        notes[1].Elements().Any(element => element.Name.LocalName == "pitch") &&
        notes.All(IsUnbeamedQuarterTriplet);

    private static bool IsUnbeamedQuarterTriplet(XElement note)
    {
        bool isQuarter = Child(note, "type")?.Value.Trim() == "quarter";
        var timeModification = Child(note, "time-modification");
        return isQuarter &&
            !note.Elements().Any(element => element.Name.LocalName == "beam") &&
            Child(timeModification, "actual-notes")?.Value.Trim() == "3" &&
            Child(timeModification, "normal-notes")?.Value.Trim() == "2";
    }

    private static bool HasTupletType(XElement note, string type) =>
        Child(note, "notations")?
            .Elements()
            .Any(element =>
                element.Name.LocalName == "tuplet" &&
                element.Attribute("type")?.Value == type) == true;

    private static void SetCanonicalDuration(XElement note, int divisions)
    {
        var duration = Child(note, "duration") ??
            throw new InvalidDataException("Audiveris MusicXML note has no duration.");
        int baseDuration = Child(note, "type")?.Value.Trim() switch
        {
            "whole" => divisions * 4,
            "half" => divisions * 2,
            "quarter" => divisions,
            "eighth" when divisions % 2 == 0 => divisions / 2,
            "16th" when divisions % 4 == 0 => divisions / 4,
            var type => throw new NotSupportedException($"Unsupported Audiveris note type '{type}'."),
        };
        int canonicalDuration = baseDuration;
        int dotValue = baseDuration;
        foreach (var _ in note.Elements().Where(element => element.Name.LocalName == "dot"))
        {
            dotValue /= 2;
            canonicalDuration += dotValue;
        }

        duration.Value = canonicalDuration.ToString(CultureInfo.InvariantCulture);
    }

    private static void RemoveTupletNotation(XElement note)
    {
        var notations = Child(note, "notations");
        foreach (var tuplet in notations?
                     .Elements()
                     .Where(element => element.Name.LocalName == "tuplet")
                     .ToArray() ?? [])
        {
            tuplet.Remove();
        }

        if (notations is not null && !notations.Elements().Any())
        {
            notations.Remove();
        }
    }

    private static void AddFingering(XElement note, string placement)
    {
        XNamespace ns = note.Name.Namespace;
        var notations = Child(note, "notations");
        if (notations is null)
        {
            notations = new XElement(ns + "notations");
            note.Add(notations);
        }

        var technical = notations.Elements().FirstOrDefault(element => element.Name.LocalName == "technical");
        if (technical is null)
        {
            technical = new XElement(ns + "technical");
            notations.Add(technical);
        }

        technical.Add(new XElement(ns + "fingering", new XAttribute("placement", placement), "3"));
    }

    private static void RebuildMonophonicStaffTimelines(XElement measure)
    {
        var notesByStaff = GetNotesByStaff(measure).ToArray();
        var timingElements = measure.Elements()
            .Where(element => element.Name.LocalName is "note" or "backup" or "forward")
            .ToArray();
        foreach (var element in timingElements)
        {
            element.Remove();
        }

        XNamespace ns = measure.Name.Namespace;
        var rebuilt = new List<XElement>();
        for (int staffIndex = 0; staffIndex < notesByStaff.Length; staffIndex++)
        {
            int staffDuration = 0;
            foreach (var onsetGroup in notesByStaff[staffIndex].GroupBy(GetHorizontalPosition))
            {
                XElement[] notes = onsetGroup.ToArray();
                RemoveChild(notes[0], "chord");
                for (int chordIndex = 1; chordIndex < notes.Length; chordIndex++)
                {
                    RemoveChild(notes[chordIndex], "chord");
                    notes[chordIndex].AddFirst(new XElement(ns + "chord"));
                }

                staffDuration += ParsePositiveInt(Child(notes[0], "duration")!, "duration");
                rebuilt.AddRange(notes);
            }

            if (staffIndex < notesByStaff.Length - 1)
            {
                rebuilt.Add(new XElement(
                    ns + "backup",
                    new XElement(ns + "duration", staffDuration)));
            }
        }

        var rightBarline = measure.Elements().FirstOrDefault(element =>
            element.Name.LocalName == "barline" &&
            element.Attribute("location")?.Value != "left");
        if (rightBarline is null)
        {
            measure.Add(rebuilt);
        }
        else
        {
            rightBarline.AddBeforeSelf(rebuilt);
        }
    }

    private static IEnumerable<IGrouping<int, XElement>> GetNotesByStaff(XElement measure) =>
        measure.Elements()
            .Where(element => element.Name.LocalName == "note")
            .GroupBy(note => Child(note, "staff") is { } staff
                ? ParsePositiveInt(staff, "staff")
                : 1)
            .OrderBy(group => group.Key)
            .Select(group => new OrderedGrouping<int, XElement>(
                group.Key,
                group.OrderBy(GetHorizontalPosition)));

    private static double GetHorizontalPosition(XElement note)
    {
        string? value = note.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "default-x")?
            .Value;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double position))
        {
            throw new InvalidDataException("Audiveris MusicXML note has no valid default-x position.");
        }

        return position;
    }

    private static void RemoveChild(XElement parent, string name) => Child(parent, name)?.Remove();

    private static XElement? Child(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == name);

    private static int ParsePositiveInt(XElement element, string name)
    {
        if (!int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new InvalidDataException($"Invalid Audiveris MusicXML <{name}> value '{element.Value}'.");
        }

        return value;
    }

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
            WriteEntry(archive, ContainerEntryName, containerXml);
            var scoreEntry = archive.CreateEntry(ScoreEntryName);
            using var scoreStream = scoreEntry.Open();
            score.Save(scoreStream);
        }

        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(contents);
    }

    private sealed class OrderedGrouping<TKey, TElement>(
        TKey key,
        IEnumerable<TElement> elements) : List<TElement>(elements), IGrouping<TKey, TElement>
    {
        public TKey Key { get; } = key;
    }
}
