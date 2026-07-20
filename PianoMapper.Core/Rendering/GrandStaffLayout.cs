using PianoMapper.Music;

namespace PianoMapper.Rendering;

public static class GrandStaffLayout
{
    public const float DiatonicStep = 0.045f;
    public const float MiddleCY = (PianoRollLayout.BandY0 + PianoRollLayout.BandY1) / 2f;
    public const float ScoreX0 = -0.56f;
    public const float ScoreX1 = 0.96f;
    public const int VisibleMeasureCount = 6;

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

    public static int GetLiveFirstVisibleMeasure(
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        double currentBeat = MusicalTime.DurationToBeats(currentTime, tempo);
        int currentMeasure = Math.Max(0, (int)Math.Floor(currentBeat / timeSignature.Numerator));
        return currentMeasure / VisibleMeasureCount * VisibleMeasureCount;
    }

    public static LiveNoteLayout? GetLiveNoteLayout(
        Pitch pitch,
        TimeSpan startTime,
        TimeSpan endTime,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        int firstVisibleMeasure = GetLiveFirstVisibleMeasure(currentTime, timeSignature, tempo);
        double windowStartBeat = firstVisibleMeasure * timeSignature.Numerator;
        double windowEndBeat = (firstVisibleMeasure + VisibleMeasureCount) * timeSignature.Numerator;
        double startBeat = MusicalTime.DurationToBeats(startTime, tempo);
        double endBeat = Math.Max(startBeat, MusicalTime.DurationToBeats(endTime, tempo));
        if (endBeat <= windowStartBeat || startBeat >= windowEndBeat)
        {
            return null;
        }

        return new LiveNoteLayout(
            MapAbsoluteBeatToScoreX(startBeat, timeSignature, firstVisibleMeasure),
            MapAbsoluteBeatToScoreX(endBeat, timeSignature, firstVisibleMeasure),
            GetLivePosition(pitch));
    }

    public static IReadOnlyList<GridLine> GetLiveMeasureGridLines(
        TimeSpan now,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        int firstVisibleMeasure = GetLiveFirstVisibleMeasure(now, timeSignature, tempo);
        var lines = new List<GridLine>();

        foreach (float barlineX in GetScoreBarlineXs(firstVisibleMeasure, firstVisibleMeasure + VisibleMeasureCount))
        {
            lines.Add(new GridLine(barlineX, GridLineKind.Barline));
        }

        int visibleBeatCount = VisibleMeasureCount * timeSignature.Numerator;
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
        int firstVisibleMeasure)
    {
        if (note.MeasureIndex < firstVisibleMeasure || note.MeasureIndex >= firstVisibleMeasure + VisibleMeasureCount)
        {
            return null;
        }

        var position = GetPosition(note.Pitch, note.Staff);
        var stemDirection = ResolveStemDirection(note.StemDirection, position);
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
}
