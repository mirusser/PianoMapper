using System.Xml.Linq;

namespace PianoMapper.Music;

// <clef>. The grand staff always draws a note on the treble or bass staff by its pitch, so a clef never moves a pitch.
// It decides the staff of a one-staff part (bass clef: bass staff), and a change of clef, or one this app does not draw,
// is reported. A clef for unpitched or tablature notation has no place here and fails the import.
public sealed partial class MusicXmlScoreReader
{
    private const string ClefChangeWarning = "clef change";
    private const string NonStandardClefWarning = "non-standard clef";

    private readonly record struct ClefValue(string Sign, int Line, int OctaveChange)
    {
        internal bool IsStandard => OctaveChange == 0 && ((Sign == "G" && Line == 2) || (Sign == "F" && Line == 4));
    }

    private static void ParseClef(XElement clef, PartState state, MusicXmlImportWarnings warnings)
    {
        string sign = RequiredChild(clef, "sign").Value.Trim();
        if (sign is "percussion" or "TAB" or "jianpu")
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <clef> sign '{sign}': only pitched staves are supported.");
        }

        if (sign == "none")
        {
            return;
        }

        int staffNumber = clef.Attribute("number") is { } number ? ParsePositiveAttribute(number, "clef") : 1;
        int line = FindChild(clef, "line") is { } lineElement ? ParseInt(lineElement, "line") : GetDefaultClefLine(sign);
        int octaveChange = FindChild(clef, "clef-octave-change") is { } octaveElement ? ParseInt(octaveElement, "clef-octave-change") : 0;
        var parsed = new ClefValue(sign, line, octaveChange);
        if (state.Clefs.TryGetValue(staffNumber, out var previous) && previous != parsed)
        {
            warnings.Add(ClefChangeWarning);
        }

        if (!parsed.IsStandard && (!state.Clefs.TryGetValue(staffNumber, out previous) || previous != parsed))
        {
            warnings.Add(NonStandardClefWarning);
        }

        state.Clefs[staffNumber] = parsed;
    }

    private static int GetDefaultClefLine(string sign) => sign switch
    {
        "G" => 2,
        "F" => 4,
        _ => 3,
    };

    // A one-staff part's staff is its clef's: a bass clef puts the notes on the bass staff, and any other clef on the
    // treble staff. Where a part has two staves, <staff> says which, as before; an explicit second staff is honored in
    // a part that never declared <staves>, as it always was.
    private static Staff ResolveStaff(XElement element, PartState state)
    {
        Staff elementStaff = ParseStaff(element);
        if (state.StaveCount != 1 || elementStaff == Staff.Bass)
        {
            return elementStaff;
        }

        return state.Clefs.TryGetValue(1, out var clef) && clef.Sign == "F" ? Staff.Bass : Staff.Treble;
    }
}
