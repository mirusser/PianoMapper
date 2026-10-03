using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace PianoMapper.Music;

// <direction> (dynamics, words, hairpins, pedal marks, tempo, rehearsal, segno and coda) and <harmony> (chord symbols),
// kept as the measure's <see cref="ScoreDirection"/>s. Marks with no drawing here are reported as warnings.
public sealed partial class MusicXmlScoreReader
{
    private const string FrameWarning = "frame";
    private const string HarmonyElementName = "harmony";
    private const string PedalElementName = "pedal";
    private const string PlacementAttributeName = "placement";
    private const string WordsElementName = "words";

    // Returns the quarter-notes-per-minute of the direction's tempo: its <sound tempo>, else its <metronome> mark.
    private static double? ParseDirection(
        XElement direction,
        PartState state,
        MeasureBuilder measure,
        MusicXmlImportWarnings warnings)
    {
        XElement? sound = null;
        int offsetDivisions = 0;
        foreach (var element in direction.Elements())
        {
            string name = element.Name.LocalName;
            if (name == "offset")
            {
                // An offset "for sound only" moves what is heard, not what is printed.
                if (element.Attribute("sound")?.Value != "yes")
                {
                    offsetDivisions = ParseInt(element, "offset");
                }

                continue;
            }

            if (name == DirectionTypeElementName || IgnoredDirectionElements.Contains(name) ||
                IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            if (name != SoundElementName || sound is not null)
            {
                throw Unsupported(name);
            }

            sound = element;
        }

        Staff staff = ResolveStaff(direction, state);
        double beatOffset = GetDirectionBeatOffset(measure, state, offsetDivisions);
        string? directionPlacement = direction.Attribute(PlacementAttributeName)?.Value;
        double? metronomeTempo = null;
        foreach (var directionType in direction.Elements().Where(element => element.Name.LocalName == DirectionTypeElementName))
        {
            foreach (var element in directionType.Elements())
            {
                if (ParseDirectionTypeElement(element, directionPlacement, beatOffset, staff, measure, warnings) is { } tempo)
                {
                    metronomeTempo ??= tempo;
                }
            }
        }

        return (sound is null ? null : ParseSound(sound, state, warnings)) ?? metronomeTempo;
    }

    private static double GetDirectionBeatOffset(MeasureBuilder measure, PartState state, int offsetDivisions) =>
        DivisionsToBeats(
            Math.Max(0, measure.CursorDivisions + offsetDivisions),
            state.Divisions,
            state.TimeSignature);

    // Adds the mark a direction-type child stands for and returns the tempo a <metronome> child carries. The octave shift
    // has its own handling (see ParseOctaveShiftDirective).
    private static double? ParseDirectionTypeElement(
        XElement element,
        string? directionPlacement,
        double beatOffset,
        Staff staff,
        MeasureBuilder measure,
        MusicXmlImportWarnings warnings)
    {
        string name = element.Name.LocalName;
        string? placement = element.Attribute(PlacementAttributeName)?.Value ?? directionPlacement;
        bool isBelowByDefault = name is "dynamics" or "wedge" or PedalElementName;
        bool isBelow = placement is null ? isBelowByDefault : placement == "below";
        void Add(ScoreDirectionKind kind, string text = "", int number = 1) =>
            measure.Directions.Add(new ScoreDirection(kind, beatOffset, staff, text, isBelow, number));

        switch (name)
        {
            case OctaveShiftElementName:
                break;
            case "dynamics":
                foreach (var dynamic in element.Elements())
                {
                    Add(ScoreDirectionKind.Dynamics, dynamic.Name.LocalName == "other-dynamics" ? dynamic.Value.Trim() : dynamic.Name.LocalName);
                }

                break;
            case WordsElementName:
                if (CollapseWhitespace(element.Value) is { Length: > 0 } words)
                {
                    Add(ScoreDirectionKind.Words, words);
                }

                break;
            case "wedge":
                AddWedge(element, Add);
                break;
            case PedalElementName:
                ScoreDirectionKind? pedalKind = element.Attribute("type")?.Value switch
                {
                    "start" => ScoreDirectionKind.PedalStart,
                    "stop" => ScoreDirectionKind.PedalStop,
                    "change" => ScoreDirectionKind.PedalChange,
                    _ => null,
                };
                if (pedalKind is { } kind)
                {
                    Add(kind);
                }
                else
                {
                    warnings.Add(name);
                }

                break;
            case MetronomeElementName:
                if (FormatMetronome(element) is { Length: > 0 } metronomeText)
                {
                    Add(ScoreDirectionKind.Metronome, metronomeText);
                }

                return TryParseMetronome(element);
            case "rehearsal":
                if (CollapseWhitespace(element.Value) is { Length: > 0 } rehearsal)
                {
                    Add(ScoreDirectionKind.Rehearsal, rehearsal);
                }

                break;
            case "segno":
                Add(ScoreDirectionKind.Segno);
                break;
            case "coda":
                Add(ScoreDirectionKind.Coda);
                break;
            default:
                warnings.Add(name);
                break;
        }

        return null;
    }

    private static void AddWedge(XElement wedge, Action<ScoreDirectionKind, string, int> add)
    {
        int number = wedge.Attribute("number") is { } numberAttribute
            ? ParsePositiveAttribute(numberAttribute, "wedge")
            : 1;
        switch (wedge.Attribute("type")?.Value)
        {
            case "crescendo":
                add(ScoreDirectionKind.CrescendoStart, string.Empty, number);
                break;
            case "diminuendo":
                add(ScoreDirectionKind.DiminuendoStart, string.Empty, number);
                break;
            case "stop":
                add(ScoreDirectionKind.WedgeStop, string.Empty, number);
                break;
            case "continue":
                // A hairpin running on across a system break; its start and stop already say where it goes.
                break;
            case var type:
                throw new InvalidDataException($"Invalid MusicXML <wedge> type '{type ?? string.Empty}'.");
        }
    }

    private static string CollapseWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // The printed tempo mark: "♩ = 120", "♪. = 66", or a ratio such as "𝅗𝅥 = ♩". Free-text <metronome-note> forms are
    // not drawn.
    private static string FormatMetronome(XElement metronome)
    {
        var text = new StringBuilder();
        foreach (var element in metronome.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "beat-unit":
                    if (text.Length > 0)
                    {
                        text.Append(" = ");
                    }

                    text.Append(GetBeatUnitGlyph(element.Value.Trim()));
                    break;
                case "beat-unit-dot":
                    text.Append('.');
                    break;
                case "per-minute":
                    text.Append(" = ").Append(element.Value.Trim());
                    break;
            }
        }

        return text.ToString();
    }

    private static string GetBeatUnitGlyph(string beatUnit) => beatUnit switch
    {
        "whole" => "𝅝",
        "half" => "𝅗𝅥",
        "quarter" => "𝅘𝅥",
        "eighth" => "𝅘𝅥𝅮",
        "16th" => "𝅘𝅥𝅯",
        "32nd" => "𝅘𝅥𝅰",
        _ => beatUnit,
    };

    private static void ParseHarmony(XElement harmony, PartState state, MeasureBuilder measure, MusicXmlImportWarnings warnings)
    {
        int offsetDivisions = FindChild(harmony, "offset") is { } offset && offset.Attribute("sound")?.Value != "yes"
            ? ParseInt(offset, "offset")
            : 0;
        if (harmony.Elements().Any(element => element.Name.LocalName == "frame"))
        {
            warnings.Add(FrameWarning);
        }

        if (FormatChordSymbol(harmony) is not { Length: > 0 } text)
        {
            warnings.Add(HarmonyElementName);
            return;
        }

        measure.Directions.Add(new ScoreDirection(
            ScoreDirectionKind.ChordSymbol,
            GetDirectionBeatOffset(measure, state, offsetDivisions),
            ResolveStaff(harmony, state),
            text));
    }

    // The name a chord symbol prints: root (or function), kind, degrees, then a bass note. A <kind> text attribute, even
    // an empty one, replaces the kind's default text.
    private static string FormatChordSymbol(XElement harmony)
    {
        var kind = FindChild(harmony, "kind");
        if (kind?.Value.Trim() == "none")
        {
            // "No chord": the root some exporters still write is not part of what is printed.
            return kind.Attribute("text")?.Value ?? GetDefaultKindText("none");
        }

        var text = new StringBuilder();
        if (FindChild(harmony, "root") is { } root)
        {
            text.Append(FindChild(root, "root-step")?.Value.Trim() ?? string.Empty);
            AppendAlter(text, FindChild(root, "root-alter"));
        }
        else if (FindChild(harmony, "function") is { } function)
        {
            text.Append(function.Value.Trim());
        }
        else
        {
            return string.Empty;
        }

        if (kind is not null)
        {
            text.Append(kind.Attribute("text")?.Value ?? GetDefaultKindText(kind.Value.Trim()));
        }

        AppendDegrees(text, harmony, kind?.Attribute("parentheses-degrees")?.Value == "yes");
        if (FindChild(harmony, "bass") is { } bass)
        {
            text.Append('/').Append(FindChild(bass, "bass-step")?.Value.Trim() ?? string.Empty);
            AppendAlter(text, FindChild(bass, "bass-alter"));
        }

        return text.ToString();
    }

    private static void AppendAlter(StringBuilder text, XElement? alter)
    {
        if (alter is not null && int.TryParse(alter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int semitones))
        {
            text.Append(GetAlterSymbol(semitones));
        }
    }

    private static string GetAlterSymbol(int semitones) => semitones switch
    {
        -2 => "𝄫",
        -1 => "♭",
        1 => "♯",
        2 => "𝄪",
        _ => string.Empty,
    };

    private static void AppendDegrees(StringBuilder text, XElement harmony, bool hasParentheses)
    {
        var degrees = harmony.Elements().Where(element => element.Name.LocalName == "degree").Select(FormatDegree).ToArray();
        if (degrees.Length == 0)
        {
            return;
        }

        string joined = string.Concat(degrees);
        text.Append(hasParentheses ? $"({joined})" : joined);
    }

    private static string FormatDegree(XElement degree)
    {
        var type = FindChild(degree, "degree-type");
        string defaultWord = type?.Value.Trim() switch
        {
            "add" => "add",
            "subtract" => "no",
            _ => string.Empty,
        };
        string word = type?.Attribute("text")?.Value ?? defaultWord;
        int alter = FindChild(degree, "degree-alter") is { } alterElement &&
            int.TryParse(alterElement.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int semitones)
                ? semitones
                : 0;
        return word + GetAlterSymbol(alter) + FindChild(degree, "degree-value")?.Value.Trim();
    }

    private static string GetDefaultKindText(string kind) => kind switch
    {
        "major" => string.Empty,
        "minor" => "m",
        "augmented" => "aug",
        "diminished" => "dim",
        "dominant" => "7",
        "major-seventh" => "maj7",
        "minor-seventh" => "m7",
        "diminished-seventh" => "dim7",
        "augmented-seventh" => "aug7",
        "half-diminished" => "m7♭5",
        "major-minor" => "m(maj7)",
        "major-sixth" => "6",
        "minor-sixth" => "m6",
        "dominant-ninth" => "9",
        "major-ninth" => "maj9",
        "minor-ninth" => "m9",
        "dominant-11th" => "11",
        "major-11th" => "maj11",
        "minor-11th" => "m11",
        "dominant-13th" => "13",
        "major-13th" => "maj13",
        "minor-13th" => "m13",
        "suspended-second" => "sus2",
        "suspended-fourth" => "sus4",
        "Neapolitan" => "N",
        "Italian" => "It+6",
        "French" => "Fr+6",
        "German" => "Ger+6",
        "pedal" => "ped",
        "power" => "5",
        "Tristan" => "Tristan",
        "none" => "N.C.",
        _ => string.Empty,
    };
}
