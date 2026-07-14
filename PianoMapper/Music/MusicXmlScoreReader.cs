using System.Collections.Frozen;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PianoMapper.Music;

internal sealed class MusicXmlScoreReader
{
    private const string ChordElementName = "chord";
    private const string DotElementName = "dot";
    private const string DurationElementName = "duration";
    private const string NotationsElementName = "notations";
    private const string PitchElementName = "pitch";
    private const string RestElementName = "rest";
    private const string SoundElementName = "sound";
    private const string StaffElementName = "staff";
    private const string TieElementName = "tie";
    private const string TypeElementName = "type";
    private const string VoiceElementName = "voice";

    private static readonly FrozenSet<string> IgnoredPresentationElements = new[]
    {
        "work",
        "identification",
        "defaults",
        "credit",
        "print",
        "bar-style",
        "clef",
        "lyric",
        "dynamics",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IgnoredDirectionElements = new[]
    {
        "direction-type",
        "footnote",
        "level",
        StaffElementName,
        VoiceElementName,
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SupportedNoteElements = new[]
    {
        PitchElementName,
        RestElementName,
        DurationElementName,
        TypeElementName,
        DotElementName,
        StaffElementName,
        VoiceElementName,
        ChordElementName,
        TieElementName,
        NotationsElementName,
    }.ToFrozenSet(StringComparer.Ordinal);

    public Score Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        XDocument document;
        try
        {
            document = XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"Could not parse MusicXML: {exception.Message}", exception);
        }

        var root = document.Root ?? throw new InvalidDataException("Could not parse MusicXML: document has no root element.");
        if (root.Name.LocalName != "score-partwise")
        {
            throw Unsupported(root.Name.LocalName);
        }

        ValidateRootElements(root);
        var parts = root.Elements().Where(element => element.Name.LocalName == "part").ToArray();
        if (parts.Length != 1)
        {
            throw new NotSupportedException("Unsupported MusicXML element <part>: exactly one part is required.");
        }

        int divisions = 1;
        int keyFifths = 0;
        var timeSignature = new TimeSignature(4, new NoteValue(4));
        var tempo = new Tempo(120);
        bool timeSpecified = false;
        bool tempoSpecified = false;
        var measures = new List<ScoreMeasure>();

        int measureIndex = 0;
        foreach (var measureElement in parts[0].Elements())
        {
            if (measureElement.Name.LocalName != "measure")
            {
                throw Unsupported(measureElement.Name.LocalName);
            }

            var notes = new List<ScoreNote>();
            var rests = new List<ScoreRest>();
            int cursorDivisions = 0;
            int lastNoteOnsetDivisions = 0;

            foreach (var element in measureElement.Elements())
            {
                string name = element.Name.LocalName;
                if (IgnoredPresentationElements.Contains(name))
                {
                    continue;
                }

                switch (name)
                {
                    case "attributes":
                        ParseAttributes(element, ref divisions, ref keyFifths, ref timeSignature, ref timeSpecified);
                        break;
                    case "direction":
                        if (ParseTempo(element) is { } parsedTempo)
                        {
                            if (tempoSpecified && parsedTempo != tempo)
                            {
                                throw Unsupported(SoundElementName);
                            }

                            tempo = parsedTempo;
                            tempoSpecified = true;
                        }

                        break;
                    case "barline":
                        ValidateBarline(element);
                        break;
                    case "note":
                        ParseNote(
                            element,
                            measureIndex,
                            divisions,
                            timeSignature,
                            ref cursorDivisions,
                            ref lastNoteOnsetDivisions,
                            notes,
                            rests);
                        break;
                    case "backup":
                        cursorDivisions -= ParsePositiveInt(RequiredChild(element, "duration"), "duration");
                        if (cursorDivisions < 0)
                        {
                            throw new InvalidDataException("MusicXML <backup> moves before the start of the measure.");
                        }

                        break;
                    case "forward":
                        cursorDivisions += ParsePositiveInt(RequiredChild(element, "duration"), "duration");
                        break;
                    default:
                        throw Unsupported(name);
                }
            }

            measures.Add(new ScoreMeasure(notes, rests));
            measureIndex++;
        }

        return new Score(Path.GetFileNameWithoutExtension(path), timeSignature, tempo, keyFifths, measures);
    }

    private static void ValidateRootElements(XElement root)
    {
        foreach (var element in root.Elements())
        {
            string name = element.Name.LocalName;
            if (name is "part-list" or "part" or "movement-title" || IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            throw Unsupported(name);
        }
    }

    private static void ParseAttributes(
        XElement attributes,
        ref int divisions,
        ref int keyFifths,
        ref TimeSignature timeSignature,
        ref bool timeSpecified)
    {
        foreach (var element in attributes.Elements())
        {
            string name = element.Name.LocalName;
            if (IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            switch (name)
            {
                case "divisions":
                    divisions = ParsePositiveInt(element, "divisions");
                    break;
                case "key":
                    keyFifths = ParseInt(RequiredChild(element, "fifths"), "fifths");
                    break;
                case "time":
                    int numerator = ParsePositiveInt(RequiredChild(element, "beats"), "beats");
                    int denominator = ParsePositiveInt(RequiredChild(element, "beat-type"), "beat-type");
                    var parsedTimeSignature = new TimeSignature(numerator, new NoteValue(denominator));
                    if (timeSpecified && parsedTimeSignature != timeSignature)
                    {
                        throw Unsupported("time");
                    }

                    timeSignature = parsedTimeSignature;
                    timeSpecified = true;
                    break;
                case "staves":
                    int staffCount = ParsePositiveInt(element, "staves");
                    if (staffCount > 2)
                    {
                        throw Unsupported("staves");
                    }

                    break;
                default:
                    throw Unsupported(name);
            }
        }
    }

    private static void ValidateBarline(XElement barline)
    {
        foreach (var element in barline.Elements())
        {
            if (!IgnoredPresentationElements.Contains(element.Name.LocalName))
            {
                throw Unsupported(element.Name.LocalName);
            }
        }
    }

    private static Tempo? ParseTempo(XElement direction)
    {
        XElement? sound = null;
        foreach (var element in direction.Elements())
        {
            string name = element.Name.LocalName;
            if (IgnoredDirectionElements.Contains(name) || IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            if (name != SoundElementName || sound is not null)
            {
                throw Unsupported(name);
            }

            sound = element;
        }

        if (sound is null)
        {
            return null;
        }

        foreach (var attribute in sound.Attributes())
        {
            if (attribute.Name.LocalName != "tempo")
            {
                throw Unsupported($"{SoundElementName}@{attribute.Name.LocalName}");
            }
        }

        var unsupportedChild = sound.Elements().FirstOrDefault();
        if (unsupportedChild is not null)
        {
            throw Unsupported(unsupportedChild.Name.LocalName);
        }

        var tempoAttribute = sound.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "tempo");
        if (tempoAttribute is null)
        {
            return null;
        }

        if (!double.TryParse(tempoAttribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double beatsPerMinute))
        {
            throw new InvalidDataException($"Invalid MusicXML tempo '{tempoAttribute.Value}'.");
        }

        return new Tempo(beatsPerMinute);
    }

    private static void ParseNote(
        XElement noteElement,
        int measureIndex,
        int divisions,
        TimeSignature timeSignature,
        ref int cursorDivisions,
        ref int lastNoteOnsetDivisions,
        ICollection<ScoreNote> notes,
        ICollection<ScoreRest> rests)
    {
        ValidateNoteElements(noteElement);
        int durationDivisions = ParsePositiveInt(RequiredChild(noteElement, DurationElementName), DurationElementName);
        var noteValue = ParseNoteValue(noteElement);
        ValidateDuration(durationDivisions, divisions, noteValue);
        bool isChord = FindChild(noteElement, ChordElementName) is not null;
        int onsetDivisions = isChord ? lastNoteOnsetDivisions : cursorDivisions;
        double beatOffset = DivisionsToBeats(onsetDivisions, divisions, timeSignature);
        Staff staff = ParseStaff(noteElement);

        if (FindChild(noteElement, RestElementName) is not null)
        {
            rests.Add(new ScoreRest(noteValue, measureIndex, beatOffset, staff));
        }
        else
        {
            var pitchElement = RequiredChild(noteElement, PitchElementName);
            notes.Add(new ScoreNote(
                ParsePitch(pitchElement),
                noteValue,
                measureIndex,
                beatOffset,
                staff,
                TiesToNext: HasTieStart(noteElement)));
        }

        if (!isChord)
        {
            lastNoteOnsetDivisions = cursorDivisions;
            cursorDivisions += durationDivisions;
        }
    }

    private static void ValidateNoteElements(XElement noteElement)
    {
        foreach (var element in noteElement.Elements())
        {
            string name = element.Name.LocalName;
            if (SupportedNoteElements.Contains(name) ||
                IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            throw Unsupported(name);
        }
    }

    private static Staff ParseStaff(XElement noteElement)
    {
        var staffElement = FindChild(noteElement, StaffElementName);
        if (staffElement is null)
        {
            return Staff.Treble;
        }

        return ParsePositiveInt(staffElement, StaffElementName) switch
        {
            1 => Staff.Treble,
            2 => Staff.Bass,
            _ => throw Unsupported(StaffElementName),
        };
    }

    private static bool HasTieStart(XElement noteElement)
    {
        foreach (var tie in noteElement.Elements().Where(element => element.Name.LocalName == TieElementName))
        {
            string type = tie.Attribute("type")?.Value ?? string.Empty;
            if (type == "start")
            {
                return true;
            }

            if (type != "stop")
            {
                throw new InvalidDataException($"Invalid MusicXML tie type '{type}'.");
            }
        }

        var notations = FindChild(noteElement, NotationsElementName);
        if (notations is null)
        {
            return false;
        }

        bool hasStart = false;
        foreach (var notation in notations.Elements())
        {
            string name = notation.Name.LocalName;
            if (IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            if (name != "tied")
            {
                throw Unsupported(name);
            }

            string type = notation.Attribute("type")?.Value ?? string.Empty;
            if (type == "start")
            {
                hasStart = true;
            }
            else if (type != "stop")
            {
                throw new InvalidDataException($"Invalid MusicXML tied type '{type}'.");
            }
        }

        return hasStart;
    }

    private static Pitch ParsePitch(XElement pitchElement)
    {
        string step = RequiredChild(pitchElement, "step").Value;
        if (!Enum.TryParse(step, ignoreCase: true, out NoteLetter letter))
        {
            throw new InvalidDataException($"Invalid MusicXML pitch step '{step}'.");
        }

        int alter = FindChild(pitchElement, "alter") is { } alterElement ? ParseInt(alterElement, "alter") : 0;
        int octave = ParseInt(RequiredChild(pitchElement, "octave"), "octave");
        return new Pitch(letter, alter, octave);
    }

    private static NoteValue ParseNoteValue(XElement noteElement)
    {
        string type = RequiredChild(noteElement, "type").Value;
        int denominator = type switch
        {
            "whole" => 1,
            "half" => 2,
            "quarter" => 4,
            "eighth" => 8,
            "16th" => 16,
            _ => throw new NotSupportedException($"Unsupported MusicXML note type '{type}'."),
        };
        int dots = noteElement.Elements().Count(element => element.Name.LocalName == "dot");
        return new NoteValue(denominator, dots);
    }

    private static void ValidateDuration(int durationDivisions, int divisions, NoteValue noteValue)
    {
        double actualQuarterNotes = (double)durationDivisions / divisions;
        double expectedQuarterNotes = 4.0 * (2.0 - (1.0 / Math.Pow(2.0, noteValue.Dots))) / noteValue.Denominator;
        if (Math.Abs(actualQuarterNotes - expectedQuarterNotes) > 1e-9)
        {
            throw new InvalidDataException(
                $"MusicXML duration {durationDivisions} does not match note type 1/{noteValue.Denominator} with {noteValue.Dots} dot(s).");
        }
    }

    private static double DivisionsToBeats(int cursorDivisions, int divisions, TimeSignature timeSignature) =>
        ((double)cursorDivisions / divisions) * (timeSignature.BeatNoteValue.Denominator / 4.0);

    private static XElement RequiredChild(XElement parent, string localName) =>
        FindChild(parent, localName) ??
        throw new InvalidDataException($"MusicXML element <{parent.Name.LocalName}> requires <{localName}>.");

    private static XElement? FindChild(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static int ParsePositiveInt(XElement element, string name)
    {
        int value = ParseInt(element, name);
        if (value <= 0)
        {
            throw new InvalidDataException($"MusicXML <{name}> must be positive.");
        }

        return value;
    }

    private static int ParseInt(XElement element, string name)
    {
        if (!int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidDataException($"Invalid MusicXML <{name}> value '{element.Value}'.");
        }

        return value;
    }

    private static NotSupportedException Unsupported(string elementName) =>
        new($"Unsupported MusicXML element <{elementName}>.");
}
