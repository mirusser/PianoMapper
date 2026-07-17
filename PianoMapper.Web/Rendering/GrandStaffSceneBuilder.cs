using PianoMapper.Rendering;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

internal static class GrandStaffSceneBuilder
{
    private const double StaffX0 = -0.92;
    private const double StaffX1 = 0.96;
    private const double ClefX = -0.87;
    private const double LedgerLineHalfWidth = 0.065;
    private const double ViewY0 = -0.9;
    private const double ViewY1 = 0.9;
    private const int TrebleClefHeightInStaffSpaces = 6;
    private const int BassClefHeightInStaffSpaces = 3;
    private static readonly NoteValue[] supportedLiveNoteValues =
    [
        new(1),
        new(2),
        new(4),
        new(8),
        new(16),
    ];

    internal static int ClampFirstVisibleMeasure(Score score, int requestedMeasure)
    {
        int lastMeasure = Math.Max(0, score.Measures.Count - 1);
        return Math.Clamp(requestedMeasure, 0, lastMeasure);
    }

    internal static GrandStaffScene BuildScore(
        Score score,
        int firstVisibleMeasure,
        double? cursorBeats = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null)
    {
        int clampedMeasure = ClampFirstVisibleMeasure(score, firstVisibleMeasure);
        var lines = CreateStaffLines();
        var glyphs = CreateClefGlyphs();
        var renderedNotes = new List<GrandStaffNote>();

        float barlineY0 = GrandStaffLayout.BassLineYs[0];
        float barlineY1 = GrandStaffLayout.TrebleLineYs[^1];
        lines.AddRange(GrandStaffLayout.GetScoreBarlineXs(clampedMeasure, score.Measures.Count)
            .Select(x => new GrandStaffLine(x, barlineY0, x, barlineY1, GrandStaffLineKind.Barline)));
        if (cursorBeats.HasValue)
        {
            float cursorX = GrandStaffLayout.MapAbsoluteBeatToScoreX(
                cursorBeats.Value,
                score.TimeSignature,
                clampedMeasure);
            if (cursorX >= GrandStaffLayout.ScoreX0 && cursorX <= GrandStaffLayout.ScoreX1)
            {
                lines.Add(new GrandStaffLine(
                    cursorX,
                    barlineY0,
                    cursorX,
                    barlineY1,
                    GrandStaffLineKind.Cursor));
            }
        }

        foreach (var note in score.Measures.SelectMany(measure => measure.Notes))
        {
            if (GrandStaffLayout.GetScoreNoteLayout(note, score.TimeSignature, clampedMeasure) is not { } layout)
            {
                continue;
            }

            Verdict? verdict = verdicts is not null && verdicts.TryGetValue(note, out var visibleVerdict)
                ? visibleVerdict
                : null;

            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                layout.X,
                layout.Position.Y,
                DurationSeconds: 0,
                IsActive: false,
                IsFilled: layout.HeadStyle == NoteHeadStyle.Filled,
                layout.HasStem,
                layout.StemDirection,
                layout.HasDot,
                layout.FlagCount,
                verdict));
            lines.AddRange(layout.Position.LedgerLineYs.Select(
                y => new GrandStaffLine(
                    layout.X - LedgerLineHalfWidth,
                    y,
                    layout.X + LedgerLineHalfWidth,
                    y,
                    GrandStaffLineKind.Ledger)));
            if (layout.Position.NeedsAccidental)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetAccidentalGlyph(note.Pitch.Alter),
                    layout.X - 0.055,
                    layout.Position.Y,
                    GrandStaffGlyphKind.Accidental));
            }
        }

        return new GrandStaffScene(lines, glyphs, renderedNotes);
    }

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
        int firstVisibleMeasure = GrandStaffLayout.GetLiveFirstVisibleMeasure(currentTime, timeSignature, tempo);
        AddLiveMeasureGrid(lines, firstVisibleMeasure, currentTime, timeSignature, tempo);

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
            var staffLines = position.Staff == Staff.Treble
                ? GrandStaffLayout.TrebleLineYs
                : GrandStaffLayout.BassLineYs;
            var stemDirection = position.Y < staffLines[2]
                ? StemDirection.Up
                : StemDirection.Down;
            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                layout.Value.X,
                position.Y,
                durationSeconds,
                IsActive: isActive,
                IsFilled: isFilled,
                HasStem: hasStem,
                StemDirection: stemDirection,
                FlagCount: flagCount,
                DurationEndX: layout.Value.DurationEndX));
            lines.AddRange(position.LedgerLineYs.Select(
                y => new GrandStaffLine(
                    layout.Value.X - LedgerLineHalfWidth,
                    y,
                    layout.Value.X + LedgerLineHalfWidth,
                    y,
                    GrandStaffLineKind.Ledger)));
            if (position.NeedsAccidental)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetAccidentalGlyph(note.Pitch.Alter),
                    layout.Value.X - 0.055,
                    position.Y,
                    GrandStaffGlyphKind.Accidental));
            }
        }

        var scene = new GrandStaffScene(lines, glyphs, renderedNotes, ShouldClipNotesAtClefs: true);
        return selectedOctave.HasValue
            ? FitToSelectedOctave(scene, selectedOctave.Value)
            : scene;
    }

    private static List<GrandStaffLine> CreateStaffLines() =>
        GrandStaffLayout.TrebleLineYs
            .Concat(GrandStaffLayout.BassLineYs)
            .Select(y => new GrandStaffLine(StaffX0, y, StaffX1, y, GrandStaffLineKind.Staff))
            .ToList();

    private static void AddLiveMeasureGrid(
        List<GrandStaffLine> lines,
        int firstVisibleMeasure,
        TimeSpan currentTime,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        float barlineY0 = GrandStaffLayout.BassLineYs[0];
        float barlineY1 = GrandStaffLayout.TrebleLineYs[^1];
        lines.AddRange(GrandStaffLayout.GetScoreBarlineXs(
                firstVisibleMeasure,
                firstVisibleMeasure + GrandStaffLayout.VisibleMeasureCount)
            .Select(x => new GrandStaffLine(x, barlineY0, x, barlineY1, GrandStaffLineKind.Barline)));

        int visibleBeatCount = GrandStaffLayout.VisibleMeasureCount * timeSignature.Numerator;
        for (int beatIndex = 1; beatIndex < visibleBeatCount; beatIndex++)
        {
            if (beatIndex % timeSignature.Numerator == 0)
            {
                continue;
            }

            double absoluteBeat = (firstVisibleMeasure * timeSignature.Numerator) + beatIndex;
            float x = GrandStaffLayout.MapAbsoluteBeatToScoreX(absoluteBeat, timeSignature, firstVisibleMeasure);
            lines.Add(new GrandStaffLine(x, barlineY0, x, barlineY1, GrandStaffLineKind.Beat));
        }

        double currentBeat = MusicalTime.DurationToBeats(currentTime, tempo);
        float cursorX = GrandStaffLayout.MapAbsoluteBeatToScoreX(currentBeat, timeSignature, firstVisibleMeasure);
        if (cursorX >= GrandStaffLayout.ScoreX0 && cursorX <= GrandStaffLayout.ScoreX1)
        {
            lines.Add(new GrandStaffLine(cursorX, barlineY0, cursorX, barlineY1, GrandStaffLineKind.Cursor));
        }
    }

    private static List<GrandStaffGlyph> CreateClefGlyphs() =>
    [
        new(
            "𝄞",
            ClefX,
            GrandStaffLayout.TrebleLineYs[2],
            GrandStaffGlyphKind.Clef,
            TrebleClefHeightInStaffSpaces * (GrandStaffLayout.TrebleLineYs[1] - GrandStaffLayout.TrebleLineYs[0])),
        new(
            "𝄢",
            ClefX,
            GrandStaffLayout.BassLineYs[3],
            GrandStaffGlyphKind.Clef,
            BassClefHeightInStaffSpaces * (GrandStaffLayout.BassLineYs[1] - GrandStaffLayout.BassLineYs[0])),
    ];

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
            yValues.Add(position.Y);
            yValues.AddRange(position.LedgerLineYs.Select(y => (double)y));
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
            scene.Notes.Select(note => note with { Y = MapY(note.Y) }).ToArray(),
            scene.ShouldClipNotesAtClefs);
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
}
