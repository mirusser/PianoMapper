using System.Globalization;
using PianoMapper.Rendering;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

internal static class GrandStaffSceneBuilder
{
    private const double StaffX0 = -0.92;
    private const double StaffX1 = 0.96;
    private const double EndingBarlineGap = 0.012;
    private const double ClefX = -0.87;
    private const double KeySignatureX0 = -0.78;
    private const double KeySignatureXSpacing = 0.025;
    private const double TimeSignatureX = -0.59;
    private const double LedgerLineHalfWidth = 0.065;
    private const double StemLength = GrandStaffLayout.DiatonicStep * 6;
    private const double StaffSeparationOffset = GrandStaffLayout.DiatonicStep;
    private const double ViewY0 = -0.9;
    private const double ViewY1 = 0.9;
    private const int TrebleClefHeightInStaffSpaces = 7;
    private const int BassClefHeightInStaffSpaces = 3;
    private static readonly NoteValue[] supportedLiveNoteValues =
    [
        new(1),
        new(2),
        new(4),
        new(8),
        new(16),
    ];
    private static readonly int[] trebleSharpOffsets = [8, 5, 9, 6, 3, 7, 4];
    private static readonly int[] bassSharpOffsets = [6, 3, 7, 4, 8, 5, 9];
    private static readonly int[] trebleFlatOffsets = [4, 7, 3, 6, 2, 5, 1];
    private static readonly int[] bassFlatOffsets = [2, 5, 1, 4, 0, 3, -1];
    private static readonly NoteLetter[] sharpKeyLetters =
        [NoteLetter.F, NoteLetter.C, NoteLetter.G, NoteLetter.D, NoteLetter.A, NoteLetter.E, NoteLetter.B];
    private static readonly NoteLetter[] flatKeyLetters =
        [NoteLetter.B, NoteLetter.E, NoteLetter.A, NoteLetter.D, NoteLetter.G, NoteLetter.C, NoteLetter.F];
    private static readonly Staff[] signatureStaves = [Staff.Treble, Staff.Bass];

    internal static int ClampFirstVisibleMeasure(Score score, int requestedMeasure)
    {
        int lastMeasure = Math.Max(0, score.Measures.Count - 1);
        return Math.Clamp(requestedMeasure, 0, lastMeasure);
    }

    internal static GrandStaffScene BuildScore(
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null,
        IReadOnlyList<PerformedNote>? performedNotes = null,
        double? performedNoteBeats = null) =>
        ComposeScore(
            BuildStaticScoreParts(score, firstVisibleMeasure, verdicts),
            score,
            firstVisibleMeasure,
            cursorBeats,
            performedNotes,
            performedNoteBeats);

    /// <summary>
    /// Builds everything about a score's grand-staff rendering that does NOT depend on the
    /// playback cursor position: staff/barlines, ledger and beam geometry, notation glyphs, and
    /// note markers (including verdict coloring). Callers that re-render every tick only because
    /// the cursor moved (e.g. practice mode) can cache this result and skip straight to
    /// <see cref="ComposeScore"/> when the score, visible measure window, and verdicts are
    /// unchanged from the previous call — see <c>GrandStaffSceneCache</c>.
    /// </summary>
    internal static GrandStaffStaticScoreParts BuildStaticScoreParts(
        Score score,
        int firstVisibleMeasure,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null)
    {
        int clampedMeasure = ClampFirstVisibleMeasure(score, firstVisibleMeasure);
        var lines = CreateStaffLines();
        var glyphs = CreateClefGlyphs();
        AddScoreSignatures(glyphs, score);
        var renderedNotes = new List<GrandStaffNote>();

        var (barlineY0, barlineY1) = GetCursorLineYBounds();
        lines.AddRange(GrandStaffLayout.GetScoreBarlineXs(clampedMeasure, score.Measures.Count)
            .Where(x => x < GrandStaffLayout.ScoreX1)
            .Select(x => new GrandStaffLine(x, barlineY0, x, barlineY1, GrandStaffLineKind.Barline)));

        var visibleNotes = new List<(ScoreNote Note, ScoreNoteLayout Layout)>();
        foreach (var note in score.Measures.SelectMany(measure => measure.Notes))
        {
            if (GrandStaffLayout.GetScoreNoteLayout(note, score.TimeSignature, clampedMeasure) is not { } layout)
            {
                continue;
            }

            visibleNotes.Add((note, layout));
        }

        var beamOverrides = new Dictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)>();
        var beams = BuildBeams(visibleNotes, beamOverrides);
        foreach (var (note, layout) in visibleNotes)
        {
            Verdict? verdict = verdicts is not null && verdicts.TryGetValue(note, out var visibleVerdict)
                ? visibleVerdict
                : null;
            double noteY = SeparateStaffY(layout.Position.Y, layout.Position.Staff);
            bool isBeamed = beamOverrides.TryGetValue(note, out var beamOverride);

            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                layout.X,
                noteY,
                DurationSeconds: 0,
                IsActive: false,
                IsFilled: layout.HeadStyle == NoteHeadStyle.Filled,
                layout.HasStem,
                isBeamed ? beamOverride.Direction : layout.StemDirection,
                layout.HasDot,
                isBeamed ? layout.FlagCount - beamOverride.BeamCount : layout.FlagCount,
                verdict,
                StemEndY: isBeamed ? beamOverride.StemEndY : null));
            lines.AddRange(layout.Position.LedgerLineYs.Select(
                y => new GrandStaffLine(
                    layout.X - LedgerLineHalfWidth,
                    SeparateStaffY(y, layout.Position.Staff),
                    layout.X + LedgerLineHalfWidth,
                    SeparateStaffY(y, layout.Position.Staff),
                    GrandStaffLineKind.Ledger)));
            if (GetScoreAccidentalGlyph(note.Pitch, score.KeyFifths) is { } accidentalGlyph)
            {
                glyphs.Add(new GrandStaffGlyph(
                    accidentalGlyph,
                    layout.X - 0.055,
                    noteY,
                    GrandStaffGlyphKind.Accidental));
            }
        }

        return new GrandStaffStaticScoreParts(lines, glyphs, renderedNotes, beams);
    }

    /// <summary>
    /// Appends the playback cursor and held-note indicators onto a previously built
    /// <see cref="GrandStaffStaticScoreParts"/>, without rebuilding the static geometry.
    /// </summary>
    internal static GrandStaffScene ComposeScore(
        GrandStaffStaticScoreParts staticParts,
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats,
        IReadOnlyList<PerformedNote>? performedNotes = null,
        double? performedNoteBeats = null)
    {
        int clampedMeasure = ClampFirstVisibleMeasure(score, firstVisibleMeasure);
        float? cursorX = GetVisibleScoreX(cursorBeats, score.TimeSignature, clampedMeasure);
        double? indicatorBeats = performedNoteBeats ?? cursorBeats;
        float? indicatorX = performedNoteBeats.HasValue
            ? GetVisibleScoreX(performedNoteBeats, score.TimeSignature, clampedMeasure)
            : cursorX;

        PerformedNote[] heldNotes = performedNotes?
            .Where(note => note.ReleaseTime is null)
            .ToArray() ?? [];
        bool canShowHeldNotes = !indicatorBeats.HasValue || indicatorX.HasValue;
        if (!cursorX.HasValue && (!canShowHeldNotes || heldNotes.Length == 0))
        {
            return new GrandStaffScene(staticParts.Lines, staticParts.Glyphs, staticParts.Notes)
            {
                Beams = staticParts.Beams,
            };
        }

        var lines = new List<GrandStaffLine>(staticParts.Lines.Count + 1 + heldNotes.Length);
        lines.AddRange(staticParts.Lines);
        if (cursorX.HasValue)
        {
            var (cursorY0, cursorY1) = GetCursorLineYBounds();
            lines.Add(new GrandStaffLine(cursorX.Value, cursorY0, cursorX.Value, cursorY1, GrandStaffLineKind.Cursor));
        }

        IReadOnlyList<GrandStaffNote> notes = staticParts.Notes;
        if (canShowHeldNotes && heldNotes.Length > 0)
        {
            double noteX = indicatorX ?? GrandStaffLayout.ScoreX0;
            var composedNotes = new List<GrandStaffNote>(staticParts.Notes.Count + heldNotes.Length);
            composedNotes.AddRange(staticParts.Notes);
            foreach (var heldNote in heldNotes)
            {
                var position = GrandStaffLayout.GetLivePosition(heldNote.Pitch);
                double indicatorY = SeparateStaffY(position.Y, position.Staff);
                composedNotes.Add(new GrandStaffNote(
                    heldNote.Pitch.ToString(),
                    noteX,
                    indicatorY,
                    DurationSeconds: 0,
                    IsActive: true,
                    IsFilled: false));
                lines.AddRange(position.LedgerLineYs.Select(
                    y => new GrandStaffLine(
                        noteX - LedgerLineHalfWidth,
                        SeparateStaffY(y, position.Staff),
                        noteX + LedgerLineHalfWidth,
                        SeparateStaffY(y, position.Staff),
                        GrandStaffLineKind.Ledger)));
            }

            notes = composedNotes;
        }

        return new GrandStaffScene(lines, staticParts.Glyphs, notes)
        {
            Beams = staticParts.Beams,
        };
    }

    private static float? GetVisibleScoreX(
        double? beats,
        TimeSignature timeSignature,
        int firstVisibleMeasure)
    {
        if (!beats.HasValue)
        {
            return null;
        }

        float x = GrandStaffLayout.MapAbsoluteBeatToScoreX(beats.Value, timeSignature, firstVisibleMeasure);
        return x >= GrandStaffLayout.ScoreX0 && x <= GrandStaffLayout.ScoreX1
            ? x
            : null;
    }

    /// <summary>
    /// The Y span (in scene coordinates) that barlines and the playback cursor line run through.
    /// Also used by JS to draw the score-playback cursor on its own, off the C# tick loop.
    /// </summary>
    internal static (double Y0, double Y1) GetCursorLineYBounds() =>
        (SeparateStaffY(GrandStaffLayout.BassLineYs[0], Staff.Bass),
            SeparateStaffY(GrandStaffLayout.TrebleLineYs[^1], Staff.Treble));

    internal static GrandStaffScene Build(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        int? selectedOctave = null) =>
        Build(
            notes,
            currentTime,
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            selectedOctave);

    internal static GrandStaffScene Build(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo,
        int? selectedOctave = null)
    {
        var lines = CreateStaffLines();
        var glyphs = CreateClefGlyphs();
        AddTimeSignatureGlyphs(glyphs, timeSignature);
        AddLiveMeasureGrid(lines, currentTime, timeSignature, tempo);

        var renderedNotes = new List<GrandStaffNote>(notes.Count);
        foreach (var note in notes)
        {
            TimeSpan endTime = note.ReleaseTime ?? currentTime;
            var layout = GrandStaffLayout.GetLiveNoteLayout(
                note.Pitch,
                note.StartTime,
                endTime,
                currentTime,
                timeSignature,
                tempo);
            if (!layout.HasValue)
            {
                continue;
            }

            var position = layout.Value.Position;
            double noteY = SeparateStaffY(position.Y, position.Staff);
            double durationSeconds = Math.Max(0, endTime.TotalSeconds - note.StartTime.TotalSeconds);
            bool isActive = note.ReleaseTime is null;
            NoteValue? noteValue = isActive
                ? null
                : GetNearestLiveNoteValue(TimeSpan.FromSeconds(durationSeconds), timeSignature, tempo);
            bool isFilled = !noteValue.HasValue || noteValue.Value.Denominator >= 4;
            bool hasStem = noteValue.HasValue && noteValue.Value.Denominator != 1;
            int flagCount = noteValue?.Denominator switch
            {
                8 => 1,
                16 => 2,
                _ => 0,
            };
            var stemDirection = GrandStaffLayout.GetStemDirection(position);
            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                layout.Value.X,
                noteY,
                durationSeconds,
                IsActive: isActive,
                IsFilled: isFilled,
                HasStem: hasStem,
                StemDirection: stemDirection,
                FlagCount: flagCount,
                DurationEndX: isActive ? layout.Value.DurationEndX : null));
            lines.AddRange(position.LedgerLineYs.Select(
                y => new GrandStaffLine(
                    layout.Value.X - LedgerLineHalfWidth,
                    SeparateStaffY(y, position.Staff),
                    layout.Value.X + LedgerLineHalfWidth,
                    SeparateStaffY(y, position.Staff),
                    GrandStaffLineKind.Ledger)));
            if (position.NeedsAccidental)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetAccidentalGlyph(note.Pitch.Alter),
                    layout.Value.X - 0.055,
                    noteY,
                    GrandStaffGlyphKind.Accidental));
            }
        }

        var scene = new GrandStaffScene(lines, glyphs, renderedNotes, ShouldClipNotesAtClefs: true);
        return selectedOctave.HasValue
            ? FitToSelectedOctave(scene, selectedOctave.Value)
            : scene;
    }

    // These two templates depend on no per-call inputs at all (no score, no notes, no time) —
    // they are the same on every single call, forever. Computing them once and handing out a
    // shallow List copy (mutated further by callers via AddRange/Add) avoids repeating the same
    // LINQ/allocation work on every 16ms tick of the practice and live-keyboard refresh loops.
    // This is a memoized constant, not caller-visible mutable state: nothing ever assigns into
    // it based on a per-call input, and nothing outside this class can observe or mutate it.
    private static readonly IReadOnlyList<GrandStaffLine> baseStaffLines = BuildBaseStaffLines();
    private static readonly IReadOnlyList<GrandStaffGlyph> baseClefGlyphs = BuildBaseClefGlyphs();

    private static List<GrandStaffLine> CreateStaffLines() => new(baseStaffLines);

    private static List<GrandStaffGlyph> CreateClefGlyphs() => new(baseClefGlyphs);

    private static List<GrandStaffLine> BuildBaseStaffLines()
    {
        var lines = GrandStaffLayout.TrebleLineYs
            .Select(y => SeparateStaffY(y, Staff.Treble))
            .Concat(GrandStaffLayout.BassLineYs.Select(y => SeparateStaffY(y, Staff.Bass)))
            .Select(y => new GrandStaffLine(StaffX0, y, StaffX1, y, GrandStaffLineKind.Staff))
            .ToList();
        var (barlineY0, barlineY1) = GetCursorLineYBounds();
        lines.Add(new GrandStaffLine(StaffX0, barlineY0, StaffX0, barlineY1, GrandStaffLineKind.Barline));
        lines.Add(new GrandStaffLine(
            StaffX1 - EndingBarlineGap,
            barlineY0,
            StaffX1 - EndingBarlineGap,
            barlineY1,
            GrandStaffLineKind.Barline));
        lines.Add(new GrandStaffLine(StaffX1, barlineY0, StaffX1, barlineY1, GrandStaffLineKind.Barline));
        return lines;
    }

    private static void AddLiveMeasureGrid(
        List<GrandStaffLine> lines,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        var (barlineY0, barlineY1) = GetCursorLineYBounds();
        foreach (var gridLine in GrandStaffLayout.GetLiveMeasureGridLines(currentTime, timeSignature, tempo))
        {
            if (gridLine.Kind == GridLineKind.Barline && gridLine.X >= GrandStaffLayout.ScoreX1)
            {
                // Skip: CreateStaffLines() already draws the ending double barline at ScoreX1.
                continue;
            }

            var kind = gridLine.Kind switch
            {
                GridLineKind.Barline => GrandStaffLineKind.Barline,
                GridLineKind.Beat => GrandStaffLineKind.Beat,
                GridLineKind.Cursor => GrandStaffLineKind.Cursor,
                _ => throw new ArgumentOutOfRangeException(nameof(gridLine), gridLine.Kind, message: null),
            };
            lines.Add(new GrandStaffLine(gridLine.X, barlineY0, gridLine.X, barlineY1, kind));
        }
    }

    private static List<GrandStaffGlyph> BuildBaseClefGlyphs() =>
    [
        new(
            "𝄞",
            ClefX,
            SeparateStaffY(GrandStaffLayout.TrebleLineYs[2], Staff.Treble),
            GrandStaffGlyphKind.Clef,
            TrebleClefHeightInStaffSpaces * (GrandStaffLayout.TrebleLineYs[1] - GrandStaffLayout.TrebleLineYs[0])),
        new(
            "𝄢",
            ClefX,
            SeparateStaffY(
                (GrandStaffLayout.BassLineYs[2] + GrandStaffLayout.BassLineYs[3]) / 2,
                Staff.Bass),
            GrandStaffGlyphKind.Clef,
            BassClefHeightInStaffSpaces * (GrandStaffLayout.BassLineYs[1] - GrandStaffLayout.BassLineYs[0])),
    ];

    private static IReadOnlyList<GrandStaffBeam> BuildBeams(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides)
    {
        var beams = new List<GrandStaffBeam>();
        foreach (var measureStaff in notes.GroupBy(item => (item.Note.MeasureIndex, item.Note.Staff)))
        {
            var currentGroup = new List<(ScoreNote Note, ScoreNoteLayout Layout)>();
            foreach (var item in measureStaff.OrderBy(item => item.Note.BeatOffset))
            {
                switch (item.Note.BeamState)
                {
                    case BeamState.Begin:
                        currentGroup.Clear();
                        currentGroup.Add(item);
                        break;
                    case BeamState.Continue when currentGroup.Count > 0:
                        currentGroup.Add(item);
                        break;
                    case BeamState.End when currentGroup.Count > 0:
                        currentGroup.Add(item);
                        AddBeam(currentGroup, beams, beamOverrides);
                        currentGroup.Clear();
                        break;
                    default:
                        currentGroup.Clear();
                        break;
                }
            }
        }

        return beams;
    }

    private static void AddBeam(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> group,
        ICollection<GrandStaffBeam> beams,
        IDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides)
    {
        if (group.Count < 2)
        {
            return;
        }

        int beamCount = group.Min(item => item.Layout.FlagCount);
        if (beamCount == 0)
        {
            return;
        }

        Staff staff = group[0].Note.Staff;
        var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
        double averageY = group.Average(item => item.Layout.Position.Y);
        var automaticDirection = averageY < staffLines[2] ? StemDirection.Up : StemDirection.Down;
        var explicitDirections = group
            .Select(item => item.Note.StemDirection)
            .OfType<ScoreStemDirection>()
            .Distinct()
            .ToArray();
        var direction = explicitDirections.Length == 1
            ? group.First(item => item.Note.StemDirection == explicitDirections[0]).Layout.StemDirection
            : automaticDirection;
        double stemOffset = direction == StemDirection.Up ? StemLength : -StemLength;
        double x0 = group[0].Layout.X;
        double x1 = group[^1].Layout.X;
        double y0 = SeparateStaffY(group[0].Layout.Position.Y, staff) + stemOffset;
        double y1 = SeparateStaffY(group[^1].Layout.Position.Y, staff) + stemOffset;
        beams.Add(new GrandStaffBeam(x0, y0, x1, y1, beamCount, direction));

        foreach (var item in group)
        {
            double progress = x1 == x0 ? 0 : (item.Layout.X - x0) / (x1 - x0);
            double stemEndY = y0 + ((y1 - y0) * progress);
            beamOverrides[item.Note] = (direction, stemEndY, beamCount);
        }
    }

    private static void AddScoreSignatures(ICollection<GrandStaffGlyph> glyphs, Score score)
    {
        AddKeySignatureGlyphs(glyphs, score.KeyFifths);
        AddTimeSignatureGlyphs(glyphs, score.TimeSignature);
    }

    private static void AddKeySignatureGlyphs(ICollection<GrandStaffGlyph> glyphs, int keyFifths)
    {
        if (keyFifths == 0)
        {
            return;
        }

        int accidentalCount = Math.Abs(keyFifths);
        bool isSharp = keyFifths > 0;
        foreach (Staff staff in signatureStaves)
        {
            var offsets = (staff, isSharp) switch
            {
                (Staff.Treble, true) => trebleSharpOffsets,
                (Staff.Bass, true) => bassSharpOffsets,
                (Staff.Treble, false) => trebleFlatOffsets,
                _ => bassFlatOffsets,
            };
            double bottomLineY = staff == Staff.Treble
                ? GrandStaffLayout.TrebleLineYs[0]
                : GrandStaffLayout.BassLineYs[0];
            for (int index = 0; index < accidentalCount; index++)
            {
                glyphs.Add(new GrandStaffGlyph(
                    isSharp ? "♯" : "♭",
                    KeySignatureX0 + (index * KeySignatureXSpacing),
                    SeparateStaffY(bottomLineY + (offsets[index] * GrandStaffLayout.DiatonicStep), staff),
                    GrandStaffGlyphKind.KeySignature));
            }
        }
    }

    private static void AddTimeSignatureGlyphs(
        ICollection<GrandStaffGlyph> glyphs,
        TimeSignature timeSignature)
    {
        foreach (Staff staff in signatureStaves)
        {
            var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
            glyphs.Add(new GrandStaffGlyph(
                timeSignature.Numerator.ToString(CultureInfo.InvariantCulture),
                TimeSignatureX,
                SeparateStaffY(staffLines[3], staff),
                GrandStaffGlyphKind.TimeSignature));
            glyphs.Add(new GrandStaffGlyph(
                timeSignature.BeatNoteValue.Denominator.ToString(CultureInfo.InvariantCulture),
                TimeSignatureX,
                SeparateStaffY(staffLines[1], staff),
                GrandStaffGlyphKind.TimeSignature));
        }
    }

    private static GrandStaffScene FitToSelectedOctave(GrandStaffScene scene, int selectedOctave)
    {
        var yValues = new List<double>();
        foreach (var line in scene.Lines)
        {
            yValues.Add(line.Y0);
            yValues.Add(line.Y1);
        }

        yValues.AddRange(scene.Glyphs.Select(glyph => glyph.Y));
        yValues.AddRange(scene.Notes.Select(note => note.Y));
        for (int octave = selectedOctave; octave <= selectedOctave + 1; octave++)
        {
            var position = GrandStaffLayout.GetLivePosition(new Pitch(NoteLetter.C, 0, octave));
            yValues.Add(SeparateStaffY(position.Y, position.Staff));
            yValues.AddRange(position.LedgerLineYs.Select(y => SeparateStaffY(y, position.Staff)));
        }

        double margin = GrandStaffLayout.DiatonicStep * 2;
        double sourceY0 = yValues.Min() - margin;
        double sourceY1 = yValues.Max() + margin;
        double yScale = (ViewY1 - ViewY0) / (sourceY1 - sourceY0);
        double MapY(double y) =>
            ViewY0 + ((y - sourceY0) * yScale);

        return new GrandStaffScene(
            scene.Lines.Select(line => line with { Y0 = MapY(line.Y0), Y1 = MapY(line.Y1) }).ToArray(),
            scene.Glyphs.Select(glyph => glyph with
            {
                Y = MapY(glyph.Y),
                Height = glyph.Height * yScale,
            }).ToArray(),
            scene.Notes.Select(note => note with
            {
                Y = MapY(note.Y),
                StemEndY = note.StemEndY.HasValue ? MapY(note.StemEndY.Value) : null,
            }).ToArray(),
            scene.ShouldClipNotesAtClefs)
        {
            Beams = scene.Beams.Select(beam => beam with { Y0 = MapY(beam.Y0), Y1 = MapY(beam.Y1) }).ToArray(),
        };
    }

    private static double SeparateStaffY(double y, Staff staff) =>
        y + (staff == Staff.Treble ? StaffSeparationOffset : -StaffSeparationOffset);

    private static NoteValue GetNearestLiveNoteValue(
        TimeSpan duration,
        TimeSignature timeSignature,
        Tempo tempo) =>
        supportedLiveNoteValues.MinBy(noteValue =>
            Math.Abs((MusicalTime.ToDuration(noteValue, timeSignature, tempo) - duration).Ticks));

    private static string GetAccidentalGlyph(int alter) => alter switch
    {
        -2 => "𝄫",
        -1 => "♭",
        1 => "♯",
        2 => "𝄪",
        _ => string.Empty,
    };

    private static string? GetScoreAccidentalGlyph(Pitch pitch, int keyFifths)
    {
        var keyLetters = keyFifths >= 0 ? sharpKeyLetters : flatKeyLetters;
        int keyLetterIndex = Array.IndexOf(keyLetters, pitch.Letter);
        int expectedAlter = keyLetterIndex >= 0 && keyLetterIndex < Math.Abs(keyFifths)
            ? Math.Sign(keyFifths)
            : 0;
        if (pitch.Alter == expectedAlter)
        {
            return null;
        }

        return pitch.Alter == 0 ? "♮" : GetAccidentalGlyph(pitch.Alter);
    }
}
