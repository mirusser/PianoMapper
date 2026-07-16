using PianoMapper.Rendering;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Web.Rendering;

internal static class GrandStaffSceneBuilder
{
    private const double StaffX0 = -0.92;
    private const double StaffX1 = 0.96;
    private const double ClefX = -0.87;
    private const double ViewY0 = -0.9;
    private const double ViewY1 = 0.9;

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
                layout.NeedsFlag,
                verdict));
            lines.AddRange(layout.Position.LedgerLineYs.Select(
                y => new GrandStaffLine(layout.X - 0.04, y, layout.X + 0.04, y, GrandStaffLineKind.Ledger)));
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
        int? selectedOctave = null)
    {
        var lines = CreateStaffLines();
        var glyphs = CreateClefGlyphs();

        var renderedNotes = new List<GrandStaffNote>(notes.Count);
        foreach (var note in notes)
        {
            TimeSpan endTime = note.ReleaseTime ?? currentTime;
            float? x = GrandStaffLayout.GetLiveNoteX(note.StartTime, endTime, currentTime);
            if (!x.HasValue)
            {
                continue;
            }

            var position = GrandStaffLayout.GetLivePosition(note.Pitch);
            double durationSeconds = Math.Max(0, endTime.TotalSeconds - note.StartTime.TotalSeconds);
            renderedNotes.Add(new GrandStaffNote(
                note.Pitch.ToString(),
                x.Value,
                position.Y,
                durationSeconds,
                note.ReleaseTime is null));
            lines.AddRange(position.LedgerLineYs.Select(
                y => new GrandStaffLine(x.Value - 0.04, y, x.Value + 0.04, y, GrandStaffLineKind.Ledger)));
            if (position.NeedsAccidental)
            {
                glyphs.Add(new GrandStaffGlyph(
                    GetAccidentalGlyph(note.Pitch.Alter),
                    x.Value - 0.055,
                    position.Y,
                    GrandStaffGlyphKind.Accidental));
            }
        }

        var scene = new GrandStaffScene(lines, glyphs, renderedNotes);
        return selectedOctave.HasValue
            ? FitToSelectedOctave(scene, selectedOctave.Value)
            : scene;
    }

    private static List<GrandStaffLine> CreateStaffLines() =>
        GrandStaffLayout.TrebleLineYs
            .Concat(GrandStaffLayout.BassLineYs)
            .Select(y => new GrandStaffLine(StaffX0, y, StaffX1, y, GrandStaffLineKind.Staff))
            .ToList();

    private static List<GrandStaffGlyph> CreateClefGlyphs() =>
    [
        new("𝄞", ClefX, GrandStaffLayout.TrebleLineYs[2], GrandStaffGlyphKind.Clef),
        new("𝄢", ClefX, GrandStaffLayout.BassLineYs[2], GrandStaffGlyphKind.Clef),
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
        double MapY(double y) =>
            ViewY0 + ((y - sourceY0) / (sourceY1 - sourceY0) * (ViewY1 - ViewY0));

        return new GrandStaffScene(
            scene.Lines.Select(line => line with { Y0 = MapY(line.Y0), Y1 = MapY(line.Y1) }).ToArray(),
            scene.Glyphs.Select(glyph => glyph with { Y = MapY(glyph.Y) }).ToArray(),
            scene.Notes.Select(note => note with { Y = MapY(note.Y) }).ToArray());
    }

    private static string GetAccidentalGlyph(int alter) => alter switch
    {
        -2 => "𝄫",
        -1 => "♭",
        1 => "♯",
        2 => "𝄪",
        _ => string.Empty,
    };
}
