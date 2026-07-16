using PianoMapper.Music;

namespace PianoMapper.Rendering;

public static class GrandStaffLayout
{
    public const float DiatonicStep = 0.045f;
    public const float MiddleCY = (PianoRollLayout.BandY0 + PianoRollLayout.BandY1) / 2f;
    public const float ScoreX0 = -0.78f;
    public const float ScoreX1 = 0.96f;
    public const int VisibleMeasureCount = 4;

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
        int firstVisibleMeasure)
    {
        double relativeMeasure = measureIndex - firstVisibleMeasure + (beatOffset / timeSignature.Numerator);
        return ScoreX0 + (float)(relativeMeasure / VisibleMeasureCount * (ScoreX1 - ScoreX0));
    }

    public static ScoreNoteLayout? GetScoreNoteLayout(
        ScoreNote note,
        TimeSignature timeSignature,
        int firstVisibleMeasure)
    {
        if (note.MeasureIndex < firstVisibleMeasure || note.MeasureIndex >= firstVisibleMeasure + VisibleMeasureCount)
        {
            return null;
        }

        var position = GetPosition(note.Pitch, note.Staff);
        var staffLines = note.Staff == Staff.Treble ? TrebleLineYs : BassLineYs;
        var stemDirection = position.Y < staffLines[2] ? StemDirection.Up : StemDirection.Down;
        return new ScoreNoteLayout(
            MapScoreOnsetToX(note.MeasureIndex, note.BeatOffset, timeSignature, firstVisibleMeasure),
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

    public static IReadOnlyList<float> GetScoreBarlineXs(int firstVisibleMeasure, int measureCount)
    {
        int visibleMeasures = Math.Min(VisibleMeasureCount, Math.Max(0, measureCount - firstVisibleMeasure));
        return Enumerable.Range(0, visibleMeasures + 1)
            .Select(boundary => boundary == VisibleMeasureCount
                ? ScoreX1
                : ScoreX0 + (boundary * (ScoreX1 - ScoreX0) / VisibleMeasureCount))
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
}
