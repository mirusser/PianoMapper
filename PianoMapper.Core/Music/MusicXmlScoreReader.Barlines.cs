using System.Globalization;
using System.Xml.Linq;

namespace PianoMapper.Music;

// <barline> content (repeats, voltas, styles, fermatas, segno and coda signs) and the jump targets of <sound>.
public sealed partial class MusicXmlScoreReader
{
    private const string BarStyleWarning = "bar-style";
    private const string MidMeasureBarlineWarning = "mid-measure barline";
    private const string JumpInstructionWarning = "jump instruction";
    private const string BarlineElementName = "barline";
    private const string EndingElementName = "ending";

    private static void ParseBarline(XElement barline, MeasureBuilder measure, MusicXmlImportWarnings warnings)
    {
        string location = barline.Attribute("location")?.Value ?? "right";
        if (location == "middle")
        {
            warnings.Add(MidMeasureBarlineWarning);
            return;
        }

        if (location is not ("left" or "right"))
        {
            throw new InvalidDataException($"Invalid MusicXML <{BarlineElementName}> location '{location}'.");
        }

        var parsed = ParseBarlineContent(barline, warnings);
        if (location == "left")
        {
            measure.LeftBarline = MergeBarlines(measure.LeftBarline, parsed);
        }
        else
        {
            measure.RightBarline = MergeBarlines(measure.RightBarline, parsed);
        }
    }

    private static ScoreBarline ParseBarlineContent(XElement barline, MusicXmlImportWarnings warnings)
    {
        var style = ScoreBarlineStyle.Regular;
        ScoreRepeatDirection? repeat = null;
        int repeatTimes = 2;
        ScoreEnding? ending = null;
        ScoreFermata? fermata = null;
        ScoreBarlineMark? mark = null;
        int markCount = 0;
        string? rawStyle = null;
        foreach (var element in barline.Elements())
        {
            string name = element.Name.LocalName;
            switch (name)
            {
                case "bar-style":
                    rawStyle = element.Value.Trim();
                    style = rawStyle switch
                    {
                        "light-light" => ScoreBarlineStyle.Double,
                        "light-heavy" => ScoreBarlineStyle.Final,
                        _ => ScoreBarlineStyle.Regular,
                    };
                    break;
                case RepeatElementName:
                    repeat = ParseRepeatDirection(element);
                    if (element.Attribute("times") is { } times)
                    {
                        repeatTimes = ParsePositiveAttribute(times, RepeatElementName);
                    }

                    break;
                case EndingElementName:
                    ending = ParseEnding(element);
                    break;
                case FermataElementName:
                    fermata ??= ParseFermataElement(element);
                    break;
                case "segno":
                case "coda":
                    var elementMark = name == "segno" ? ScoreBarlineMark.Segno : ScoreBarlineMark.Coda;
                    if (mark is { } existingMark && existingMark != elementMark)
                    {
                        throw new InvalidDataException(
                            "MusicXML <segno> and <coda> cannot share one <barline>.");
                    }

                    mark = elementMark;
                    markCount++;
                    break;
                case "wavy-line":
                    warnings.Add(name);
                    break;
                case "footnote":
                case "level":
                    break;
                default:
                    throw Unsupported(name);
            }
        }

        // Bars with no drawing here: dashed, dotted, short, tick, none and heavy-heavy. heavy-light only makes sense as the
        // thick line of a forward repeat, which is drawn anyway.
        if (rawStyle is not (null or "regular" or "light-light" or "light-heavy") &&
            !(rawStyle == "heavy-light" && repeat is not null))
        {
            warnings.Add(BarStyleWarning);
        }

        return new ScoreBarline(style, repeat, repeatTimes, ending, fermata, mark, Math.Max(markCount, 1));
    }

    private static ScoreRepeatDirection ParseRepeatDirection(XElement repeat) =>
        repeat.Attribute("direction")?.Value switch
        {
            "forward" => ScoreRepeatDirection.Forward,
            "backward" => ScoreRepeatDirection.Backward,
            var value => throw new InvalidDataException(
                $"Invalid MusicXML <{RepeatElementName}> direction '{value ?? string.Empty}'."),
        };

    private static ScoreEnding ParseEnding(XElement ending)
    {
        var type = ending.Attribute("type")?.Value switch
        {
            "start" => ScoreEndingType.Start,
            "stop" => ScoreEndingType.Stop,
            "discontinue" => ScoreEndingType.Discontinue,
            var value => throw new InvalidDataException(
                $"Invalid MusicXML <{EndingElementName}> type '{value ?? string.Empty}'."),
        };
        return new ScoreEnding(ending.Attribute("number")?.Value.Trim() ?? string.Empty, type);
    }

    private static int ParsePositiveAttribute(XAttribute attribute, string elementName)
    {
        if (!int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new InvalidDataException(
                $"Invalid MusicXML <{elementName}> {attribute.Name.LocalName} '{attribute.Value}'.");
        }

        return value;
    }

    // Two <barline> elements can describe the same side of a measure (a repeat in one, a volta end in the other).
    private static ScoreBarline MergeBarlines(ScoreBarline? existing, ScoreBarline added) =>
        existing is null
            ? added
            : new ScoreBarline(
                added.Style != ScoreBarlineStyle.Regular ? added.Style : existing.Style,
                added.Repeat ?? existing.Repeat,
                added.Repeat is null ? existing.RepeatTimes : added.RepeatTimes,
                added.Ending ?? existing.Ending,
                added.Fermata ?? existing.Fermata,
                added.Mark ?? existing.Mark,
                added.Mark is null ? existing.MarkCount : added.MarkCount);

    // <sound> names the targets of jumps and the jumps themselves. Playback here is linear, so the jumps are reported;
    // a jump to a target the file never defines is a malformed file.
    private static void ReadJumpAttributes(XElement sound, PartState state, MusicXmlImportWarnings warnings)
    {
        foreach (var attribute in sound.Attributes())
        {
            string value = attribute.Value;
            switch (attribute.Name.LocalName)
            {
                case "segno" or "coda":
                    state.JumpTargets.Add((attribute.Name.LocalName, value));
                    break;
                case "dalsegno":
                    state.JumpReferences.Add(("segno", value));
                    warnings.Add(JumpInstructionWarning);
                    break;
                case "tocoda":
                    state.JumpReferences.Add(("coda", value));
                    warnings.Add(JumpInstructionWarning);
                    break;
                case "dacapo" or "fine" or "forward-repeat":
                    warnings.Add(JumpInstructionWarning);
                    break;
            }
        }
    }

    private static void ValidateJumpTargets(PartState state)
    {
        foreach (var (kind, id) in state.JumpReferences)
        {
            if (!state.JumpTargets.Contains((kind, id)))
            {
                string jump = kind == "segno" ? "dalsegno" : "tocoda";
                throw new InvalidDataException(
                    $"MusicXML <sound {jump}=\"{id}\"> jumps to a <sound {kind}=\"{id}\"> that the score does not contain.");
            }
        }
    }
}
