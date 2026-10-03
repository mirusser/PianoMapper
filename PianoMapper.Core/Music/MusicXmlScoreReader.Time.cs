using System.Globalization;
using System.Xml.Linq;

namespace PianoMapper.Music;

// <time>: the meter a measure is counted in.
public sealed partial class MusicXmlScoreReader
{
    private const string AdditiveMeterWarning = "additive meter";
    private const string HiddenTimeSignatureWarning = "hidden time signature";
    private const string TimeElementName = "time";
    private const string TimeSymbolWarning = "time symbol";

    // An additive meter such as 3+2/8 is counted as its sum (5/8), and its printed form is not kept. Pairs that disagree
    // on the beat type (3/4 + 2/8) have no single meter. A common or cut time sign is drawn as numerals.
    private static TimeSignature ParseTime(XElement time, MusicXmlImportWarnings warnings)
    {
        if (FindChild(time, "senza-misura") is not null)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{TimeElementName}>: unmeasured music (senza misura) has no meter.");
        }

        var beatElements = time.Elements().Where(element => element.Name.LocalName == "beats").ToArray();
        var beatTypes = time.Elements()
            .Where(element => element.Name.LocalName == "beat-type")
            .Select(element => ParsePositiveInt(element, "beat-type"))
            .ToArray();
        if (beatElements.Length == 0 || beatTypes.Length != beatElements.Length)
        {
            throw new InvalidDataException($"MusicXML element <{TimeElementName}> requires <beats> and <beat-type> in pairs.");
        }

        if (beatTypes.Distinct().Count() > 1)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{TimeElementName}>: a meter mixing beat types ({string.Join(", ", beatTypes)}).");
        }

        int numerator = 0;
        foreach (var beats in beatElements)
        {
            numerator += beats.Value.Split('+').Sum(part => ParsePositiveMeterNumber(part, beats));
        }

        if (beatElements.Length > 1 || beatElements[0].Value.Contains('+', StringComparison.Ordinal))
        {
            warnings.Add(AdditiveMeterWarning);
        }

        if (time.Attribute("symbol")?.Value is { } symbol and not "normal")
        {
            warnings.Add(TimeSymbolWarning);
        }

        if (time.Attribute(PrintObjectAttributeName)?.Value == "no")
        {
            warnings.Add(HiddenTimeSignatureWarning);
        }

        return new TimeSignature(numerator, new NoteValue(beatTypes[0]));
    }

    private static int ParsePositiveMeterNumber(string text, XElement beats)
    {
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new InvalidDataException($"Invalid MusicXML <beats> value '{beats.Value}'.");
        }

        return value;
    }
}
