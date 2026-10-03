using System.Collections.Frozen;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PianoMapper.Music;

public sealed partial class MusicXmlScoreReader
{
    private const string AccidentalElementName = "accidental";
    private const string AccidentalMarkElementName = "accidental-mark";
    private const string ActualNotesElementName = "actual-notes";
    private const string ArticulationsElementName = "articulations";
    private const string BeamElementName = "beam";
    private const string ChordElementName = "chord";
    private const string CompressedMusicXmlExtension = ".mxl";
    private const string ContainerEntryName = "META-INF/container.xml";
    private const string DotElementName = "dot";
    private const string DirectionTypeElementName = "direction-type";
    private const string DurationElementName = "duration";
    private const string FermataElementName = "fermata";
    private const string MeasureAttributeName = "measure";
    private const string MetronomeElementName = "metronome";
    private const string PrintObjectAttributeName = "print-object";
    private const string FingeringElementName = "fingering";
    private const string NormalNotesElementName = "normal-notes";
    private const string NotationsElementName = "notations";
    private const string OrnamentsElementName = "ornaments";
    private const string OctaveShiftElementName = "octave-shift";
    private const string PitchElementName = "pitch";
    private const string RepeatElementName = "repeat";
    private const string RestElementName = "rest";
    private const string SlurElementName = "slur";
    private const string SoundElementName = "sound";
    private const string StaffElementName = "staff";
    private const string StemElementName = "stem";
    private const string TechnicalElementName = "technical";
    private const string TieElementName = "tie";
    private const string TimeModificationElementName = "time-modification";
    private const string TupletElementName = "tuplet";
    private const string TypeElementName = "type";
    private const string VoiceElementName = "voice";
    private const double DefaultQuarterNotesPerMinute = 120;
    private const double DurationTolerance = 1e-9;
    private const int MaximumDerivedDots = 3;
    private static readonly int[] DerivableDenominators = [1, 2, 4, 8, 16, 32, 64];

    private static readonly FrozenSet<string> IgnoredPresentationElements = new[]
    {
        "work",
        "identification",
        "defaults",
        "credit",
        "print",
        "bar-style",
        "lyric",
        "dynamics",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The MusicXML <notations> content model's presentation-only children: real data (ties,
    // fingering, fermata) is still extracted by name elsewhere, but none of these has anything for
    // HasTieStart's own <tied>-only scan to act on, so it just needs to recognize and skip them
    // rather than throw.
    private static readonly FrozenSet<string> IgnoredNotationsElements = new[]
    {
        TechnicalElementName,
        FermataElementName,
        ArticulationsElementName,
        TupletElementName,
        SlurElementName,
        "arpeggiate",
        "non-arpeggiate",
        "glissando",
        "slide",
        "ornaments",
        "accidental-mark",
        "other-notation",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IgnoredDirectionElements = new[]
    {
        DirectionTypeElementName,
        "footnote",
        "level",
        "offset",
        "listening",
        StaffElementName,
        VoiceElementName,
    }.ToFrozenSet(StringComparer.Ordinal);

    // Measure-level elements that carry hyperlinks, grouping or playback-listening metadata rather
    // than notation, so they are skipped without a warning.
    private static readonly FrozenSet<string> IgnoredMeasureElements = new[]
    {
        "link",
        "bookmark",
        "grouping",
        "listening",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The <sound> attributes that only steer MIDI playback in the originating application.
    private static readonly FrozenSet<string> SoundPlaybackHintAttributes = new[]
    {
        "dynamics",
        "pizzicato",
        "pan",
        "elevation",
        "damper-pedal",
        "soft-pedal",
        "sostenuto-pedal",
        "divisions",
        "time-only",
        "id",
    }.ToFrozenSet(StringComparer.Ordinal);

    private const string SoundPlaybackHintsWarning = "sound playback hints";
    private const string FingerChangeWarning = "finger change";
    private const string UnsupportedAccidentalWarning = "microtonal or special accidental";
    private const string HiddenNoteWarning = "hidden note";
    private const string HiddenRestWarning = "hidden rest";

    // Every value MusicXML's accidental-value type allows; one outside this list is a malformed file, not an unsupported glyph.
    private static readonly FrozenSet<string> MusicXmlAccidentalValues = new[]
    {
        "sharp", "natural", "flat", "double-sharp", "sharp-sharp", "flat-flat", "natural-sharp", "natural-flat",
        "quarter-flat", "quarter-sharp", "three-quarters-flat", "three-quarters-sharp", "sharp-down", "sharp-up",
        "natural-down", "natural-up", "flat-down", "flat-up", "double-sharp-down", "double-sharp-up", "flat-flat-down",
        "flat-flat-up", "arrow-down", "arrow-up", "triple-sharp", "triple-flat", "slash-quarter-sharp", "slash-sharp",
        "slash-flat", "double-slash-flat", "sharp-1", "sharp-2", "sharp-3", "sharp-5", "flat-1", "flat-2", "flat-3",
        "flat-4", "sori", "koron", "other",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Technical marks that change what is heard (a harmonic sounds another pitch, a bend slides one), which a piano
    // score does not use and this importer cannot show; the other technical marks only advise how to play.
    private static readonly FrozenSet<string> PitchChangingTechnicalElements = new[] { "harmonic", "bend" }
        .ToFrozenSet(StringComparer.Ordinal);

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
        BeamElementName,
        StemElementName,
        AccidentalElementName,
        TimeModificationElementName,
        "instrument",
        "footnote",
        "level",
        "play",
        "listen",
        "notehead",
        "notehead-text",
    }.ToFrozenSet(StringComparer.Ordinal);

    public Score Read(string path) => ReadWithWarnings(path).Score;

    public Score Read(Stream stream, string sourceName) => ReadWithWarnings(stream, sourceName).Score;

    public MusicXmlReadResult ReadWithWarnings(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var stream = File.OpenRead(path);
        return ReadWithWarnings(stream, Path.GetFileName(path));
    }

    public MusicXmlReadResult ReadWithWarnings(Stream stream, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        var warnings = new MusicXmlImportWarnings();

        XDocument document = Path.GetExtension(sourceName).Equals(
            CompressedMusicXmlExtension,
            StringComparison.OrdinalIgnoreCase)
            ? LoadCompressedDocument(stream)
            : LoadDocument(stream);

        var root = document.Root ?? throw new InvalidDataException("Could not parse MusicXML: document has no root element.");
        if (root.Name.LocalName != "score-partwise")
        {
            throw Unsupported(root.Name.LocalName);
        }

        ValidateRootElements(root);
        var parts = root.Elements().Where(element => element.Name.LocalName == "part").ToArray();
        if (parts.Length == 0)
        {
            throw new NotSupportedException("Unsupported MusicXML element <part>: at least one part is required.");
        }

        ValidateStaffCount(parts);
        var partResults = parts.Select(part => ReadPart(part, warnings)).ToArray();
        var score = BuildScore(Path.GetFileNameWithoutExtension(sourceName), partResults);
        return new MusicXmlReadResult(score, warnings.ToList());
    }

    private static PartResult ReadPart(XElement part, MusicXmlImportWarnings warnings)
    {
        var state = new PartState();
        var measures = new List<ScoreMeasure>();

        int measureIndex = 0;
        foreach (var measureElement in part.Elements())
        {
            if (measureElement.Name.LocalName != "measure")
            {
                throw Unsupported(measureElement.Name.LocalName);
            }

            measures.Add(ReadMeasure(measureElement, measureIndex, state, warnings));
            measureIndex++;
        }

        ValidateJumpTargets(state);
        return new PartResult(measures, state);
    }

    private static XDocument LoadDocument(Stream stream)
    {
        try
        {
            return XDocument.Load(stream, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"Could not parse MusicXML: {exception.Message}", exception);
        }
    }

    private static XDocument LoadCompressedDocument(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var containerEntry = archive.GetEntry(ContainerEntryName) ??
            throw new InvalidDataException($"Compressed MusicXML does not contain {ContainerEntryName}.");
        XDocument container;
        using (var containerStream = containerEntry.Open())
        {
            container = LoadDocument(containerStream);
        }
        string? scorePath = container
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "rootfile")?
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "full-path")?
            .Value;
        if (string.IsNullOrWhiteSpace(scorePath))
        {
            throw new InvalidDataException("Compressed MusicXML container does not declare a root score file.");
        }

        var scoreEntry = archive.GetEntry(scorePath) ??
            throw new InvalidDataException($"Compressed MusicXML does not contain the declared score file '{scorePath}'.");
        using var scoreStream = scoreEntry.Open();
        return LoadDocument(scoreStream);
    }

    private static void ValidateRootElements(XElement root)
    {
        foreach (var element in root.Elements())
        {
            string name = element.Name.LocalName;
            if (name is "part-list" or "part" or "movement-title" or "movement-number" ||
                IgnoredPresentationElements.Contains(name))
            {
                continue;
            }

            throw Unsupported(name);
        }
    }

    // <measure-style> only asks for a multi-measure rest or a slash/repeat notation. A multiple
    // rest still has its own rest per measure here, but measure-repeat, beat-repeat and slash
    // omit the notes themselves, so they cannot be shown without inventing music.
    private static void ValidateMeasureStyle(XElement measureStyle, MusicXmlImportWarnings warnings)
    {
        foreach (var element in measureStyle.Elements())
        {
            string name = element.Name.LocalName;
            if (name == "multiple-rest")
            {
                warnings.Add(name);
                continue;
            }

            throw Unsupported(name);
        }
    }

    // <metronome> is a printed tempo mark, normally paired with <sound tempo>; it only supplies the
    // tempo when the file has no <sound tempo>. A ratio mark (beat-unit = beat-unit), a metric
    // modulation or a free-text range such as "120-132" has no single tempo, so it returns null.
    private static double? TryParseMetronome(XElement metronome)
    {
        var beatUnit = FindChild(metronome, "beat-unit");
        var perMinute = FindChild(metronome, "per-minute");
        if (beatUnit is null || perMinute is null ||
            !double.TryParse(perMinute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double perMinuteValue) ||
            perMinuteValue <= 0)
        {
            return null;
        }

        double? unitQuarterNotes = beatUnit.Value.Trim() switch
        {
            "whole" => 4,
            "half" => 2,
            "quarter" => 1,
            "eighth" => 0.5,
            "16th" => 0.25,
            "32nd" => 0.125,
            _ => null,
        };
        if (unitQuarterNotes is null)
        {
            return null;
        }

        int dots = metronome.Elements().Count(element => element.Name.LocalName == "beat-unit-dot");
        return perMinuteValue * unitQuarterNotes.Value * (2.0 - (1.0 / Math.Pow(2.0, dots)));
    }

    private static double? ParseSound(XElement sound, PartState state, MusicXmlImportWarnings warnings)
    {
        ReadJumpAttributes(sound, state, warnings);
        foreach (var attribute in sound.Attributes())
        {
            if (SoundPlaybackHintAttributes.Contains(attribute.Name.LocalName))
            {
                warnings.Add(SoundPlaybackHintsWarning);
            }
        }

        foreach (var child in sound.Elements())
        {
            if (child.Name.LocalName != "offset")
            {
                warnings.Add(SoundPlaybackHintsWarning);
            }
        }

        var tempoAttribute = sound.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "tempo");
        if (tempoAttribute is null)
        {
            return null;
        }

        if (!double.TryParse(
            tempoAttribute.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double quarterNotesPerMinute))
        {
            throw new InvalidDataException($"Invalid MusicXML tempo '{tempoAttribute.Value}'.");
        }

        return quarterNotesPerMinute;
    }

    private static (bool IsStart, int Octaves, int Number)? ParseOctaveShiftDirective(XElement direction)
    {
        var octaveShifts = direction.Elements()
            .Where(element => element.Name.LocalName == DirectionTypeElementName)
            .SelectMany(element => element.Elements())
            .Where(element => element.Name.LocalName == OctaveShiftElementName)
            .ToArray();
        if (octaveShifts.Length == 0)
        {
            return null;
        }

        if (octaveShifts.Length > 1)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{OctaveShiftElementName}>: " +
                "exactly one octave shift per direction is required.");
        }

        var octaveShift = octaveShifts[0];
        string? type = octaveShift.Attribute("type")?.Value;
        int number = ParsePairingNumber(octaveShift, OctaveShiftElementName);
        return type switch
        {
            "down" => (true, ParseOctaveShiftSize(octaveShift), number),
            "up" => (true, -ParseOctaveShiftSize(octaveShift), number),
            "stop" => (false, 0, number),
            "continue" => throw new NotSupportedException(
                $"Unsupported MusicXML <{OctaveShiftElementName}> type 'continue'."),
            _ => throw new InvalidDataException(
                $"Invalid MusicXML <{OctaveShiftElementName}> type '{type ?? string.Empty}'."),
        };
    }

    private static int ParseOctaveShiftSize(XElement octaveShift)
    {
        string value = octaveShift.Attribute("size")?.Value ?? "8";
        return value switch
        {
            "8" => 1,
            "15" => 2,
            "22" => 3,
            _ => throw Unsupported($"{OctaveShiftElementName}@size"),
        };
    }

    private static void ParseNote(
        XElement noteElement,
        int measureIndex,
        int divisions,
        bool divisionsSpecified,
        Staff staff,
        TimeSignature timeSignature,
        int soundingOctavesAboveNotated,
        ref int cursorDivisions,
        ref int lastNoteOnsetDivisions,
        ICollection<ScoreNote> notes,
        ICollection<ScoreRest> rests,
        MusicXmlImportWarnings warnings)
    {
        ValidateNoteElements(noteElement);
        ReportIgnoredNoteContent(noteElement, warnings);
        int durationDivisions = ParsePositiveInt(RequiredChild(noteElement, DurationElementName), DurationElementName);
        var restElement = FindChild(noteElement, RestElementName);
        bool isMeasureRest = restElement?.Attribute(MeasureAttributeName)?.Value == "yes";
        var noteValue = isMeasureRest
            ? ParseMeasureRestValue(durationDivisions, divisions)
            : ParseNoteValue(noteElement, durationDivisions, divisions, divisionsSpecified);
        bool isChord = FindChild(noteElement, ChordElementName) is not null;
        int onsetDivisions = isChord ? lastNoteOnsetDivisions : cursorDivisions;
        double beatOffset = DivisionsToBeats(onsetDivisions, divisions, timeSignature);

        if (noteElement.Attribute(PrintObjectAttributeName)?.Value == "no")
        {
            // A hidden note or rest still takes up its time, but it is not shown, so it is not imported:
            // the practice view expects what it displays.
            warnings.Add(restElement is null ? HiddenNoteWarning : HiddenRestWarning);
        }
        else if (restElement is not null)
        {
            _ = ParseStemDirection(noteElement);
            rests.Add(new ScoreRest(noteValue, measureIndex, beatOffset, staff, isMeasureRest));
        }
        else
        {
            ScoreStemDirection? stemDirection = ParseStemDirection(noteElement);
            var pitchElement = RequiredChild(noteElement, PitchElementName);
            Pitch soundingPitch = ParsePitch(pitchElement);
            notes.Add(new ScoreNote(
                soundingPitch,
                noteValue,
                measureIndex,
                beatOffset,
                staff,
                TiesToNext: HasTieStart(noteElement),
                IsChordContinuation: isChord,
                BeamState: ParseBeamState(noteElement),
                StemDirection: stemDirection,
                Fingering: ParseFingering(noteElement, warnings),
                Accidental: ParseAccidental(noteElement, warnings),
                Fermata: ParseFermata(noteElement),
                Articulation: ParseArticulation(noteElement, warnings),
                Ornament: ParseOrnament(noteElement, warnings),
                AccidentalMark: ParseAccidentalMark(noteElement),
                Slur: ParseSlur(noteElement),
                Arpeggio: ParseArpeggio(noteElement),
                Glissando: ParseGlissando(noteElement),
                SoundingOctavesAboveNotated: soundingOctavesAboveNotated));
        }

        if (!isChord)
        {
            lastNoteOnsetDivisions = cursorDivisions;
            cursorDivisions += durationDivisions;
        }
    }

    // Counts the note content that parses but is never drawn: lyrics, notehead shapes and notations dynamics.
    private static void ReportIgnoredNoteContent(XElement noteElement, MusicXmlImportWarnings warnings)
    {
        foreach (var element in noteElement.Elements())
        {
            string name = element.Name.LocalName;
            if (name is "lyric" or "notehead" or "notehead-text")
            {
                warnings.Add(name);
            }
            else if (name == NotationsElementName)
            {
                foreach (var notation in element.Elements().Where(notation => notation.Name.LocalName == "dynamics"))
                {
                    warnings.Add(notation.Name.LocalName);
                }
            }
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

            if (IgnoredNotationsElements.Contains(name))
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
            else if (type is not ("stop" or "continue" or "let-ring"))
            {
                // "continue" only marks a tie running on across a system break and "let-ring" is a guitar
                // curve; the <tie> elements carry the sound, so neither starts or ends a tie here.
                throw new InvalidDataException($"Invalid MusicXML tied type '{type}'.");
            }
        }

        return hasStart;
    }

    // <accidental> is only what is printed beside the note; the pitch comes from <alter>. A note can carry several
    // (a microtonal sign and a flat); the first this app can draw is kept and the others are reported.
    private static ScoreAccidental? ParseAccidental(XElement noteElement, MusicXmlImportWarnings warnings)
    {
        ScoreAccidental? result = null;
        foreach (var accidental in noteElement.Elements().Where(element => element.Name.LocalName == AccidentalElementName))
        {
            string value = accidental.Value.Trim();
            if (TryParseAccidentalValue(value) is { } parsed)
            {
                result ??= parsed;
            }
            else if (MusicXmlAccidentalValues.Contains(value))
            {
                warnings.Add(UnsupportedAccidentalWarning);
            }
            else
            {
                throw new InvalidDataException($"Invalid MusicXML <{AccidentalElementName}> value '{value}'.");
            }
        }

        return result;
    }

    private static ScoreAccidental? ParseAccidentalMark(XElement noteElement)
    {
        var accidentalMarks = FindChild(noteElement, NotationsElementName)?
            .Elements()
            .Where(element => element.Name.LocalName == AccidentalMarkElementName)
            .ToArray() ?? [];
        if (accidentalMarks.Length == 0)
        {
            return null;
        }

        if (accidentalMarks.Length > 1)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{AccidentalMarkElementName}>: exactly one accidental-mark per note is required.");
        }

        return ParseAccidentalValue(AccidentalMarkElementName, accidentalMarks[0].Value.Trim());
    }

    private static ScoreAccidental ParseAccidentalValue(string elementName, string value) =>
        TryParseAccidentalValue(value) ??
        throw new NotSupportedException($"Unsupported MusicXML <{elementName}> value '{value}'.");

    private static ScoreAccidental? TryParseAccidentalValue(string value) => value switch
    {
        "natural" => ScoreAccidental.Natural,
        "sharp" => ScoreAccidental.Sharp,
        "flat" => ScoreAccidental.Flat,
        "double-sharp" => ScoreAccidental.DoubleSharp,
        "sharp-sharp" => ScoreAccidental.SharpSharp,
        "flat-flat" => ScoreAccidental.DoubleFlat,
        _ => null,
    };

    /// <summary>
    /// Reads a start/stop-pairing notation's <c>type</c> attribute (used by both
    /// <see cref="ParseSlur"/> and, later, glissando/slide parsing) — only "start" and "stop" are
    /// supported; MusicXML's "continue" (a slur/tie segment split across a line/system break) is
    /// out of v1's scope, matching <see cref="HasTieStart"/>'s existing tie/tied handling.
    /// </summary>
    private static bool ParsePairingType(XElement element, string elementName)
    {
        string type = element.Attribute("type")?.Value ?? string.Empty;
        return type switch
        {
            "start" => true,
            "stop" => false,
            _ => throw new InvalidDataException($"Invalid MusicXML <{elementName}> type '{type}'."),
        };
    }

    /// <summary>
    /// Reads a start/stop-pairing notation's <c>number</c> attribute, defaulting to 1 when absent
    /// (MusicXML's own default), the same way <see cref="ParseBeamState"/> treats a missing beam
    /// <c>number</c> as "1".
    /// </summary>
    private static int ParsePairingNumber(XElement element, string elementName)
    {
        string? value = element.Attribute("number")?.Value;
        if (value is null)
        {
            return 1;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number <= 0)
        {
            throw new InvalidDataException($"Invalid MusicXML <{elementName}> number '{value}'.");
        }

        return number;
    }

    private static ScoreArpeggio? ParseArpeggio(XElement noteElement)
    {
        var notations = FindChild(noteElement, NotationsElementName);
        if (notations is null)
        {
            return null;
        }

        var arpeggiateMarks = notations.Elements().Where(element => element.Name.LocalName == "arpeggiate").ToArray();
        var nonArpeggiateMarks = notations.Elements()
            .Where(element => element.Name.LocalName == "non-arpeggiate")
            .ToArray();
        int markCount = arpeggiateMarks.Length + nonArpeggiateMarks.Length;
        if (markCount == 0)
        {
            return null;
        }

        if (markCount > 1)
        {
            throw new NotSupportedException(
                "Unsupported MusicXML <arpeggiate>/<non-arpeggiate>: exactly one arpeggio mark per note is required.");
        }

        return arpeggiateMarks.Length == 1 ? ScoreArpeggio.Arpeggiate : ScoreArpeggio.NonArpeggiate;
    }

    private static ScoreGlissando? ParseGlissando(XElement noteElement)
    {
        var notations = FindChild(noteElement, NotationsElementName);
        if (notations is null)
        {
            return null;
        }

        var glissandos = notations.Elements().Where(element => element.Name.LocalName == "glissando").ToArray();
        var slides = notations.Elements().Where(element => element.Name.LocalName == "slide").ToArray();
        int markCount = glissandos.Length + slides.Length;
        if (markCount == 0)
        {
            return null;
        }

        if (markCount > 1)
        {
            throw new NotSupportedException(
                "Unsupported MusicXML <glissando>/<slide>: exactly one glissando or slide per note is required.");
        }

        (XElement element, ScoreGlissandoKind kind) = glissandos.Length == 1
            ? (glissandos[0], ScoreGlissandoKind.Glissando)
            : (slides[0], ScoreGlissandoKind.Slide);
        string elementName = element.Name.LocalName;
        return new ScoreGlissando(
            ParsePairingType(element, elementName),
            ParsePairingNumber(element, elementName),
            kind);
    }

    private static ScoreFermata? ParseFermata(XElement noteElement)
    {
        var fermatas = FindChild(noteElement, NotationsElementName)?
            .Elements()
            .Where(element => element.Name.LocalName == FermataElementName)
            .ToArray() ?? [];
        if (fermatas.Length == 0)
        {
            return null;
        }

        if (fermatas.Length > 1)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{FermataElementName}>: exactly one fermata per note is required.");
        }

        return ParseFermataElement(fermatas[0]);
    }

    private static ScoreFermata ParseFermataElement(XElement fermata)
    {
        string shape = fermata.Value.Trim();
        if (shape is not ("" or "normal"))
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{FermataElementName}> value '{shape}'.");
        }

        return fermata.Attribute("type")?.Value switch
        {
            null or "upright" => ScoreFermata.Upright,
            "inverted" => ScoreFermata.Inverted,
            var value => throw new InvalidDataException(
                $"Invalid MusicXML <{FermataElementName}> type '{value}'."),
        };
    }

    private static ScoreFingering? ParseFingering(XElement noteElement, MusicXmlImportWarnings warnings)
    {
        var notations = FindChild(noteElement, NotationsElementName);
        var technical = notations?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == TechnicalElementName);
        if (technical is null)
        {
            return null;
        }

        var fingeringElements = technical.Elements()
            .Where(element => element.Name.LocalName == FingeringElementName)
            .ToArray();
        foreach (var element in technical.Elements().Where(element => element.Name.LocalName != FingeringElementName))
        {
            if (PitchChangingTechnicalElements.Contains(element.Name.LocalName))
            {
                throw Unsupported(element.Name.LocalName);
            }

            warnings.Add(TechnicalElementName);
        }

        if (fingeringElements.Length == 0)
        {
            return null;
        }

        if (fingeringElements.Length > 1)
        {
            throw new NotSupportedException(
                $"Unsupported MusicXML <{FingeringElementName}>: exactly one fingering per note is required.");
        }

        var fingeringElement = fingeringElements[0];
        string value = fingeringElement.Value.Trim();
        string[] fingers = value.Split('-');
        if (fingers.Length > 1)
        {
            warnings.Add(FingerChangeWarning);
        }

        if (!int.TryParse(fingers[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ||
            number is < 1 or > 5 ||
            fingers.Skip(1).Any(finger =>
                !int.TryParse(finger, NumberStyles.Integer, CultureInfo.InvariantCulture, out int change) ||
                change is < 1 or > 5))
        {
            throw new InvalidDataException(
                $"Invalid MusicXML <{FingeringElementName}> value '{value}': expected a piano finger number from 1 to 5.");
        }

        ScoreFingeringPlacement? placement = fingeringElement.Attribute("placement")?.Value switch
        {
            null => null,
            "above" => ScoreFingeringPlacement.Above,
            "below" => ScoreFingeringPlacement.Below,
            var placementValue => throw new InvalidDataException(
                $"Invalid MusicXML <{FingeringElementName}> placement '{placementValue}'."),
        };
        return new ScoreFingering(number, placement);
    }

    private static BeamState ParseBeamState(XElement noteElement)
    {
        var beam = noteElement
            .Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == BeamElementName &&
                (element.Attribute("number")?.Value is null or "1"));
        if (beam is null)
        {
            return BeamState.None;
        }

        return beam.Value.Trim() switch
        {
            "begin" => BeamState.Begin,
            "continue" => BeamState.Continue,
            "end" => BeamState.End,
            var value => throw new InvalidDataException($"Invalid MusicXML beam value '{value}'."),
        };
    }

    private static ScoreStemDirection? ParseStemDirection(XElement noteElement)
    {
        var stem = FindChild(noteElement, StemElementName);
        if (stem is null)
        {
            return null;
        }

        string value = stem.Value.Trim();
        return value switch
        {
            "up" => ScoreStemDirection.Up,
            "down" => ScoreStemDirection.Down,
            "none" or "double" => throw new NotSupportedException(
                $"Unsupported MusicXML <{StemElementName}> value '{value}'."),
            _ => throw new InvalidDataException($"Invalid MusicXML <{StemElementName}> value '{value}'."),
        };
    }

    private static void ValidateBeamStemDirections(IEnumerable<ScoreNote> notes)
    {
        foreach (var staffNotes in notes.GroupBy(note => note.Staff))
        {
            bool isInBeamGroup = false;
            ScoreStemDirection? groupDirection = null;
            foreach (var note in staffNotes.OrderBy(note => note.BeatOffset))
            {
                switch (note.BeamState)
                {
                    case BeamState.Begin:
                        isInBeamGroup = true;
                        groupDirection = note.StemDirection;
                        break;
                    case BeamState.Continue when isInBeamGroup:
                    case BeamState.End when isInBeamGroup:
                        ValidateBeamStemDirection(note.StemDirection, ref groupDirection);
                        if (note.BeamState == BeamState.End)
                        {
                            isInBeamGroup = false;
                            groupDirection = null;
                        }

                        break;
                    default:
                        isInBeamGroup = false;
                        groupDirection = null;
                        break;
                }
            }
        }
    }

    private static void ValidateBeamStemDirection(
        ScoreStemDirection? stemDirection,
        ref ScoreStemDirection? groupDirection)
    {
        if (stemDirection is null)
        {
            return;
        }

        if (groupDirection is not null && stemDirection != groupDirection)
        {
            string firstValue = groupDirection.Value.ToString().ToLowerInvariant();
            string secondValue = stemDirection.Value.ToString().ToLowerInvariant();
            throw new NotSupportedException(
                $"Conflicting MusicXML <{StemElementName}> values " +
                $"'{firstValue}' and '{secondValue}' in one beam group.");
        }

        groupDirection = stemDirection;
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

    private static NoteValue ParseNoteValue(
        XElement noteElement,
        int durationDivisions,
        int divisions,
        bool divisionsSpecified)
    {
        int dots = noteElement.Elements().Count(element => element.Name.LocalName == DotElementName);
        int tupletActualNotes = 1;
        int tupletNormalNotes = 1;
        if (FindChild(noteElement, TimeModificationElementName) is { } timeModification)
        {
            tupletActualNotes = ParsePositiveInt(
                RequiredChild(timeModification, ActualNotesElementName),
                ActualNotesElementName);
            tupletNormalNotes = ParsePositiveInt(
                RequiredChild(timeModification, NormalNotesElementName),
                NormalNotesElementName);
        }

        if (FindChild(noteElement, TypeElementName) is not { } typeElement)
        {
            return DeriveNoteValue(durationDivisions, divisions, tupletActualNotes, tupletNormalNotes);
        }

        string type = typeElement.Value;
        int denominator = type switch
        {
            "whole" => 1,
            "half" => 2,
            "quarter" => 4,
            "eighth" => 8,
            "16th" => 16,
            "32nd" => 32,
            "64th" => 64,
            _ => throw new NotSupportedException($"Unsupported MusicXML note type '{type}'."),
        };
        var noteValue = new NoteValue(denominator, dots, tupletActualNotes, tupletNormalNotes);
        ValidateDuration(durationDivisions, divisions, divisionsSpecified, noteValue);
        return noteValue;
    }

    // <type> is optional in MusicXML: without it the written value is whatever single (dotted) note value
    // the duration comes to once any <time-modification> is undone.
    private static NoteValue DeriveNoteValue(
        int durationDivisions,
        int divisions,
        int tupletActualNotes,
        int tupletNormalNotes)
    {
        double writtenQuarterNotes = (double)durationDivisions / divisions * tupletActualNotes / tupletNormalNotes;
        return FindSingleNoteValue(writtenQuarterNotes, tupletActualNotes, tupletNormalNotes) ??
            throw new InvalidDataException(
                $"MusicXML <note> has no <{TypeElementName}> and its duration {durationDivisions} " +
                $"(divisions {divisions}) is not a single note value.");
    }

    private static NoteValue? FindSingleNoteValue(double quarterNotes, int tupletActualNotes, int tupletNormalNotes)
    {
        foreach (int denominator in DerivableDenominators)
        {
            for (int dots = 0; dots <= MaximumDerivedDots; dots++)
            {
                double candidate = 4.0 * (2.0 - (1.0 / Math.Pow(2.0, dots))) / denominator;
                if (Math.Abs(candidate - quarterNotes) <= DurationTolerance)
                {
                    return new NoteValue(denominator, dots, tupletActualNotes, tupletNormalNotes);
                }
            }
        }

        return null;
    }

    // A whole-measure rest lasts as long as its measure, whatever its (optional, often "whole") <type> says; the length
    // is kept as a single value where there is one, else as a whole rest.
    private static NoteValue ParseMeasureRestValue(int durationDivisions, int divisions) =>
        FindSingleNoteValue((double)durationDivisions / divisions, 1, 1) ?? new NoteValue(1);

    private static void ValidateDuration(int durationDivisions, int divisions, bool divisionsSpecified, NoteValue noteValue)
    {
        double actualQuarterNotes = (double)durationDivisions / divisions;
        double expectedQuarterNotes = 4.0 * (2.0 - (1.0 / Math.Pow(2.0, noteValue.Dots)))
            * noteValue.TupletNormalNotes / noteValue.TupletActualNotes / noteValue.Denominator;
        if (Math.Abs(actualQuarterNotes - expectedQuarterNotes) > 1e-9)
        {
            string divisionsHint = divisionsSpecified
                ? string.Empty
                : " (no <divisions> was given, so one division per quarter note was assumed)";
            throw new InvalidDataException(
                $"MusicXML duration {durationDivisions} does not match note type 1/{noteValue.Denominator} " +
                $"with {noteValue.Dots} dot(s){divisionsHint}.");
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
