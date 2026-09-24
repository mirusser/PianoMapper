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
    private const double TimeSignatureGapAfterKeySignature = 0.08;
    private const double TimeSignatureX = -0.59;
    private const double MeasureEdgeNoteClearance = 0.02;
    private const double OpeningBarlineLead = MeasureEdgeNoteClearance;
    private const double LedgerLineHalfWidth = 0.065;
    // Slightly less than one measured notehead width in scene-X units (empirically measured from
    // a rendered screenshot: pixel width of a notehead / measured scene-X-to-pixel scale, both
    // from the same canvas) — a full notehead width only makes the two noteheads' edges just
    // touch the shared stem from either side, which still reads as a visible gap between them;
    // this slightly tighter value makes their ink overlap, the standard engraving look for a
    // chord a 2nd apart. Do NOT derive this from a Y-axis quantity like
    // GrandStaffLayout.DiatonicStep or "one staff space": canvas.js maps scene-X and scene-Y to
    // pixels using independent scale factors (the canvas is wide and short, not square), so a
    // Y-sized quantity reused as an X quantity here previously rendered roughly 5-6x too wide.
    // Re-tune only by measuring a real render again, not by recomputing from Y or from a formula.
    private const double ChordNoteheadDisplacement = 0.016;
    // Minimum visual gap a displaced chord notehead must keep from its nearest same-staff
    // neighbor — see the clamp in ApplyChordLayout.
    private const double MinimumOnsetClearance = ChordNoteheadDisplacement * 0.15;
    // At most this fraction of a measure's total width may be spent on extra chord/accidental
    // spacing, combined across every dense onset in that measure — see BuildNotationSpacingAnchors.
    private const double MaxMeasureSpacingBudgetFraction = 0.2;
    // How far left of the notehead an accidental glyph's center sits, in scene-X units — tuned by
    // rendering a real accidental next to a real notehead and measuring the pixel gap, the same
    // way as ChordNoteheadDisplacement above. Do NOT derive this from a Y-axis quantity: it must
    // shrink to match AccidentalHeightInStaffSpaces below whenever that height changes, since a
    // taller glyph renders wider too.
    private const double AccidentalHorizontalOffset = 0.019;
    // Slightly larger than the key signature's own accidental glyphs
    // (KeySignatureHeightInStaffSpaces) so an inline accidental reads clearly next to its notehead.
    private const double AccidentalHeightInStaffSpaces = 2.6;
    // Smaller than a printed accidental: articulation/ornament marks (a dot, dash, wedge, "tr")
    // read clearly at a more modest size than an accidental glyph needs to stay legible.
    private const double NotationMarkHeightInStaffSpaces = 1.2;
    // How far left of the leftmost chord notehead an arpeggio mark sits — tuned the same way as
    // AccidentalHorizontalOffset, but a little wider since the mark itself has visual width (a
    // wavy line or bracket, not a thin single glyph).
    private const double ArpeggioMarkHorizontalOffset = 0.03;
    private const string RightHandFingeringPrefix = "R";
    private const string LeftHandFingeringPrefix = "L";
    // Vertical clearance (in staff spaces, same axis as the beam's own Y) between a beamed
    // tuplet's numeral and the beam itself — mirrors GrandStaffLayout.FermataClearanceInStaffSpaces'
    // role of pushing a glyph just outside the notation it annotates.
    private const double TupletGlyphClearanceInStaffSpaces = 0.6;
    private const double OctaveShiftClearanceInStaffSpaces = 1.5;
    private const double OctaveShiftNumeralHalfHeightInStaffSpaces = 0.75;
    private const double ViewY0 = -0.9;
    private const double ViewY1 = 0.9;
    private const int TrebleClefHeightInStaffSpaces = 7;
    private const int BassClefHeightInStaffSpaces = 3;
    private const int KeySignatureHeightInStaffSpaces = 2;
    private const int TimeSignatureHeightInStaffSpaces = 2;
    private static readonly NoteValue[] supportedLiveNoteValues =
    [
        new(1),
        new(2),
        new(4),
        new(8),
        new(16),
        new(1, 1),
        new(2, 1),
        new(4, 1),
        new(8, 1),
        new(16, 1),
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
        double? performedNoteBeats = null,
        bool showNoteLabels = true,
        bool showFingerings = true,
        IReadOnlySet<ScoreNote>? expectedNotes = null,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount) =>
        ComposeScore(
            BuildStaticScoreParts(
                score,
                firstVisibleMeasure,
                verdicts,
                showNoteLabels,
                showFingerings,
                expectedNotes,
                visibleMeasureCount),
            score,
            firstVisibleMeasure,
            cursorBeats,
            performedNotes,
            performedNoteBeats,
            showNoteLabels,
            visibleMeasureCount);

    /// <summary>
    /// Builds everything about a score's grand-staff rendering that does NOT depend on the
    /// playback cursor position: staff/barlines, ledger and beam geometry, notation glyphs, and
    /// note markers (including verdict coloring and the currently expected notes). Callers that
    /// re-render every tick only because
    /// the cursor moved (e.g. practice mode) can cache this result and skip straight to
    /// <see cref="ComposeScore"/> when the score, visible measure window, and verdicts are
    /// unchanged from the previous call — see <c>GrandStaffSceneCache</c>.
    /// </summary>
    internal static GrandStaffStaticScoreParts BuildStaticScoreParts(
        Score score,
        int firstVisibleMeasure,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null,
        bool showNoteLabels = true,
        bool showFingerings = true,
        IReadOnlySet<ScoreNote>? expectedNotes = null,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        int clampedMeasure = ClampFirstVisibleMeasure(score, firstVisibleMeasure);
        var visibleNotes = new List<(ScoreNote Note, ScoreNoteLayout Layout)>();
        var visibleNoteAddresses = new List<ScoreNoteAddress>();
        // GetScoreNoteLayout below culls any note whose MeasureIndex falls outside
        // [clampedMeasure, clampedMeasure + visibleMeasureCount), so only that range can ever
        // contribute a note — bound the loop to it instead of scanning the whole score, which
        // otherwise makes every measure-window advance cost O(notes in the entire piece).
        int lastMeasureIndexExclusive = Math.Min(
            score.Measures.Count,
            clampedMeasure + visibleMeasureCount);
        for (int measureIndex = clampedMeasure; measureIndex < lastMeasureIndexExclusive; measureIndex++)
        {
            ScoreMeasure measure = score.Measures[measureIndex];
            bool hasBassRegisterNote = measure.Notes.Any(candidate =>
                candidate.Staff == Staff.Bass &&
                GrandStaffLayout.GetLivePosition(GrandStaffLayout.GetNotatedPitch(candidate)).Staff == Staff.Bass);
            for (int noteIndex = 0; noteIndex < measure.Notes.Count; noteIndex++)
            {
                ScoreNote note = measure.Notes[noteIndex];
                Staff notationStaff = note.Staff == Staff.Bass && hasBassRegisterNote
                    ? Staff.Bass
                    : GrandStaffLayout.GetLivePosition(GrandStaffLayout.GetNotatedPitch(note)).Staff;
                ScoreNote notationNote = note with { Staff = notationStaff };
                if (GrandStaffLayout.GetScoreNoteLayout(
                        notationNote,
                        score.TimeSignature,
                        clampedMeasure,
                        visibleMeasureCount) is not { } layout)
                {
                    continue;
                }

                float renderedX = MapScoreNotationBeatToX(
                    measure,
                    note.MeasureIndex,
                    note.BeatOffset,
                    score.TimeSignature,
                    clampedMeasure,
                    visibleMeasureCount);
                visibleNotes.Add((note, layout with { X = renderedX }));
                visibleNoteAddresses.Add(new ScoreNoteAddress(measureIndex, noteIndex));
            }
        }

        var glyphs = CreateClefGlyphs();
        AddScoreSignatures(glyphs, score);

        // Score pitch determines notation placement for bass voices without a bass-register anchor.
        // The source hand still controls R/L fingerings independently of notation placement.
        var beamOverrides = new Dictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)>();
        IReadOnlyList<GrandStaffBeam> beams = BuildBeams(visibleNotes, beamOverrides, glyphs);
        var chordOverrides = new Dictionary<ScoreNote, ChordNoteOverride>();
        ApplyChordLayout(visibleNotes, chordOverrides, beamOverrides);
        int[] labelRowIndexes = GrandStaffLayout.GetLabelRowIndexes(visibleNotes);
        double trebleNotationBottomY = IncludeDownwardOctaveShiftInNotationBottom(
            Staff.Treble,
            visibleNotes,
            GrandStaffLayout.GetNotationBottomY(Staff.Treble, visibleNotes, beamOverrides));
        AnnotationRows trebleAnnotationRows = GrandStaffLayout.GetAnnotationRows(
            Staff.Treble,
            visibleNotes,
            trebleNotationBottomY,
            showNoteLabels ? GrandStaffLayout.GetLabelRowCount(Staff.Treble, visibleNotes, labelRowIndexes) : 0,
            showFingerings);
        double bassNotationBottomY = IncludeDownwardOctaveShiftInNotationBottom(
            Staff.Bass,
            visibleNotes,
            GrandStaffLayout.GetNotationBottomY(Staff.Bass, visibleNotes, beamOverrides));
        AnnotationRows bassAnnotationRows = GrandStaffLayout.GetAnnotationRows(
            Staff.Bass,
            visibleNotes,
            bassNotationBottomY,
            showNoteLabels ? GrandStaffLayout.GetLabelRowCount(Staff.Bass, visibleNotes, labelRowIndexes) : 0,
            showFingerings);

        var lines = CreateStaffLines();
        var renderedNotes = new List<GrandStaffNote>();

        var (barlineY0, barlineY1) = GetCursorLineYBounds();
        lines.AddRange(GrandStaffLayout.GetScoreBarlineXs(clampedMeasure, score.Measures.Count, visibleMeasureCount)
            .Where(x => x < GrandStaffLayout.ScoreX1)
            .Select((x, boundary) =>
            {
                double barlineX = boundary == 0 && clampedMeasure < score.Measures.Count
                    ? x - OpeningBarlineLead
                    : x;
                return new GrandStaffLine(
                    barlineX,
                    barlineY0,
                    barlineX,
                    barlineY1,
                    GrandStaffLineKind.Barline);
            }));

        for (int visibleNoteIndex = 0; visibleNoteIndex < visibleNotes.Count; visibleNoteIndex++)
        {
            var (note, layout) = visibleNotes[visibleNoteIndex];
            bool suppressChordStem = false;
            StemDirection? chordDirectionOverride = null;
            if (chordOverrides.TryGetValue(note, out var chordOverride))
            {
                layout = layout with { X = layout.X + (float)chordOverride.XOffset };
                suppressChordStem = chordOverride.SuppressStem;
                chordDirectionOverride = chordOverride.DirectionOverride;
            }

            Verdict? verdict = verdicts is not null && verdicts.TryGetValue(note, out var visibleVerdict)
                ? visibleVerdict
                : null;
            AnnotationRows annotationRows = layout.Position.Staff == Staff.Treble
                ? trebleAnnotationRows
                : bassAnnotationRows;
            double noteY = GrandStaffLayout.SeparateStaffY(layout.Position.Y, layout.Position.Staff);
            bool isBeamed = beamOverrides.TryGetValue(note, out var beamOverride);
            double scoreOnsetBeats = ScoreDerivation.GetOnsetBeats(note, score.TimeSignature);
            double scoreEndBeats = scoreOnsetBeats + MusicalTime.GetBeats(note.NoteValue, score.TimeSignature);
            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                layout.X,
                noteY,
                DurationSeconds: 0,
                IsActive: expectedNotes?.Contains(note) == true,
                IsFilled: layout.HeadStyle == NoteHeadStyle.Filled,
                HasStem: !suppressChordStem && layout.HasStem,
                isBeamed ? beamOverride.Direction : chordDirectionOverride ?? layout.StemDirection,
                layout.HasDot,
                FlagCount: suppressChordStem ? 0 : (isBeamed ? layout.FlagCount - beamOverride.BeamCount : layout.FlagCount),
                verdict,
                StemEndY: isBeamed ? beamOverride.StemEndY : null,
                LabelY: showNoteLabels && annotationRows.LabelY is { } labelY
                    ? labelY - (labelRowIndexes[visibleNoteIndex] * GrandStaffLayout.LabelRowSeparation)
                    : null,
                ScoreOnsetBeats: scoreOnsetBeats,
                ScoreEndBeats: scoreEndBeats,
                Fingering: !showFingerings || note.Fingering is null
                    ? null
                    : GetFingeringLabel(note.Fingering.Number, note.Staff),
                FingeringY: !showFingerings || note.Fingering is null
                    ? null
                    : annotationRows.FingeringY,
                Address: visibleNoteAddresses[visibleNoteIndex]));
            lines.AddRange(layout.Position.LedgerLineYs.Select(
                y => new GrandStaffLine(
                    layout.X - LedgerLineHalfWidth,
                    GrandStaffLayout.SeparateStaffY(y, layout.Position.Staff),
                    layout.X + LedgerLineHalfWidth,
                    GrandStaffLayout.SeparateStaffY(y, layout.Position.Staff),
                    GrandStaffLineKind.Ledger)));
            string? accidentalGlyph = note.Accidental is { } accidental
                ? GetAccidentalGlyph(accidental)
                : GetScoreAccidentalGlyph(note.Pitch, score.KeyFifths);
            if (accidentalGlyph is not null)
            {
                glyphs.Add(new GrandStaffGlyph(
                    accidentalGlyph,
                    layout.X - AccidentalHorizontalOffset,
                    noteY,
                    GrandStaffGlyphKind.Accidental,
                    AccidentalHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(layout.Position.Staff),
                    IsActive: expectedNotes?.Contains(note) == true,
                    Verdict: verdict));
            }

            if (note.Fermata is { } fermata)
            {
                glyphs.Add(new GrandStaffGlyph(
                    fermata == ScoreFermata.Upright ? "𝄐" : "𝄑",
                    layout.X,
                    GrandStaffLayout.GetFermataY(fermata, noteY, layout, isBeamed ? beamOverride.StemEndY : null),
                    GrandStaffGlyphKind.Fermata,
                    GrandStaffLayout.FermataHeightInStaffSpaces
                        * GrandStaffLayout.GetRenderedStaffSpace(layout.Position.Staff)));
            }

            double? beamStemEndY = isBeamed ? beamOverride.StemEndY : null;
            if (note.Articulation is { } articulation)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetArticulationGlyph(articulation),
                    layout.X,
                    GrandStaffLayout.GetPointGlyphY(noteY, layout, beamStemEndY),
                    GrandStaffGlyphKind.Articulation,
                    NotationMarkHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(layout.Position.Staff)));
            }

            if (note.Ornament is { } ornament)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetOrnamentGlyph(ornament),
                    layout.X,
                    GrandStaffLayout.GetPointGlyphY(noteY, layout, beamStemEndY),
                    GrandStaffGlyphKind.Ornament,
                    NotationMarkHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(layout.Position.Staff)));
            }

            if (note.AccidentalMark is { } accidentalMark)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetAccidentalGlyph(accidentalMark),
                    layout.X,
                    GrandStaffLayout.GetPointGlyphY(noteY, layout, beamStemEndY),
                    GrandStaffGlyphKind.AccidentalMark,
                    AccidentalHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(layout.Position.Staff)));
            }
        }

        IReadOnlyList<GrandStaffBand> bands = BuildAnnotationBands(
            trebleAnnotationRows,
            bassAnnotationRows);
        IReadOnlyList<GrandStaffSlur> slurs = BuildSlurs(visibleNotes, renderedNotes);
        IReadOnlyList<GrandStaffArpeggioMark> arpeggioMarks = BuildArpeggioMarks(visibleNotes, renderedNotes);
        lines.AddRange(BuildGlissandoLines(visibleNotes, renderedNotes));
        AddOctaveShiftMarks(visibleNotes, renderedNotes, lines, glyphs);
        return new GrandStaffStaticScoreParts(
            lines,
            glyphs,
            renderedNotes,
            beams,
            bands,
            trebleAnnotationRows.LabelY,
            bassAnnotationRows.LabelY,
            slurs,
            arpeggioMarks);
    }

    /// <summary>
    /// One vertical arpeggio mark per real MusicXML chord (a contiguous
    /// <see cref="ScoreNote.IsChordContinuation"/> run — see <see cref="BuildChordGroups"/>, the
    /// same anchor <see cref="ApplyChordLayout"/> uses for notehead displacement) that carries an
    /// <see cref="ScoreNote.Arpeggio"/> mark on any member. A single non-chord note carrying an
    /// arpeggio mark (musically meaningless but not disallowed by the MusicXML schema) renders
    /// nothing, resolving Task 9's open question toward "ignore" rather than "throw": it is valid,
    /// already-imported data, not a new error condition to invent at render time.
    /// </summary>
    private static IReadOnlyList<GrandStaffArpeggioMark> BuildArpeggioMarks(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IReadOnlyList<GrandStaffNote> renderedNotes)
    {
        var marks = new List<GrandStaffArpeggioMark>();
        int index = 0;
        foreach (var group in BuildChordGroups(notes))
        {
            int groupStart = index;
            index += group.Count;
            if (group.Count < 2)
            {
                continue;
            }

            ScoreArpeggio? arpeggio = group
                .Select(item => item.Note.Arpeggio)
                .FirstOrDefault(value => value is not null);
            if (arpeggio is not { } markKind)
            {
                continue;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            for (int memberIndex = groupStart; memberIndex < index; memberIndex++)
            {
                minX = Math.Min(minX, renderedNotes[memberIndex].X);
                minY = Math.Min(minY, renderedNotes[memberIndex].Y);
                maxY = Math.Max(maxY, renderedNotes[memberIndex].Y);
            }

            marks.Add(new GrandStaffArpeggioMark(
                minX - ArpeggioMarkHorizontalOffset,
                minY,
                maxY,
                markKind == ScoreArpeggio.NonArpeggiate));
        }

        return marks;
    }

    /// <summary>
    /// Matches slur-start notes to slur-stop notes by <see cref="ScoreSlur.Number"/> (not a
    /// sequential "next note that ends" scan — a staff can have multiple simultaneously-open,
    /// overlapping slurs) within the same notation staff, and resolves each matched pair to the
    /// two notes' already-finalized rendered positions (post chord-displacement X).
    /// </summary>
    private static IReadOnlyList<GrandStaffSlur> BuildSlurs(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IReadOnlyList<GrandStaffNote> renderedNotes)
    {
        var slurs = new List<GrandStaffSlur>();
        for (int startIndex = 0; startIndex < notes.Count; startIndex++)
        {
            if (notes[startIndex].Note.Slur is not { IsStart: true } start)
            {
                continue;
            }

            Staff staff = notes[startIndex].Layout.Position.Staff;
            for (int stopIndex = startIndex + 1; stopIndex < notes.Count; stopIndex++)
            {
                if (notes[stopIndex].Layout.Position.Staff != staff ||
                    notes[stopIndex].Note.Slur is not { IsStart: false, Number: var stopNumber } ||
                    stopNumber != start.Number)
                {
                    continue;
                }

                slurs.Add(new GrandStaffSlur(
                    renderedNotes[startIndex].X,
                    renderedNotes[startIndex].Y,
                    renderedNotes[stopIndex].X,
                    renderedNotes[stopIndex].Y,
                    GetSlurCurveDirection(notes, startIndex, stopIndex, staff)));
                break;
            }
        }

        return slurs;
    }

    /// <summary>
    /// Matches glissando/slide-start notes to their matching stop by <see cref="ScoreGlissando.Number"/>
    /// (same by-number matching as <see cref="BuildSlurs"/>, not a sequential "next note" scan),
    /// and draws a straight line directly between the two noteheads' final rendered positions.
    /// Glissando and slide render identically in v1 — MusicXML distinguishes them semantically
    /// (a discrete pitch slide vs. a continuous one), but a beginner-learning grand staff doesn't
    /// need visually distinct treatments yet; both use the same <see cref="GrandStaffLineKind.Glissando"/>
    /// line kind. A straight connecting line is a much simpler shape than a slur's arc, so it
    /// reuses the existing <see cref="GrandStaffLine"/>/<c>drawLine</c> machinery directly rather
    /// than a new record/draw-function pair.
    /// </summary>
    private static IReadOnlyList<GrandStaffLine> BuildGlissandoLines(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IReadOnlyList<GrandStaffNote> renderedNotes)
    {
        var lines = new List<GrandStaffLine>();
        for (int startIndex = 0; startIndex < notes.Count; startIndex++)
        {
            if (notes[startIndex].Note.Glissando is not { IsStart: true } start)
            {
                continue;
            }

            Staff staff = notes[startIndex].Layout.Position.Staff;
            for (int stopIndex = startIndex + 1; stopIndex < notes.Count; stopIndex++)
            {
                if (notes[stopIndex].Layout.Position.Staff != staff ||
                    notes[stopIndex].Note.Glissando is not { IsStart: false, Number: var stopNumber } ||
                    stopNumber != start.Number)
                {
                    continue;
                }

                lines.Add(new GrandStaffLine(
                    renderedNotes[startIndex].X,
                    renderedNotes[startIndex].Y,
                    renderedNotes[stopIndex].X,
                    renderedNotes[stopIndex].Y,
                    GrandStaffLineKind.Glissando));
                break;
            }
        }

        return lines;
    }

    private static void AddOctaveShiftMarks(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IReadOnlyList<GrandStaffNote> renderedNotes,
        ICollection<GrandStaffLine> lines,
        ICollection<GrandStaffGlyph> glyphs)
    {
        foreach (Staff staff in Enum.GetValues<Staff>())
        {
            int[] orderedIndexes = Enumerable.Range(0, notes.Count)
                .Where(index => notes[index].Layout.Position.Staff == staff)
                .OrderBy(index => notes[index].Note.MeasureIndex)
                .ThenBy(index => notes[index].Note.BeatOffset)
                .ThenBy(index => index)
                .ToArray();
            int orderedIndex = 0;
            while (orderedIndex < orderedIndexes.Length)
            {
                int shift = notes[orderedIndexes[orderedIndex]].Note.SoundingOctavesAboveNotated;
                if (shift == 0)
                {
                    orderedIndex++;
                    continue;
                }

                int runEnd = orderedIndex;
                while (runEnd + 1 < orderedIndexes.Length &&
                    notes[orderedIndexes[runEnd + 1]].Note.SoundingOctavesAboveNotated == shift)
                {
                    runEnd++;
                }

                // Start/stop boundary markers are intentionally not stored on ScoreNote. As a
                // result, immediately adjacent spans with the same shift merge into one visual run.
                int firstNoteIndex = orderedIndexes[orderedIndex];
                int lastNoteIndex = orderedIndexes[runEnd];
                double y = GetOctaveShiftY(staff, shift);
                string numeral = Math.Abs(shift) switch
                {
                    1 => "8",
                    2 => "15",
                    3 => "22",
                    _ => throw new InvalidOperationException($"Unsupported octave shift value '{shift}'."),
                };

                lines.Add(new GrandStaffLine(
                    renderedNotes[firstNoteIndex].X,
                    y,
                    renderedNotes[lastNoteIndex].X,
                    y,
                    GrandStaffLineKind.OctaveShift));
                glyphs.Add(new GrandStaffGlyph(
                    numeral,
                    renderedNotes[firstNoteIndex].X,
                    y,
                    GrandStaffGlyphKind.OctaveShiftNumeral));
                orderedIndex = runEnd + 1;
            }
        }
    }

    private static double IncludeDownwardOctaveShiftInNotationBottom(
        Staff staff,
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        double notationBottomY)
    {
        if (!notes.Any(item =>
                item.Layout.Position.Staff == staff &&
                item.Note.SoundingOctavesAboveNotated < 0))
        {
            return notationBottomY;
        }

        double numeralBottomY = GetOctaveShiftY(staff, shift: -1) -
            (OctaveShiftNumeralHalfHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(staff));
        return Math.Min(notationBottomY, numeralBottomY);
    }

    private static double GetOctaveShiftY(Staff staff, int shift)
    {
        IReadOnlyList<float> staffLineYs = staff == Staff.Treble
            ? GrandStaffLayout.TrebleLineYs
            : GrandStaffLayout.BassLineYs;
        double staffEdgeY = shift > 0 ? staffLineYs[^1] : staffLineYs[0];
        return GrandStaffLayout.SeparateStaffY(staffEdgeY, staff) +
            (Math.Sign(shift) * OctaveShiftClearanceInStaffSpaces *
                GrandStaffLayout.GetRenderedStaffSpace(staff));
    }

    /// <summary>
    /// A slur curves on the opposite side from where its spanned notes automatically sit relative
    /// to the staff's middle line — the same "opposite the notes' own stem direction" convention
    /// <see cref="GrandStaffSceneBuilder.Build"/> already uses for a live tie's curve direction.
    /// </summary>
    private static StemDirection GetSlurCurveDirection(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        int startIndex,
        int stopIndex,
        Staff staff)
    {
        var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
        double averageY = Enumerable.Range(startIndex, (stopIndex - startIndex) + 1)
            .Where(index => notes[index].Layout.Position.Staff == staff)
            .Average(index => notes[index].Layout.Position.Y);
        var automaticDirection = averageY < staffLines[2] ? StemDirection.Up : StemDirection.Down;
        return automaticDirection == StemDirection.Up ? StemDirection.Down : StemDirection.Up;
    }

    private static IReadOnlyList<GrandStaffBand> BuildAnnotationBands(params AnnotationRows[] rows)
    {
        var bands = new List<GrandStaffBand>(rows.Length);
        foreach (AnnotationRows row in rows)
        {
            if (row.BandY0 is not { } bandY0 || row.BandY1 is not { } bandY1)
            {
                continue;
            }

            bands.Add(new GrandStaffBand(StaffX0, bandY0, StaffX1, bandY1));
        }

        return bands;
    }

    private static string GetFingeringLabel(int number, Staff staff)
    {
        string handPrefix = staff == Staff.Treble ? RightHandFingeringPrefix : LeftHandFingeringPrefix;
        return handPrefix + number.ToString(CultureInfo.InvariantCulture);
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
        double? performedNoteBeats = null,
        bool showNoteLabels = true,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        int clampedMeasure = ClampFirstVisibleMeasure(score, firstVisibleMeasure);
        float? cursorX = GetVisibleScoreX(score, cursorBeats, clampedMeasure, visibleMeasureCount);
        double? indicatorBeats = performedNoteBeats ?? cursorBeats;
        float? indicatorX = performedNoteBeats.HasValue
            ? GetVisibleScoreX(score, performedNoteBeats, clampedMeasure, visibleMeasureCount)
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
                Bands = staticParts.Bands,
                Slurs = staticParts.Slurs,
                ArpeggioMarks = staticParts.ArpeggioMarks,
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
                double indicatorY = GrandStaffLayout.SeparateStaffY(position.Y, position.Staff);
                double? labelY = position.Staff == Staff.Treble
                    ? staticParts.TrebleAnnotationLabelY
                    : staticParts.BassAnnotationLabelY;
                composedNotes.Add(new GrandStaffNote(
                    heldNote.Pitch.ToString(),
                    noteX,
                    indicatorY,
                    DurationSeconds: 0,
                    IsActive: true,
                    IsFilled: false,
                    LabelY: showNoteLabels ? labelY ?? GrandStaffLayout.GetStaffLabelY(position.Staff) : null));
                lines.AddRange(position.LedgerLineYs.Select(
                    y => new GrandStaffLine(
                        noteX - LedgerLineHalfWidth,
                        GrandStaffLayout.SeparateStaffY(y, position.Staff),
                        noteX + LedgerLineHalfWidth,
                        GrandStaffLayout.SeparateStaffY(y, position.Staff),
                        GrandStaffLineKind.Ledger)));
            }

            notes = composedNotes;
        }

        return new GrandStaffScene(lines, staticParts.Glyphs, notes)
        {
            Beams = staticParts.Beams,
            Bands = staticParts.Bands,
            Slurs = staticParts.Slurs,
            ArpeggioMarks = staticParts.ArpeggioMarks,
        };
    }

    private static float? GetVisibleScoreX(
        Score score,
        double? beats,
        int firstVisibleMeasure,
        int visibleMeasureCount)
    {
        if (!beats.HasValue)
        {
            return null;
        }

        TimeSignature timeSignature = score.TimeSignature;
        double windowStartBeat = firstVisibleMeasure * timeSignature.Numerator;
        double windowEndBeat = (firstVisibleMeasure + visibleMeasureCount)
            * timeSignature.Numerator;
        if (beats.Value < windowStartBeat || beats.Value >= windowEndBeat)
        {
            return null;
        }

        int measureIndex = (int)Math.Floor(beats.Value / timeSignature.Numerator);
        double beatOffset = beats.Value - (measureIndex * timeSignature.Numerator);
        ScoreMeasure? measure = measureIndex < score.Measures.Count ? score.Measures[measureIndex] : null;
        return MapScoreNotationBeatToX(
            measure,
            measureIndex,
            beatOffset,
            timeSignature,
            firstVisibleMeasure,
            visibleMeasureCount);
    }

    /// <summary>
    /// Maps a beat position within a measure to scene-X for score notation (as opposed to
    /// <see cref="GrandStaffLayout.MapAbsoluteBeatToScoreX"/>, used by the live/rolling piano-roll
    /// view, which stays purely beat-proportional and is untouched by the spacing below).
    /// Beat-proportional by default, widened around onsets that need extra room — a displaced
    /// chord second or a printed accidental — via <see cref="BuildNotationSpacingAnchors"/>, then
    /// rescaled to still land exactly on <paramref name="measureIndex"/>'s barlines if that
    /// widening would otherwise overflow the measure.
    /// </summary>
    private static float MapScoreNotationBeatToX(
        ScoreMeasure? measure,
        int measureIndex,
        double beatOffset,
        TimeSignature timeSignature,
        int firstVisibleMeasure,
        int visibleMeasureCount = GrandStaffLayout.DefaultVisibleMeasureCount)
    {
        double measureStartX = GrandStaffLayout.MapScoreOnsetToX(
            measureIndex, 0, timeSignature, firstVisibleMeasure, visibleMeasureCount);
        double measureEndX = GrandStaffLayout.MapScoreOnsetToX(
            measureIndex + 1, 0, timeSignature, firstVisibleMeasure, visibleMeasureCount);
        double noteAreaStartX = measureIndex == firstVisibleMeasure
            ? measureStartX
            : measureStartX + MeasureEdgeNoteClearance;
        double noteAreaEndX = measureEndX - MeasureEdgeNoteClearance;
        double noteAreaWidth = noteAreaEndX - noteAreaStartX;
        double fraction = measure is null
            ? beatOffset / timeSignature.Numerator
            : GetNotationBeatFraction(measure, beatOffset, timeSignature, noteAreaWidth);
        return (float)Math.Clamp(
            noteAreaStartX + (fraction * noteAreaWidth),
            noteAreaStartX,
            noteAreaEndX);
    }

    private static double GetNotationBeatFraction(
        ScoreMeasure measure,
        double beatOffset,
        TimeSignature timeSignature,
        double noteAreaWidth)
    {
        var anchors = BuildNotationSpacingAnchors(measure, timeSignature, noteAreaWidth);
        for (int index = 1; index < anchors.Count; index++)
        {
            if (beatOffset <= anchors[index].Beat || index == anchors.Count - 1)
            {
                (double beat0, double fraction0) = anchors[index - 1];
                (double beat1, double fraction1) = anchors[index];
                double span = beat1 - beat0;
                double progress = span <= 0 ? 0 : (beatOffset - beat0) / span;
                return fraction0 + (progress * (fraction1 - fraction0));
            }
        }

        return anchors[^1].Fraction;
    }

    /// <summary>
    /// Piecewise-linear (beat, fraction-of-measure-width) anchors for one measure's notation
    /// spacing: beat-proportional by default, with extra fraction inserted before/after any onset
    /// that <see cref="RequiresExtraNotationWidth"/> flags, then rescaled back to [0, 1] if that
    /// widening would push the last anchor past the measure's own width.
    /// </summary>
    private static IReadOnlyList<(double Beat, double Fraction)> BuildNotationSpacingAnchors(
        ScoreMeasure measure,
        TimeSignature timeSignature,
        double noteAreaWidth)
    {
        double[] onsetBeats = measure.Notes
            .Select(note => note.BeatOffset)
            .Concat(measure.Rests.Select(rest => rest.BeatOffset))
            .Distinct()
            .OrderBy(beat => beat)
            .ToArray();
        if (onsetBeats.Length == 0)
        {
            return [(0, 0), (timeSignature.Numerator, 1)];
        }

        bool[] needsExtraWidth = onsetBeats
            .Select(beat => RequiresExtraNotationWidth(measure, beat))
            .ToArray();

        var beats = new List<double>(onsetBeats.Length + 2) { 0 };
        beats.AddRange(onsetBeats.Where(beat => beat > 0));
        if (beats[^1] < timeSignature.Numerator)
        {
            beats.Add(timeSignature.Numerator);
        }

        // Reserve more than exactly one displacement's worth of clearance per gap: a chord's
        // displaced member can move toward either neighbor, and reserving the same amount it
        // moves by leaves zero margin — floating-point rounding alone can then tip the displaced
        // notehead behind its neighbor instead of just touching it.
        bool[] intervalNeedsGap = new bool[beats.Count];
        for (int index = 1; index < beats.Count; index++)
        {
            int onsetIndex = Array.IndexOf(onsetBeats, beats[index]);
            int previousOnsetIndex = Array.IndexOf(onsetBeats, beats[index - 1]);
            intervalNeedsGap[index] = (onsetIndex >= 0 && needsExtraWidth[onsetIndex]) ||
                (previousOnsetIndex >= 0 && needsExtraWidth[previousOnsetIndex]);
        }

        double rawGapFraction = noteAreaWidth > 0
            ? ChordNoteheadDisplacement / noteAreaWidth
            : 0;
        int gapCount = intervalNeedsGap.Count(needsGap => needsGap);
        double totalRawExtra = gapCount * rawGapFraction;
        // Cap how much of the measure's total width chord/accidental clearance can consume: a
        // busy measure can have several dense onsets, each individually wanting close to a full
        // notehead-width of clearance on both sides, which — left uncapped — forces the
        // rescale-to-fit step below to crush every other (unrelated) note in the measure to
        // compensate. Scale every gap down proportionally instead once the combined ask exceeds
        // this budget, so a busy measure degrades toward plain proportional spacing rather than
        // toward a compressed mess.
        double gapFraction = totalRawExtra > MaxMeasureSpacingBudgetFraction && totalRawExtra > 0
            ? rawGapFraction * (MaxMeasureSpacingBudgetFraction / totalRawExtra)
            : rawGapFraction;

        // Accumulate each interval's own proportional share plus any extra clearance — never
        // compare against an absolute baseline fraction. Comparing against baseline let one
        // widened gap permanently outrun every later onset's baseline for the rest of the
        // measure once accumulated, collapsing them onto the same X (see the "foo1" bug report:
        // a single dense chord early in a measure pulled every subsequent note onto one X).
        var fractions = new double[beats.Count];
        for (int index = 1; index < beats.Count; index++)
        {
            double naturalGap = (beats[index] - beats[index - 1]) / timeSignature.Numerator;
            fractions[index] = fractions[index - 1] + naturalGap + (intervalNeedsGap[index] ? gapFraction : 0);
        }

        double lastFraction = fractions[^1];
        if (lastFraction > 1)
        {
            double scale = 1 / lastFraction;
            for (int index = 0; index < fractions.Length; index++)
            {
                fractions[index] *= scale;
            }
        }

        var anchors = new List<(double Beat, double Fraction)>(beats.Count);
        for (int index = 0; index < beats.Count; index++)
        {
            anchors.Add((beats[index], fractions[index]));
        }

        return anchors;
    }

    /// <summary>
    /// Whether the notation at this onset (a printed accidental, or two same-staff notes a 2nd
    /// apart that <see cref="ApplyChordLayout"/> will displace — whether they're one real chord or
    /// independent voices sharing the onset, see <see cref="ApplyVoiceOverlapDisplacement"/>) needs
    /// more than the default beat-proportional gap from its neighboring onset to avoid visually
    /// overlapping it.
    /// </summary>
    private static bool RequiresExtraNotationWidth(ScoreMeasure measure, double beatOffset)
    {
        ScoreNote[] notesAtOnset = measure.Notes.Where(note => note.BeatOffset == beatOffset).ToArray();
        if (notesAtOnset.Any(note => note.Accidental is not null))
        {
            return true;
        }

        foreach (var staffNotes in notesAtOnset.GroupBy(note => note.Staff))
        {
            ScoreNote[] notesOnStaff = staffNotes.ToArray();
            for (int i = 0; i < notesOnStaff.Length; i++)
            {
                for (int j = i + 1; j < notesOnStaff.Length; j++)
                {
                    if (Math.Abs(notesOnStaff[i].Pitch.DiatonicIndex - notesOnStaff[j].Pitch.DiatonicIndex) == 1)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The Y span (in scene coordinates) that barlines and the playback cursor line run through.
    /// Also used by JS to draw the score-playback cursor on its own, off the C# tick loop.
    /// </summary>
    internal static (double Y0, double Y1) GetCursorLineYBounds() =>
        (GrandStaffLayout.SeparateStaffY(GrandStaffLayout.BassLineYs[0], Staff.Bass),
            GrandStaffLayout.SeparateStaffY(GrandStaffLayout.TrebleLineYs[^1], Staff.Treble));

    internal static GrandStaffScene Build(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        int? selectedOctave = null,
        bool showNoteLabels = true) =>
        Build(
            notes,
            currentTime,
            new TimeSignature(4, new NoteValue(4)),
            new Tempo(120),
            selectedOctave,
            showNoteLabels);

    internal static GrandStaffScene Build(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo,
        int? selectedOctave = null,
        bool showNoteLabels = true)
    {
        var lines = CreateStaffLines();
        var glyphs = CreateClefGlyphs();
        AddTimeSignatureGlyphs(glyphs, timeSignature, TimeSignatureX);
        AddLiveMeasureGrid(lines, currentTime, timeSignature, tempo);

        var renderedNotes = new List<GrandStaffNote>(notes.Count);
        var ties = new List<GrandStaffTie>();
        var staffHasVisibleNotes = new HashSet<Staff>();
        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, timeSignature, tempo);
        foreach (var note in notes)
        {
            TimeSpan endTime = note.ReleaseTime ?? currentTime;
            var segments = GrandStaffLayout.GetLiveNoteSegmentLayouts(
                note.Pitch,
                note.StartTime,
                endTime,
                currentTime,
                timeSignature,
                tempo);
            if (segments.Count == 0)
            {
                continue;
            }

            var position = segments[0].Position;
            staffHasVisibleNotes.Add(position.Staff);
            double noteY = GrandStaffLayout.SeparateStaffY(position.Y, position.Staff);
            var stemDirection = GrandStaffLayout.GetStemDirection(position);
            var tieCurveDirection = stemDirection == StemDirection.Up
                ? StemDirection.Down
                : StemDirection.Up;
            bool isPerformedNoteActive = note.ReleaseTime is null;
            double? previousNoteX = null;
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                LiveNoteSegmentLayout segment = segments[segmentIndex];
                int measureIndex = (int)Math.Floor(segment.StartBeat / timeSignature.Numerator);
                float measureStartX = GrandStaffLayout.MapScoreOnsetToX(
                    measureIndex,
                    beatOffset: 0,
                    timeSignature,
                    firstVisibleMeasure);
                float measureEndX = GrandStaffLayout.MapScoreOnsetToX(
                    measureIndex + 1,
                    beatOffset: 0,
                    timeSignature,
                    firstVisibleMeasure);
                double renderedX = Math.Clamp(
                    segment.X,
                    measureStartX + MeasureEdgeNoteClearance,
                    measureEndX - MeasureEdgeNoteClearance);
                TimeSpan segmentDuration = MusicalTime.BeatsToDuration(segment.EndBeat - segment.StartBeat, tempo);
                bool isActiveSegment = isPerformedNoteActive && segmentIndex == segments.Count - 1;
                NoteValue? noteValue = isActiveSegment
                    ? null
                    : GetNearestLiveNoteValue(segmentDuration, timeSignature, tempo);
                bool isFilled = !noteValue.HasValue || noteValue.Value.Denominator >= 4;
                bool hasStem = noteValue.HasValue && noteValue.Value.Denominator != 1;
                int flagCount = noteValue?.Denominator switch
                {
                    8 => 1,
                    16 => 2,
                    _ => 0,
                };
                renderedNotes.Add(new GrandStaffNote(
                    note.Pitch.ToString(),
                    renderedX,
                    noteY,
                    segmentDuration.TotalSeconds,
                    IsActive: isActiveSegment,
                    IsFilled: isFilled,
                    HasStem: hasStem,
                    StemDirection: stemDirection,
                    HasDot: noteValue?.Dots > 0,
                    FlagCount: flagCount,
                    DurationEndX: isActiveSegment ? Math.Max(renderedX, segment.DurationEndX) : null,
                    LabelY: showNoteLabels ? GrandStaffLayout.GetStaffLabelY(position.Staff) : null));
                lines.AddRange(position.LedgerLineYs.Select(
                    y => new GrandStaffLine(
                        renderedX - LedgerLineHalfWidth,
                        GrandStaffLayout.SeparateStaffY(y, position.Staff),
                        renderedX + LedgerLineHalfWidth,
                        GrandStaffLayout.SeparateStaffY(y, position.Staff),
                        GrandStaffLineKind.Ledger)));
                if (position.NeedsAccidental && !segment.HasIncomingTie)
                {
                    glyphs.Add(new GrandStaffGlyph(
                        GetAccidentalGlyph(note.Pitch.Alter),
                        renderedX - AccidentalHorizontalOffset,
                        noteY,
                        GrandStaffGlyphKind.Accidental,
                        AccidentalHeightInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(position.Staff),
                        IsActive: isActiveSegment));
                }

                if (segment.HasIncomingTie)
                {
                    ties.Add(new GrandStaffTie(
                        previousNoteX ?? GrandStaffLayout.ScoreX0,
                        noteY,
                        renderedX,
                        noteY,
                        tieCurveDirection,
                        isPerformedNoteActive));
                }

                if (segmentIndex == segments.Count - 1 && segment.HasOutgoingTie)
                {
                    ties.Add(new GrandStaffTie(
                        renderedX,
                        noteY,
                        GrandStaffLayout.ScoreX1,
                        noteY,
                        tieCurveDirection,
                        isPerformedNoteActive));
                }

                previousNoteX = renderedX;
            }
        }

        DistributeChordTieDirections(ties);

        IReadOnlyList<GrandStaffBand> bands = showNoteLabels
            ? signatureStaves
                .Where(staffHasVisibleNotes.Contains)
                .Select(staff => new GrandStaffBand(
                    StaffX0,
                    GrandStaffLayout.GetStaffLabelY(staff) - GrandStaffLayout.AnnotationBandPadding,
                    StaffX1,
                    GrandStaffLayout.GetStaffLabelY(staff) + GrandStaffLayout.AnnotationBandPadding))
                .ToArray()
            : [];
        var scene = new GrandStaffScene(lines, glyphs, renderedNotes, ShouldClipNotesAtClefs: true)
        {
            Ties = ties,
            Bands = bands,
        };
        return selectedOctave.HasValue
            ? FitToSelectedOctave(scene, selectedOctave.Value)
            : scene;
    }

    private static void DistributeChordTieDirections(List<GrandStaffTie> ties)
    {
        var indexedTies = ties.Select((tie, index) => (Tie: tie, Index: index));
        foreach (var chord in indexedTies.GroupBy(item => (item.Tie.X0, item.Tie.X1)))
        {
            var orderedTies = chord
                .OrderByDescending(item => item.Tie.Y0)
                .ToArray();
            if (orderedTies.Length < 2)
            {
                continue;
            }

            int upTieCount = orderedTies.Length / 2;
            if (orderedTies.Length % 2 != 0
                && orderedTies.Count(item => item.Tie.CurveDirection == StemDirection.Up)
                    > orderedTies.Length / 2)
            {
                upTieCount++;
            }

            for (int tieIndex = 0; tieIndex < orderedTies.Length; tieIndex++)
            {
                var indexedTie = orderedTies[tieIndex];
                StemDirection direction = tieIndex < upTieCount
                    ? StemDirection.Up
                    : StemDirection.Down;
                ties[indexedTie.Index] = indexedTie.Tie with { CurveDirection = direction };
            }
        }
    }

    // These two templates depend on no per-call inputs at all (no score, no notes, no time) —
    // they are the same on every single call, forever. Computing them once and handing out a
    // shallow List copy (mutated further by callers via AddRange/Add) avoids repeating the same
    // LINQ/allocation work on every 16ms tick of the practice and live-keyboard refresh loops.
    // This is a memoized constant, not caller-visible mutable state: nothing ever assigns into
    // it based on a per-call input, and nothing outside this class can observe or mutate it.
    private static readonly IReadOnlyList<GrandStaffLine> baseStaffLines = BuildStaffLines();
    private static readonly IReadOnlyList<GrandStaffGlyph> baseClefGlyphs = BuildClefGlyphs();

    private static List<GrandStaffLine> CreateStaffLines() => new(baseStaffLines);

    private static List<GrandStaffGlyph> CreateClefGlyphs() => new(baseClefGlyphs);

    private static List<GrandStaffLine> BuildStaffLines()
    {
        var lines = GrandStaffLayout.TrebleLineYs
            .Select(y => GrandStaffLayout.SeparateStaffY(y, Staff.Treble))
            .Concat(GrandStaffLayout.BassLineYs.Select(y => GrandStaffLayout.SeparateStaffY(y, Staff.Bass)))
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

    private static List<GrandStaffGlyph> BuildClefGlyphs() =>
    [
        new(
            "𝄞",
            ClefX,
            GrandStaffLayout.SeparateStaffY(GrandStaffLayout.TrebleLineYs[2], Staff.Treble),
            GrandStaffGlyphKind.Clef,
            TrebleClefHeightInStaffSpaces
                * GrandStaffLayout.GrandStaffVerticalScale
                * (GrandStaffLayout.TrebleLineYs[1] - GrandStaffLayout.TrebleLineYs[0])),
        new(
            "𝄢",
            ClefX,
            GrandStaffLayout.SeparateStaffY(
                (GrandStaffLayout.BassLineYs[2] + GrandStaffLayout.BassLineYs[3]) / 2,
                Staff.Bass),
            GrandStaffGlyphKind.Clef,
            BassClefHeightInStaffSpaces
                * GrandStaffLayout.GrandStaffVerticalScale
                * (GrandStaffLayout.BassLineYs[1] - GrandStaffLayout.BassLineYs[0])),
    ];

    private static IReadOnlyList<GrandStaffBeam> BuildBeams(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides,
        ICollection<GrandStaffGlyph> glyphs)
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
                        AddBeam(currentGroup, beams, beamOverrides, glyphs);
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
        IDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides,
        ICollection<GrandStaffGlyph> glyphs)
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

        Staff staff = group[0].Layout.Position.Staff;
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
        double stemOffset = direction == StemDirection.Up ? GrandStaffLayout.StemLength : -GrandStaffLayout.StemLength;
        double x0 = group[0].Layout.X;
        double x1 = group[^1].Layout.X;
        double y0 = GrandStaffLayout.SeparateStaffY(group[0].Layout.Position.Y, staff) + stemOffset;
        double y1 = GrandStaffLayout.SeparateStaffY(group[^1].Layout.Position.Y, staff) + stemOffset;
        beams.Add(new GrandStaffBeam(x0, y0, x1, y1, beamCount, direction));

        foreach (var item in group)
        {
            double progress = x1 == x0 ? 0 : (item.Layout.X - x0) / (x1 - x0);
            double stemEndY = y0 + ((y1 - y0) * progress);
            beamOverrides[item.Note] = (direction, stemEndY, beamCount);
        }

        AddTupletGlyph(group, staff, x0, x1, y0, y1, direction, glyphs);
    }

    /// <summary>
    /// A beamed group whose members share one non-identity tuplet ratio (e.g. a triplet) gets a
    /// single numeral, positioned outside the beam like a fermata sits outside its note. v1 only
    /// labels beamed tuplets — an unbeamed tuplet note still imports and plays back correctly
    /// (see <see cref="MusicXmlScoreReader"/>/<see cref="MusicalTime"/>), it just has no numeral yet.
    /// </summary>
    private static void AddTupletGlyph(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> group,
        Staff staff,
        double x0,
        double x1,
        double y0,
        double y1,
        StemDirection direction,
        ICollection<GrandStaffGlyph> glyphs)
    {
        NoteValue firstValue = group[0].Note.NoteValue;
        if (firstValue.TupletActualNotes == firstValue.TupletNormalNotes ||
            group.Any(item =>
                item.Note.NoteValue.TupletActualNotes != firstValue.TupletActualNotes ||
                item.Note.NoteValue.TupletNormalNotes != firstValue.TupletNormalNotes))
        {
            return;
        }

        double clearance = TupletGlyphClearanceInStaffSpaces * GrandStaffLayout.GetRenderedStaffSpace(staff);
        double beamMidY = (y0 + y1) / 2;
        double glyphY = direction == StemDirection.Up ? beamMidY + clearance : beamMidY - clearance;
        glyphs.Add(new GrandStaffGlyph(
            firstValue.TupletActualNotes.ToString(CultureInfo.InvariantCulture),
            (x0 + x1) / 2,
            glyphY,
            GrandStaffGlyphKind.Tuplet));
    }

    /// <summary>
    /// Per-note adjustments produced by <see cref="ApplyChordLayout"/>: how far a chord member's
    /// notehead (and everything anchored to it — ledger lines, accidental, fermata) is displaced
    /// to the side of the shared stem, and whether this member draws its own stem/flags at all
    /// (only the chord's stem-owning member does; the rest share its stem visually).
    /// </summary>
    private readonly record struct ChordNoteOverride(
        double XOffset,
        bool SuppressStem,
        StemDirection? DirectionOverride = null);

    /// <summary>
    /// Groups simultaneous notes that form one true MusicXML chord (contiguous
    /// <see cref="ScoreNote.IsChordContinuation"/> runs on the same notation staff), displaces
    /// noteheads that are a 2nd apart to the correct side of the shared stem, and gives the
    /// group's stem-owning member a <paramref name="beamOverrides"/> entry spanning the full
    /// chord so it renders as a single stem. Chords with a beamed member are left untouched here
    /// — beam geometry (<see cref="BuildBeams"/>) already assumes one note per time slot, and
    /// reconciling that with a merged chord stem is out of scope for this pass; those notes keep
    /// today's independent per-note stems. Afterward, independent voices that share an onset and
    /// staff without a real chord link between them (different durations, so never eligible for a
    /// shared stem) still get pulled apart via <see cref="ApplyVoiceOverlapDisplacement"/> — they
    /// are just as unreadable fully overlapping as an undisplaced chord would be.
    /// </summary>
    private static void ApplyChordLayout(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IDictionary<ScoreNote, ChordNoteOverride> chordOverrides,
        IDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides)
    {
        IReadOnlyList<IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)>> chordGroups = BuildChordGroups(notes);
        foreach (var group in chordGroups)
        {
            if (group.Count < 2 || group.Any(item => item.Note.BeamState != BeamState.None))
            {
                continue;
            }

            StemDirection direction = ResolveChordStemDirection(group);
            var ordered = (direction == StemDirection.Up
                    ? group.OrderBy(item => item.Layout.Position.DiatonicPosition.DiatonicOffset)
                    : group.OrderByDescending(item => item.Layout.Position.DiatonicPosition.DiatonicOffset))
                .ToArray();

            var displaced = new bool[ordered.Length];
            for (int index = 1; index < ordered.Length; index++)
            {
                int step = Math.Abs(
                    ordered[index].Layout.Position.DiatonicPosition.DiatonicOffset -
                    ordered[index - 1].Layout.Position.DiatonicPosition.DiatonicOffset);
                displaced[index] = step == 1 && !displaced[index - 1];
            }

            double displacementSign = direction == StemDirection.Up ? 1 : -1;
            double maxDisplacement = ClampDisplacementToNeighbor(
                notes,
                ordered[0].Note,
                ordered[0].Layout.Position.Staff,
                ordered[0].Layout.X,
                displacementSign,
                ChordNoteheadDisplacement);

            for (int index = 0; index < ordered.Length; index++)
            {
                chordOverrides[ordered[index].Note] = new ChordNoteOverride(
                    displaced[index] ? displacementSign * maxDisplacement : 0,
                    SuppressStem: index > 0);
            }

            Staff staff = ordered[0].Layout.Position.Staff;
            double stemOffset = direction == StemDirection.Up ? GrandStaffLayout.StemLength : -GrandStaffLayout.StemLength;
            double stemEndY = GrandStaffLayout.SeparateStaffY(ordered[^1].Layout.Position.Y, staff) + stemOffset;
            beamOverrides[ordered[0].Note] = (direction, stemEndY, BeamCount: 0);
        }

        ApplyVoiceOverlapDisplacement(notes, chordGroups, chordOverrides);
    }

    /// <summary>
    /// Clamps a requested chord-notehead displacement so it can never cross past the nearest
    /// same-staff neighbor at a different onset in this measure. Cosmetic spacing
    /// (<see cref="BuildNotationSpacingAnchors"/>) tries to leave room for this, but its
    /// reservation is capped for busy measures (see <see cref="MaxMeasureSpacingBudgetFraction"/>)
    /// and must never be the only thing preventing a displaced notehead from visually landing
    /// behind — or on top of — its neighbor.
    /// </summary>
    private static double ClampDisplacementToNeighbor(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        ScoreNote referenceNote,
        Staff staff,
        double referenceX,
        double displacementSign,
        double requestedDisplacement)
    {
        if (displacementSign < 0)
        {
            double? previousNeighborX = notes
                .Where(item =>
                    item.Note.MeasureIndex == referenceNote.MeasureIndex &&
                    item.Layout.Position.Staff == staff &&
                    item.Note.BeatOffset < referenceNote.BeatOffset)
                .Select(item => (double?)item.Layout.X)
                .Max();
            return previousNeighborX is { } previousX
                ? Math.Clamp(referenceX - previousX - MinimumOnsetClearance, 0, requestedDisplacement)
                : requestedDisplacement;
        }

        double? nextNeighborX = notes
            .Where(item =>
                item.Note.MeasureIndex == referenceNote.MeasureIndex &&
                item.Layout.Position.Staff == staff &&
                item.Note.BeatOffset > referenceNote.BeatOffset)
            .Select(item => (double?)item.Layout.X)
            .Min();
        return nextNeighborX is { } nextX
            ? Math.Clamp(nextX - referenceX - MinimumOnsetClearance, 0, requestedDisplacement)
            : requestedDisplacement;
    }

    /// <summary>
    /// Displaces independent voices — chord groups from <see cref="BuildChordGroups"/> that share
    /// an onset and staff but were never linked by a real <c>&lt;chord/&gt;</c>, so each keeps its
    /// own stem — when they are pitch-adjacent enough to otherwise render on top of each other.
    /// Two notes at the same onset with different durations can never be one real chord (a chord
    /// shares a single stem and duration across all its members), but they are exactly as
    /// unreadable fully overlapping as an undisplaced chord would be. Reuses the same "flip sides
    /// on each adjacent 2nd" rule as real chords, and the same neighbor clamp, but only ever
    /// shifts whole voices as rigid units — a voice that already got its own internal chord
    /// displacement in the caller's earlier loop keeps that shape, just moved as one piece.
    /// </summary>
    private static void ApplyVoiceOverlapDisplacement(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes,
        IReadOnlyList<IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)>> chordGroups,
        IDictionary<ScoreNote, ChordNoteOverride> chordOverrides)
    {
        var buckets = chordGroups.GroupBy(group =>
            (group[0].Note.MeasureIndex, group[0].Note.BeatOffset, group[0].Layout.Position.Staff));
        foreach (var bucket in buckets)
        {
            var voices = bucket.ToArray();
            if (voices.Length < 2)
            {
                continue;
            }

            var ordered = voices
                .OrderBy(voice => voice.Average(item => item.Layout.Position.DiatonicPosition.DiatonicOffset))
                .ToArray();

            var displaced = new bool[ordered.Length];
            for (int index = 1; index < ordered.Length; index++)
            {
                int forwardGap = Math.Abs(
                    ordered[index].Min(item => item.Layout.Position.DiatonicPosition.DiatonicOffset) -
                    ordered[index - 1].Max(item => item.Layout.Position.DiatonicPosition.DiatonicOffset));
                int backwardGap = Math.Abs(
                    ordered[index].Max(item => item.Layout.Position.DiatonicPosition.DiatonicOffset) -
                    ordered[index - 1].Min(item => item.Layout.Position.DiatonicPosition.DiatonicOffset));
                displaced[index] = Math.Min(forwardGap, backwardGap) <= 1 && !displaced[index - 1];
            }

            if (!displaced.Any(needsDisplacement => needsDisplacement))
            {
                // No two voices in this bucket are actually close enough to collide (e.g. a 5th
                // apart) — leave each note's independently-resolved stem direction alone. The
                // top-voice-up/bottom-voice-down convention below is only for telling apart
                // voices that would otherwise visually overlap.
                continue;
            }

            // Independent voices sharing a staff use a different stem-direction convention than a
            // single line of notes once they're close enough to need disambiguating: the top
            // voice always stems up and the bottom voice always stems down, regardless of where
            // either one actually sits relative to the middle line, so a reader can keep each
            // voice visually distinct. (Any voices strictly between the top and bottom keep
            // whatever direction their own pitch already resolved to — three or more real,
            // independent voices sharing one exact onset on one staff is rare enough not to need
            // a general rule here.)
            var directionOverrides = new StemDirection?[ordered.Length];
            directionOverrides[0] = StemDirection.Down;
            directionOverrides[^1] = StemDirection.Up;

            for (int index = 0; index < ordered.Length; index++)
            {
                double shift = 0;
                if (displaced[index])
                {
                    StemDirection ownDirection = directionOverrides[index] ?? ResolveChordStemDirection(ordered[index]);
                    double displacementSign = ownDirection == StemDirection.Up ? 1 : -1;
                    var anchor = ordered[index][0];
                    shift = ClampDisplacementToNeighbor(
                        notes,
                        anchor.Note,
                        anchor.Layout.Position.Staff,
                        anchor.Layout.X,
                        displacementSign,
                        ChordNoteheadDisplacement) * displacementSign;
                }

                if (shift == 0 && directionOverrides[index] is null)
                {
                    continue;
                }

                foreach (var (note, _) in ordered[index])
                {
                    ChordNoteOverride existing = chordOverrides.TryGetValue(note, out var current)
                        ? current
                        : new ChordNoteOverride(0, SuppressStem: false);
                    chordOverrides[note] = existing with
                    {
                        XOffset = existing.XOffset + shift,
                        DirectionOverride = directionOverrides[index] ?? existing.DirectionOverride,
                    };
                }
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)>> BuildChordGroups(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> notes)
    {
        var groups = new List<List<(ScoreNote Note, ScoreNoteLayout Layout)>>();
        foreach (var item in notes)
        {
            List<(ScoreNote Note, ScoreNoteLayout Layout)>? currentGroup = groups.Count > 0 ? groups[^1] : null;
            bool continuesChord = item.Note.IsChordContinuation &&
                currentGroup is not null &&
                currentGroup[^1].Note.MeasureIndex == item.Note.MeasureIndex &&
                currentGroup[^1].Layout.Position.Staff == item.Layout.Position.Staff;
            if (continuesChord)
            {
                currentGroup!.Add(item);
            }
            else
            {
                groups.Add([item]);
            }
        }

        return groups;
    }

    private static StemDirection ResolveChordStemDirection(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> group)
    {
        Staff staff = group[0].Layout.Position.Staff;
        var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
        double averageY = group.Average(item => item.Layout.Position.Y);
        var automaticDirection = averageY < staffLines[2] ? StemDirection.Up : StemDirection.Down;
        var explicitDirections = group
            .Select(item => item.Note.StemDirection)
            .OfType<ScoreStemDirection>()
            .Distinct()
            .ToArray();
        return explicitDirections.Length == 1
            ? group.First(item => item.Note.StemDirection == explicitDirections[0]).Layout.StemDirection
            : automaticDirection;
    }

    private static void AddScoreSignatures(ICollection<GrandStaffGlyph> glyphs, Score score)
    {
        AddKeySignatureGlyphs(glyphs, score.KeyFifths);
        int accidentalCount = Math.Abs(score.KeyFifths);
        double timeSignatureX = accidentalCount == 0
            ? TimeSignatureX - OpeningBarlineLead
            : KeySignatureX0
                + ((accidentalCount - 1) * KeySignatureXSpacing)
                + TimeSignatureGapAfterKeySignature;
        AddTimeSignatureGlyphs(glyphs, score.TimeSignature, timeSignatureX);
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
                    GrandStaffLayout.SeparateStaffY(
                        bottomLineY + (offsets[index] * GrandStaffLayout.DiatonicStep),
                        staff),
                    GrandStaffGlyphKind.KeySignature,
                    KeySignatureHeightInStaffSpaces
                        * GrandStaffLayout.GrandStaffVerticalScale
                        * (GrandStaffLayout.TrebleLineYs[1] - GrandStaffLayout.TrebleLineYs[0])));
            }
        }
    }

    private static void AddTimeSignatureGlyphs(
        ICollection<GrandStaffGlyph> glyphs,
        TimeSignature timeSignature,
        double x)
    {
        foreach (Staff staff in signatureStaves)
        {
            var staffLines = staff == Staff.Treble ? GrandStaffLayout.TrebleLineYs : GrandStaffLayout.BassLineYs;
            glyphs.Add(new GrandStaffGlyph(
                timeSignature.Numerator.ToString(CultureInfo.InvariantCulture),
                x,
                GrandStaffLayout.SeparateStaffY(staffLines[3], staff),
                GrandStaffGlyphKind.TimeSignature,
                TimeSignatureHeightInStaffSpaces * GrandStaffLayout.GrandStaffVerticalScale * (staffLines[1] - staffLines[0])));
            glyphs.Add(new GrandStaffGlyph(
                timeSignature.BeatNoteValue.Denominator.ToString(CultureInfo.InvariantCulture),
                x,
                GrandStaffLayout.SeparateStaffY(staffLines[1], staff),
                GrandStaffGlyphKind.TimeSignature,
                TimeSignatureHeightInStaffSpaces * GrandStaffLayout.GrandStaffVerticalScale * (staffLines[1] - staffLines[0])));
        }
    }

    internal static GrandStaffScene FitToSelectedOctave(GrandStaffScene scene, int selectedOctave)
    {
        var yValues = new List<double>();
        foreach (var line in scene.Lines)
        {
            yValues.Add(line.Y0);
            yValues.Add(line.Y1);
        }

        yValues.AddRange(scene.Glyphs.Select(glyph => glyph.Y));
        yValues.AddRange(scene.Notes.Select(note => note.Y));
        yValues.AddRange(scene.Notes.Select(note => note.LabelY).OfType<double>());
        foreach (var band in scene.Bands)
        {
            yValues.Add(band.Y0);
            yValues.Add(band.Y1);
        }

        foreach (var tie in scene.Ties)
        {
            yValues.Add(tie.Y0);
            yValues.Add(tie.Y1);
        }

        foreach (var slur in scene.Slurs)
        {
            yValues.Add(slur.Y0);
            yValues.Add(slur.Y1);
        }

        foreach (var arpeggioMark in scene.ArpeggioMarks)
        {
            yValues.Add(arpeggioMark.Y0);
            yValues.Add(arpeggioMark.Y1);
        }

        for (int octave = selectedOctave; octave <= selectedOctave + 1; octave++)
        {
            var position = GrandStaffLayout.GetLivePosition(new Pitch(NoteLetter.C, 0, octave));
            yValues.Add(GrandStaffLayout.SeparateStaffY(position.Y, position.Staff));
            yValues.AddRange(position.LedgerLineYs.Select(y => GrandStaffLayout.SeparateStaffY(y, position.Staff)));
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
                LabelY = note.LabelY.HasValue ? MapY(note.LabelY.Value) : null,
            }).ToArray(),
            scene.ShouldClipNotesAtClefs)
        {
            Beams = scene.Beams.Select(beam => beam with { Y0 = MapY(beam.Y0), Y1 = MapY(beam.Y1) }).ToArray(),
            Ties = scene.Ties.Select(tie => tie with { Y0 = MapY(tie.Y0), Y1 = MapY(tie.Y1) }).ToArray(),
            Bands = scene.Bands.Select(band => band with { Y0 = MapY(band.Y0), Y1 = MapY(band.Y1) }).ToArray(),
            Slurs = scene.Slurs.Select(slur => slur with { Y0 = MapY(slur.Y0), Y1 = MapY(slur.Y1) }).ToArray(),
            ArpeggioMarks = scene.ArpeggioMarks
                .Select(mark => mark with { Y0 = MapY(mark.Y0), Y1 = MapY(mark.Y1) })
                .ToArray(),
        };
    }

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

    // "●" (not the visually lighter "•") and ">" both measure a normal, letter-like bounding
    // box height in canvas.js's dynamic glyph-sizing math, unlike "–" (an en dash, tenuto's mark)
    // — see the minimumMeasuredHeight comment in canvas.js's drawGlyph for why that still needs a
    // defensive clamp rather than a "just pick a well-behaved character" fix alone.
    private static string GetArticulationGlyph(ScoreArticulation articulation) => articulation switch
    {
        ScoreArticulation.Staccato => "●",
        ScoreArticulation.Tenuto => "–",
        ScoreArticulation.Accent => ">",
        ScoreArticulation.Staccatissimo => "▾",
        _ => throw new ArgumentOutOfRangeException(nameof(articulation), articulation, message: null),
    };

    private static string GetOrnamentGlyph(ScoreOrnament ornament) => ornament switch
    {
        ScoreOrnament.TrillMark => "tr",
        _ => throw new ArgumentOutOfRangeException(nameof(ornament), ornament, message: null),
    };

    private static string GetAccidentalGlyph(ScoreAccidental accidental) => accidental switch
    {
        ScoreAccidental.Natural => "♮",
        ScoreAccidental.Sharp => "♯",
        ScoreAccidental.Flat => "♭",
        ScoreAccidental.DoubleSharp => "𝄪",
        ScoreAccidental.SharpSharp => "♯♯",
        ScoreAccidental.DoubleFlat => "𝄫",
        _ => throw new ArgumentOutOfRangeException(nameof(accidental), accidental, message: null),
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
