using PianoMapper.Music;

namespace PianoMapper.Rendering;

public static class GrandStaffLayout
{
    private const double BeatComparisonTolerance = 0.000000001;

    public const float DiatonicStep = 0.045f;
    public const float MiddleCY = (PianoRollLayout.BandY0 + PianoRollLayout.BandY1) / 2f;
    public const float ScoreX0 = -0.56f;
    public const float ScoreX1 = 0.96f;
    public const int DefaultVisibleMeasureCount = 5;

    // Grand-staff annotation/notation geometry policy. These match the Canvas 2D notehead and
    // beam/stem proportions in wwwroot/js/canvas.js.
    public const double GrandStaffVerticalScale = 0.82;
    public const double StemLength = DiatonicStep * GrandStaffVerticalScale * 6;
    public const double StaffSeparationOffset = DiatonicStep * 6;
    public const double NoteLabelOffsetBelowStaff = DiatonicStep * 5;
    // Five rendered diatonic steps keep 16 px labels separate at the minimum 15 rem score-canvas height.
    public const double LabelRowSeparation = DiatonicStep * GrandStaffVerticalScale * 5;
    public const double FingeringRowSeparation = DiatonicStep * GrandStaffVerticalScale * 8;
    public const double AnnotationBandPadding = DiatonicStep * GrandStaffVerticalScale;
    public const double AnnotationNotationGap = DiatonicStep * GrandStaffVerticalScale;
    public const double NoteHeadHalfHeightInStaffSpaces = 0.4;
    public const double NotationStrokePaddingInStaffSpaces = 0.25;
    public const double FermataHeightInStaffSpaces = 1.5;
    public const double FermataClearanceInStaffSpaces = 0.75;
    public const double PointGlyphClearanceInStaffSpaces = 0.75;

    private const int TrebleBottomDiatonicIndex = 30; // E4
    private const int BassBottomDiatonicIndex = 18; // G2
    private const float TrebleBottomY = MiddleCY + (2 * DiatonicStep);
    private const float BassBottomY = MiddleCY - (10 * DiatonicStep);

    public static IReadOnlyList<float> TrebleLineYs { get; } = BuildLineYs(TrebleBottomY);

    public static IReadOnlyList<float> BassLineYs { get; } = BuildLineYs(BassBottomY);

    public static StaffPlacement GetLivePosition(Pitch pitch) =>
        GetPosition(pitch, pitch.MidiNumber >= 60 ? Staff.Treble : Staff.Bass);

    public static float MapTimeToX(TimeSpan time, TimeSpan now) =>
        PianoRollLayout.MapTimeToX(time.TotalSeconds, now.TotalSeconds);

    public static float? GetLiveNoteX(TimeSpan startTime, TimeSpan endTime, TimeSpan now)
    {
        var windowStart = now - TimeSpan.FromSeconds(PianoRollLayout.RollingWindowSeconds);
        return endTime < windowStart ? null : MapTimeToX(startTime, now);
    }

    public static float MapScoreOnsetToX(
        int measureIndex,
        double beatOffset,
        TimeSignature timeSignature,
        int firstVisibleMeasure,
        int visibleMeasureCount = DefaultVisibleMeasureCount)
    {
        double relativeMeasure = measureIndex - firstVisibleMeasure + (beatOffset / timeSignature.Numerator);
        return ScoreX0 + (float)(relativeMeasure / visibleMeasureCount * (ScoreX1 - ScoreX0));
    }

    public static int GetLiveFirstVisibleMeasure(
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        double currentBeat = MusicalTime.DurationToBeats(currentTime, tempo);
        int currentMeasure = Math.Max(0, (int)Math.Floor(currentBeat / timeSignature.Numerator));
        return currentMeasure / DefaultVisibleMeasureCount * DefaultVisibleMeasureCount;
    }

    public static LiveNoteLayout? GetLiveNoteLayout(
        Pitch pitch,
        TimeSpan startTime,
        TimeSpan endTime,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        var segments = GetLiveNoteSegmentLayouts(
            pitch,
            startTime,
            endTime,
            currentTime,
            timeSignature,
            tempo);
        if (segments.Count == 0)
        {
            return null;
        }

        return new LiveNoteLayout(
            segments[0].X,
            segments[^1].DurationEndX,
            segments[0].Position);
    }

    public static IReadOnlyList<LiveNoteSegmentLayout> GetLiveNoteSegmentLayouts(
        Pitch pitch,
        TimeSpan startTime,
        TimeSpan endTime,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        int firstVisibleMeasure = GetLiveFirstVisibleMeasure(currentTime, timeSignature, tempo);
        double windowStartBeat = firstVisibleMeasure * timeSignature.Numerator;
        double windowEndBeat = (firstVisibleMeasure + DefaultVisibleMeasureCount) * timeSignature.Numerator;
        double startBeat = MusicalTime.DurationToBeats(startTime, tempo);
        double endBeat = Math.Max(startBeat, MusicalTime.DurationToBeats(endTime, tempo));
        double visibleStartBeat = Math.Max(startBeat, windowStartBeat);
        double visibleEndBeat = Math.Min(endBeat, windowEndBeat);
        if (visibleEndBeat - visibleStartBeat <= BeatComparisonTolerance)
        {
            return [];
        }

        var position = GetLivePosition(pitch);
        var segments = new List<LiveNoteSegmentLayout>();
        double segmentStartBeat = visibleStartBeat;
        while (visibleEndBeat - segmentStartBeat > BeatComparisonTolerance)
        {
            int measureIndex = (int)Math.Floor(segmentStartBeat / timeSignature.Numerator);
            double measureEndBeat = (measureIndex + 1) * timeSignature.Numerator;
            double segmentEndBeat = Math.Min(visibleEndBeat, measureEndBeat);
            segments.Add(new LiveNoteSegmentLayout(
                MapAbsoluteBeatToScoreX(segmentStartBeat, timeSignature, firstVisibleMeasure),
                MapAbsoluteBeatToScoreX(segmentEndBeat, timeSignature, firstVisibleMeasure),
                segmentStartBeat,
                segmentEndBeat,
                position,
                HasIncomingTie: startBeat < segmentStartBeat - BeatComparisonTolerance,
                HasOutgoingTie: endBeat > segmentEndBeat + BeatComparisonTolerance));
            segmentStartBeat = segmentEndBeat;
        }

        return segments;
    }

    public static IReadOnlyList<GridLine> GetLiveMeasureGridLines(
        TimeSpan now,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        int firstVisibleMeasure = GetLiveFirstVisibleMeasure(now, timeSignature, tempo);
        var lines = new List<GridLine>();

        foreach (float barlineX in GetScoreBarlineXs(firstVisibleMeasure, firstVisibleMeasure + DefaultVisibleMeasureCount))
        {
            lines.Add(new GridLine(barlineX, GridLineKind.Barline));
        }

        int visibleBeatCount = DefaultVisibleMeasureCount * timeSignature.Numerator;
        for (int beatIndex = 1; beatIndex < visibleBeatCount; beatIndex++)
        {
            if (beatIndex % timeSignature.Numerator == 0)
            {
                continue;
            }

            double absoluteBeat = (firstVisibleMeasure * timeSignature.Numerator) + beatIndex;
            float beatX = MapAbsoluteBeatToScoreX(absoluteBeat, timeSignature, firstVisibleMeasure);
            lines.Add(new GridLine(beatX, GridLineKind.Beat));
        }

        double currentBeat = MusicalTime.DurationToBeats(now, tempo);
        float cursorX = MapAbsoluteBeatToScoreX(currentBeat, timeSignature, firstVisibleMeasure);
        if (cursorX >= ScoreX0 && cursorX <= ScoreX1)
        {
            lines.Add(new GridLine(cursorX, GridLineKind.Cursor));
        }

        return lines;
    }

    public static ScoreNoteLayout? GetScoreNoteLayout(
        ScoreNote note,
        TimeSignature timeSignature,
        int firstVisibleMeasure,
        int visibleMeasureCount = DefaultVisibleMeasureCount)
    {
        if (note.MeasureIndex < firstVisibleMeasure || note.MeasureIndex >= firstVisibleMeasure + visibleMeasureCount)
        {
            return null;
        }

        var position = GetPosition(note.Pitch, note.Staff);
        var stemDirection = ResolveStemDirection(note.StemDirection, position);
        return new ScoreNoteLayout(
            MapScoreOnsetToX(note.MeasureIndex, note.BeatOffset, timeSignature, firstVisibleMeasure, visibleMeasureCount),
            position,
            note.NoteValue.Denominator <= 2 ? NoteHeadStyle.Hollow : NoteHeadStyle.Filled,
            stemDirection,
            HasStem: note.NoteValue.Denominator != 1,
            HasDot: note.NoteValue.Dots > 0,
            FlagCount: note.NoteValue.Denominator switch
            {
                8 => 1,
                16 => 2,
                _ => 0,
            });
    }

    public static IReadOnlyList<float> GetScoreBarlineXs(
        int firstVisibleMeasure,
        int measureCount,
        int visibleMeasureCount = DefaultVisibleMeasureCount)
    {
        int visibleMeasures = Math.Min(visibleMeasureCount, Math.Max(0, measureCount - firstVisibleMeasure));
        return Enumerable.Range(0, visibleMeasures + 1)
            .Select(boundary => boundary == visibleMeasureCount
                ? ScoreX1
                : ScoreX0 + (boundary * (ScoreX1 - ScoreX0) / visibleMeasureCount))
            .ToArray();
    }

    public static float MapAbsoluteBeatToScoreX(
        double absoluteBeat,
        TimeSignature timeSignature,
        int firstVisibleMeasure)
    {
        int measureIndex = (int)Math.Floor(absoluteBeat / timeSignature.Numerator);
        double beatOffset = absoluteBeat - (measureIndex * timeSignature.Numerator);
        return MapScoreOnsetToX(measureIndex, beatOffset, timeSignature, firstVisibleMeasure);
    }

    public static StaffPosition GetStaffPosition(Pitch pitch, Staff staff)
    {
        int bottomIndex = staff == Staff.Treble ? TrebleBottomDiatonicIndex : BassBottomDiatonicIndex;
        int diatonicOffset = pitch.DiatonicIndex - bottomIndex;
        return new StaffPosition(staff, diatonicOffset, BuildLedgerLineOffsets(diatonicOffset));
    }

    public static StaffPlacement GetPosition(Pitch pitch, Staff staff)
    {
        float bottomY = staff == Staff.Treble ? TrebleBottomY : BassBottomY;
        var position = GetStaffPosition(pitch, staff);
        float y = bottomY + (position.DiatonicOffset * DiatonicStep);
        var ledgerLineYs = position.LedgerLineOffsets
            .Select(offset => bottomY + (offset * DiatonicStep))
            .ToArray();
        return new StaffPlacement(position, y, ledgerLineYs, pitch.Alter != 0);
    }

    public static StemDirection GetStemDirection(StaffPlacement position)
    {
        var staffLines = position.Staff == Staff.Treble ? TrebleLineYs : BassLineYs;
        return position.Y < staffLines[2] ? StemDirection.Up : StemDirection.Down;
    }

    private static StemDirection ResolveStemDirection(
        ScoreStemDirection? scoreDirection,
        StaffPlacement position) =>
        scoreDirection switch
        {
            ScoreStemDirection.Up => StemDirection.Up,
            ScoreStemDirection.Down => StemDirection.Down,
            null => GetStemDirection(position),
            _ => throw new ArgumentOutOfRangeException(nameof(scoreDirection), scoreDirection, message: null),
        };

    private static IReadOnlyList<float> BuildLineYs(float bottomY) =>
        Enumerable.Range(0, 5)
            .Select(line => bottomY + (line * 2 * DiatonicStep))
            .ToArray();

    private static IReadOnlyList<int> BuildLedgerLineOffsets(int pitchOffset)
    {
        const int topLineOffset = 8;
        var ledgerLineOffsets = new List<int>();

        for (int ledgerOffset = -2; ledgerOffset >= pitchOffset; ledgerOffset -= 2)
        {
            ledgerLineOffsets.Add(ledgerOffset);
        }

        for (int ledgerOffset = topLineOffset + 2; ledgerOffset <= pitchOffset; ledgerOffset += 2)
        {
            ledgerLineOffsets.Add(ledgerOffset);
        }

        return ledgerLineOffsets;
    }

    /// <summary>
    /// Offsets a single-staff Y (from <see cref="GetPosition"/>, where treble and bass overlap
    /// around <see cref="MiddleCY"/>) into grand-staff scene space, where the two staves are
    /// pulled apart by <see cref="StaffSeparationOffset"/> and the whole system is scaled by
    /// <see cref="GrandStaffVerticalScale"/>.
    /// </summary>
    public static double SeparateStaffY(double y, Staff staff) =>
        (y + (staff == Staff.Treble ? StaffSeparationOffset : -StaffSeparationOffset))
        * GrandStaffVerticalScale;

    public static double GetRenderedStaffSpace(Staff staff)
    {
        IReadOnlyList<float> staffLines = staff == Staff.Treble ? TrebleLineYs : BassLineYs;
        return GrandStaffVerticalScale * (staffLines[1] - staffLines[0]);
    }

    public static double GetStaffBottomLineY(Staff staff) =>
        staff == Staff.Treble ? TrebleLineYs[0] : BassLineYs[0];

    public static double GetStaffLabelY(Staff staff) =>
        SeparateStaffY(GetStaffBottomLineY(staff) - NoteLabelOffsetBelowStaff, staff);

    public static double GetFermataY(
        ScoreFermata fermata,
        double noteY,
        ScoreNoteLayout layout,
        double? beamStemEndY) =>
        GetOuterGlyphY(
            fermata == ScoreFermata.Upright,
            noteY,
            layout,
            beamStemEndY,
            FermataClearanceInStaffSpaces);

    /// <summary>
    /// Y for a point-glyph notation (articulation, ornament, accidental-mark) that has no
    /// MusicXML orientation of its own (unlike <see cref="ScoreFermata"/>'s explicit
    /// upright/inverted type) — always placed above the note, outside its stem/beam, using the
    /// same outer-clearance placement as <see cref="GetFermataY"/>.
    /// </summary>
    public static double GetPointGlyphY(
        double noteY,
        ScoreNoteLayout layout,
        double? beamStemEndY) =>
        GetOuterGlyphY(isUpright: true, noteY, layout, beamStemEndY, PointGlyphClearanceInStaffSpaces);

    private static double GetOuterGlyphY(
        bool isUpright,
        double noteY,
        ScoreNoteLayout layout,
        double? beamStemEndY,
        double clearanceInStaffSpaces)
    {
        IReadOnlyList<float> staffLines = layout.Position.Staff == Staff.Treble ? TrebleLineYs : BassLineYs;
        double staffEdgeY = SeparateStaffY(isUpright ? staffLines[^1] : staffLines[0], layout.Position.Staff);
        double outerY = isUpright
            ? Math.Max(noteY, staffEdgeY)
            : Math.Min(noteY, staffEdgeY);
        if (beamStemEndY.HasValue)
        {
            outerY = isUpright
                ? Math.Max(outerY, beamStemEndY.Value)
                : Math.Min(outerY, beamStemEndY.Value);
        }
        else if (layout.HasStem)
        {
            double stemEndY = noteY + (layout.StemDirection == StemDirection.Up ? StemLength : -StemLength);
            outerY = isUpright
                ? Math.Max(outerY, stemEndY)
                : Math.Min(outerY, stemEndY);
        }

        double direction = isUpright ? 1 : -1;
        return outerY + (direction * clearanceInStaffSpaces * GetRenderedStaffSpace(layout.Position.Staff));
    }

    public static double GetNotationBottomY(
        Staff staff,
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> visibleNotes,
        IReadOnlyDictionary<ScoreNote, (StemDirection Direction, double StemEndY, int BeamCount)> beamOverrides)
    {
        IReadOnlyList<float> staffLines = staff == Staff.Treble ? TrebleLineYs : BassLineYs;
        double minimumY = SeparateStaffY(staffLines[0], staff);
        double renderedStaffSpace = GrandStaffVerticalScale * (staffLines[1] - staffLines[0]);
        double noteHeadHalfHeight = renderedStaffSpace * NoteHeadHalfHeightInStaffSpaces;
        double strokePadding = renderedStaffSpace * NotationStrokePaddingInStaffSpaces;

        foreach (var (note, layout) in visibleNotes.Where(item => item.Layout.Position.Staff == staff))
        {
            double noteY = SeparateStaffY(layout.Position.Y, staff);
            minimumY = Math.Min(minimumY, noteY - noteHeadHalfHeight);
            foreach (float ledgerLineY in layout.Position.LedgerLineYs)
            {
                double renderedLedgerY = SeparateStaffY(ledgerLineY, staff);
                minimumY = Math.Min(minimumY, renderedLedgerY);
            }

            double? stemEndY = null;
            if (layout.HasStem)
            {
                if (beamOverrides.TryGetValue(note, out var beamOverride))
                {
                    stemEndY = beamOverride.StemEndY;
                }
                else
                {
                    stemEndY = noteY + (layout.StemDirection == StemDirection.Up ? StemLength : -StemLength);
                }

                minimumY = Math.Min(minimumY, stemEndY.Value - strokePadding);
            }

            if (note.Fermata == ScoreFermata.Inverted)
            {
                double fermataY = GetFermataY(note.Fermata.Value, noteY, layout, stemEndY);
                minimumY = Math.Min(
                    minimumY,
                    fermataY - (FermataHeightInStaffSpaces * renderedStaffSpace / 2) - strokePadding);
            }
        }

        return minimumY;
    }

    public static int[] GetLabelRowIndexes(
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> visibleNotes)
    {
        var rowIndexes = new int[visibleNotes.Count];
        var simultaneousNotes = visibleNotes
            .Select((item, index) => (item.Note, item.Layout, Index: index))
            .GroupBy(item => (
                item.Note.MeasureIndex,
                item.Note.BeatOffset,
                item.Layout.Position.Staff));

        foreach (var notes in simultaneousNotes)
        {
            int rowIndex = 0;
            foreach (var note in notes
                         .OrderByDescending(item => item.Note.Pitch.MidiNumber)
                         .ThenBy(item => item.Index))
            {
                rowIndexes[note.Index] = rowIndex;
                rowIndex++;
            }
        }

        return rowIndexes;
    }

    public static int GetLabelRowCount(
        Staff staff,
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> visibleNotes,
        IReadOnlyList<int> labelRowIndexes)
    {
        int rowCount = 0;
        for (int noteIndex = 0; noteIndex < visibleNotes.Count; noteIndex++)
        {
            if (visibleNotes[noteIndex].Layout.Position.Staff == staff)
            {
                rowCount = Math.Max(rowCount, labelRowIndexes[noteIndex] + 1);
            }
        }

        return rowCount;
    }

    public static AnnotationRows GetAnnotationRows(
        Staff staff,
        IReadOnlyList<(ScoreNote Note, ScoreNoteLayout Layout)> visibleNotes,
        double notationBottomY,
        int labelRowCount,
        bool showFingerings)
    {
        bool hasLabel = labelRowCount > 0;
        bool hasFingering = showFingerings
            && visibleNotes.Any(item => item.Layout.Position.Staff == staff && item.Note.Fingering is not null);
        if (!hasLabel && !hasFingering)
        {
            return default;
        }

        double firstRowY = notationBottomY - AnnotationNotationGap - AnnotationBandPadding;
        double? labelY = hasLabel ? firstRowY : null;
        double? lowestLabelY = hasLabel
            ? firstRowY - ((labelRowCount - 1) * LabelRowSeparation)
            : null;
        double? fingeringY = hasFingering
            ? (lowestLabelY ?? firstRowY) - (hasLabel ? FingeringRowSeparation : 0)
            : null;
        double bandY0 = Math.Min(lowestLabelY ?? double.MaxValue, fingeringY ?? double.MaxValue)
            - AnnotationBandPadding;
        double bandY1 = Math.Max(labelY ?? double.MinValue, fingeringY ?? double.MinValue)
            + AnnotationBandPadding;
        return new AnnotationRows(labelY, fingeringY, bandY0, bandY1);
    }
}

/// <summary>
/// Where a staff's note labels and fingering annotations sit, and the vertical band that
/// encloses them, in grand-staff scene space. Empty (all-null) when the staff has neither.
/// </summary>
public readonly record struct AnnotationRows(
    double? LabelY,
    double? FingeringY,
    double? BandY0,
    double? BandY1);
