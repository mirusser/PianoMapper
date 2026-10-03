using System.Xml.Linq;

namespace PianoMapper.Music;

// Reading one <measure>: the part-wide state that carries from measure to measure, and the <attributes> content.
public sealed partial class MusicXmlScoreReader
{
    private const string BackupBeforeMeasureStartWarning = "backup before the measure start";
    private const string ImplicitAttributeName = "implicit";
    private const string MissingDivisionsWarning = "missing divisions";
    private const string MidMeasureKeyChangeWarning = "mid-measure key change";
    private const string NonTraditionalKeyWarning = "non-traditional key";

    // What carries from one measure of a part to the next.
    private sealed class PartState
    {
        internal int Divisions { get; set; } = 1;

        internal bool DivisionsSpecified { get; set; }

        internal bool HasReportedMissingDivisions { get; set; }

        /// <summary>The key signature in effect now.</summary>
        internal int KeyFifths { get; set; }

        /// <summary>The key the score opens with: the one given before the first note of the first measure, else C major.</summary>
        internal int InitialKeyFifths { get; set; }

        /// <summary>A key given after a measure's first note, which takes effect from the next measure.</summary>
        internal int? PendingKeyFifths { get; set; }

        internal TimeSignature TimeSignature { get; set; } = new(4, new NoteValue(4));

        internal bool TimeSpecified { get; set; }

        internal double QuarterNotesPerMinute { get; set; } = DefaultQuarterNotesPerMinute;

        internal bool TempoSpecified { get; set; }

        internal int ActiveOctaveShiftOctaves { get; set; }

        internal int ActiveOctaveShiftNumber { get; set; } = 1;

        /// <summary>How many staves the part declares (<c>&lt;staves&gt;</c>); one unless it says otherwise.</summary>
        internal int StaveCount { get; set; } = 1;

        /// <summary>The clef in effect on each staff, by its MusicXML staff number.</summary>
        internal Dictionary<int, ClefValue> Clefs { get; } = [];

        /// <summary>The (kind, id) pairs the score defines with <c>&lt;sound segno="..."&gt;</c> and <c>coda</c>.</summary>
        internal HashSet<(string Kind, string Id)> JumpTargets { get; } = [];

        /// <summary>The (target kind, id) pairs its <c>dalsegno</c> and <c>tocoda</c> jumps refer to.</summary>
        internal List<(string Kind, string Id)> JumpReferences { get; } = [];
    }

    private sealed record PartResult(IReadOnlyList<ScoreMeasure> Measures, PartState State);

    // What one measure collects while its children are read.
    private sealed class MeasureBuilder(int measureIndex)
    {
        internal int MeasureIndex { get; } = measureIndex;

        internal List<ScoreNote> Notes { get; } = [];

        internal List<ScoreRest> Rests { get; } = [];

        internal int CursorDivisions;

        internal int LastNoteOnsetDivisions;

        internal int MaximumCursorDivisions;

        /// <summary>The key signature that takes effect at this measure's start, when it differs from the measure before.</summary>
        internal int? KeyFifths { get; set; }

        /// <summary>Whether a key change was given after this measure's first note and not yet followed by a note.</summary>
        internal bool HasKeyChangeAwaitingNote { get; set; }

        internal ScoreBarline? LeftBarline { get; set; }

        internal ScoreBarline? RightBarline { get; set; }

        internal List<ScoreDirection> Directions { get; } = [];
    }

    private static ScoreMeasure ReadMeasure(
        XElement measureElement,
        int measureIndex,
        PartState state,
        MusicXmlImportWarnings warnings)
    {
        var measure = new MeasureBuilder(measureIndex);
        if (state.PendingKeyFifths is { } pendingKeyFifths)
        {
            ApplyKeyAtMeasureStart(pendingKeyFifths, state, measure);
            state.PendingKeyFifths = null;
        }

        foreach (var element in measureElement.Elements())
        {
            string name = element.Name.LocalName;
            if (IgnoredPresentationElements.Contains(name) || IgnoredMeasureElements.Contains(name))
            {
                continue;
            }

            switch (name)
            {
                case "attributes":
                    ParseAttributes(element, state, measure, warnings);
                    break;
                case SoundElementName:
                    if (ParseSound(element, state, warnings) is { } soundTempo)
                    {
                        ApplyTempo(soundTempo, state);
                    }

                    break;
                case HarmonyElementName:
                    ParseHarmony(element, state, measure, warnings);
                    break;
                case "figured-bass":
                    warnings.Add(name);
                    break;
                case "direction":
                    if (ParseDirection(element, state, measure, warnings) is { } parsedQuarterNotesPerMinute)
                    {
                        ApplyTempo(parsedQuarterNotesPerMinute, state);
                    }

                    ApplyOctaveShiftDirective(element, state);
                    break;
                case BarlineElementName:
                    ParseBarline(element, measure, warnings);
                    break;
                case "note":
                    ReportMissingDivisions(state, warnings);
                    if (measure.HasKeyChangeAwaitingNote)
                    {
                        warnings.Add(MidMeasureKeyChangeWarning);
                        measure.HasKeyChangeAwaitingNote = false;
                    }

                    ParseNote(
                        element,
                        measureIndex,
                        state.Divisions,
                        state.DivisionsSpecified,
                        ResolveStaff(element, state),
                        state.TimeSignature,
                        state.ActiveOctaveShiftOctaves,
                        ref measure.CursorDivisions,
                        ref measure.LastNoteOnsetDivisions,
                        measure.Notes,
                        measure.Rests,
                        warnings);
                    measure.MaximumCursorDivisions = Math.Max(
                        measure.MaximumCursorDivisions,
                        measure.CursorDivisions);
                    break;
                case "backup":
                    int backupDivisions = ParsePositiveInt(
                        RequiredChild(element, DurationElementName),
                        DurationElementName);
                    if (backupDivisions > measure.CursorDivisions)
                    {
                        // Spec-wise the cursor cannot go before the measure; the notes that follow only make sense
                        // from its first beat, so the move stops there.
                        warnings.Add(BackupBeforeMeasureStartWarning);
                        measure.CursorDivisions = 0;
                    }
                    else
                    {
                        measure.CursorDivisions -= backupDivisions;
                    }

                    break;
                case "forward":
                    measure.CursorDivisions += ParsePositiveInt(
                        RequiredChild(element, DurationElementName),
                        DurationElementName);
                    measure.MaximumCursorDivisions = Math.Max(
                        measure.MaximumCursorDivisions,
                        measure.CursorDivisions);
                    break;
                default:
                    throw Unsupported(name);
            }
        }

        ValidateBeamStemDirections(measure.Notes);
        return new ScoreMeasure(
            measure.Notes,
            measure.Rests,
            measure.KeyFifths,
            measure.LeftBarline,
            measure.RightBarline,
            measure.Directions.Count == 0 ? null : measure.Directions,
            GetImplicitMeasureLength(measureElement, measure, state));
    }

    private static double? GetImplicitMeasureLength(
        XElement measureElement,
        MeasureBuilder measure,
        PartState state)
    {
        string? value = measureElement.Attribute(ImplicitAttributeName)?.Value;
        if (value is null or "no")
        {
            return null;
        }

        if (value != "yes")
        {
            throw new InvalidDataException(
                $"Invalid MusicXML <measure> {ImplicitAttributeName} value '{value}'.");
        }

        if (measure.MaximumCursorDivisions == 0)
        {
            return null;
        }

        double lengthInBeats = DivisionsToBeats(
            measure.MaximumCursorDivisions,
            state.Divisions,
            state.TimeSignature);
        return lengthInBeats == state.TimeSignature.Numerator ? null : lengthInBeats;
    }

    // Notes without a <divisions> before them still parse with one division per quarter note, the way a bare
    // fragment is usually written; that is said once instead of staying silent.
    private static void ReportMissingDivisions(PartState state, MusicXmlImportWarnings warnings)
    {
        if (state.DivisionsSpecified || state.HasReportedMissingDivisions)
        {
            return;
        }

        warnings.Add(MissingDivisionsWarning);
        state.HasReportedMissingDivisions = true;
    }

    private static void ApplyTempo(double parsedQuarterNotesPerMinute, PartState state)
    {
        if (state.TempoSpecified && parsedQuarterNotesPerMinute != state.QuarterNotesPerMinute)
        {
            throw Unsupported(SoundElementName);
        }

        state.QuarterNotesPerMinute = parsedQuarterNotesPerMinute;
        state.TempoSpecified = true;
    }

    private static void ApplyOctaveShiftDirective(XElement direction, PartState state)
    {
        if (ParseOctaveShiftDirective(direction) is not { } directive)
        {
            return;
        }

        if (directive.IsStart)
        {
            if (state.ActiveOctaveShiftOctaves != 0)
            {
                throw new NotSupportedException(
                    $"Unsupported MusicXML <{OctaveShiftElementName}>: " +
                    "overlapping octave shifts are not supported.");
            }

            state.ActiveOctaveShiftOctaves = directive.Octaves;
            state.ActiveOctaveShiftNumber = directive.Number;
            return;
        }

        if (state.ActiveOctaveShiftOctaves == 0)
        {
            throw new InvalidDataException(
                $"Invalid MusicXML <{OctaveShiftElementName}>: stop has no active octave shift.");
        }

        if (directive.Number != state.ActiveOctaveShiftNumber)
        {
            throw new InvalidDataException(
                $"Invalid MusicXML <{OctaveShiftElementName}>: stop number " +
                $"'{directive.Number}' does not match active number '{state.ActiveOctaveShiftNumber}'.");
        }

        state.ActiveOctaveShiftOctaves = 0;
        state.ActiveOctaveShiftNumber = 1;
    }

    private static void ParseAttributes(
        XElement attributes,
        PartState state,
        MeasureBuilder measure,
        MusicXmlImportWarnings warnings)
    {
        bool hasReadKey = false;
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
                    state.Divisions = ParsePositiveInt(element, "divisions");
                    state.DivisionsSpecified = true;
                    break;
                case "key":
                    if (!hasReadKey)
                    {
                        ParseKeys(attributes, state, measure, warnings);
                        hasReadKey = true;
                    }

                    break;
                case TimeElementName:
                    var parsedTimeSignature = ParseTime(element, warnings);
                    if (state.TimeSpecified && parsedTimeSignature != state.TimeSignature)
                    {
                        throw Unsupported("time");
                    }

                    state.TimeSignature = parsedTimeSignature;
                    state.TimeSpecified = true;
                    break;
                case "staves":
                    int staffCount = ParsePositiveInt(element, "staves");
                    if (staffCount > 2)
                    {
                        throw Unsupported("staves");
                    }

                    state.StaveCount = staffCount;
                    break;
                case "clef":
                    ParseClef(element, state, warnings);
                    break;
                case "staff-details":
                case "part-symbol":
                case "instruments":
                case "directive":
                case "voice-directions":
                    warnings.Add(name);
                    break;
                case "footnote":
                case "level":
                    break;
                case "measure-style":
                    ValidateMeasureStyle(element, warnings);
                    break;
                default:
                    throw Unsupported(name);
            }
        }
    }

    // A <key> may be repeated per staff (number="1", "2"); the grand staff shares one signature, so they must agree.
    // A key without <fifths> lists its accidentals one by one (a non-traditional signature), which has no place in a
    // sharps-or-flats count: it is reported and the key stays what it was.
    private static void ParseKeys(
        XElement attributes,
        PartState state,
        MeasureBuilder measure,
        MusicXmlImportWarnings warnings)
    {
        var keyElements = attributes.Elements().Where(element => element.Name.LocalName == "key").ToArray();
        var fifthsPerKey = new List<int>();
        foreach (var keyElement in keyElements)
        {
            if (FindChild(keyElement, "fifths") is { } fifthsElement)
            {
                fifthsPerKey.Add(ParseInt(fifthsElement, "fifths"));
            }
            else if (FindChild(keyElement, "key-step") is not null)
            {
                warnings.Add(NonTraditionalKeyWarning);
            }
            else
            {
                throw new InvalidDataException("MusicXML element <key> requires <fifths>.");
            }
        }

        if (fifthsPerKey.Distinct().Count() > 1)
        {
            throw new NotSupportedException(
                "Unsupported MusicXML element <key>: different key signatures for the two staves.");
        }

        if (fifthsPerKey.Count == 0)
        {
            return;
        }

        int fifths = fifthsPerKey[0];
        if (measure.CursorDivisions == 0 && measure.Notes.Count == 0 && measure.Rests.Count == 0)
        {
            ApplyKeyAtMeasureStart(fifths, state, measure);
            return;
        }

        state.PendingKeyFifths = fifths;
        measure.HasKeyChangeAwaitingNote = true;
    }

    private static void ApplyKeyAtMeasureStart(int fifths, PartState state, MeasureBuilder measure)
    {
        if (measure.MeasureIndex == 0)
        {
            state.InitialKeyFifths = fifths;
        }
        else if (fifths != state.KeyFifths)
        {
            measure.KeyFifths = fifths;
        }

        state.KeyFifths = fifths;
    }
}
