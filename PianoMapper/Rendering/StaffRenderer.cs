using OpenTK.Graphics.OpenGL4;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Rendering;

internal sealed class StaffRenderer : IDisposable
{
    private static readonly float[] StaffColor = [0.8f, 0.8f, 0.8f];
    private static readonly float[] BeatColor = [0.25f, 0.3f, 0.38f];
    private static readonly float[] NoteColor = [0.95f, 0.95f, 0.95f];
    private static readonly float[] CursorColor = [0.2f, 0.9f, 0.4f];
    private static readonly float[] CorrectColor = [0.2f, 0.9f, 0.3f];
    private static readonly float[] WrongColor = [0.95f, 0.2f, 0.2f];
    private static readonly float[] TimingColor = [0.95f, 0.8f, 0.2f];
    private static readonly float[] DurationColor = [0.95f, 0.45f, 0.15f];
    private static readonly float[] MissedColor = [0.45f, 0.45f, 0.45f];

    private const float StaffLineHalfHeight = 0.002f;
    private const float NoteHeadHalfWidth = 0.025f;
    private const float NoteHeadHalfHeight = 0.014f;
    private const float LedgerHalfWidth = 0.037f;
    private const float ClefX = -0.92f;
    private const float ClefCellWidth = 0.012f;
    private const float ClefCellHeight = 0.018f;
    private const float AccidentalGlyphWidth = 0.010f;
    private const float AccidentalGlyphHeight = 0.025f;
    private const float AccidentalXOffset = 0.055f;
    private const float AccidentalYOffset = 0.025f;
    private const float HollowBorder = 0.004f;
    private const float StemHalfWidth = 0.002f;
    private const float StemLength = 0.15f;
    private const float FlagWidth = 0.04f;
    private const float FlagHalfHeight = 0.006f;
    private const float FlagSpacing = 0.02f;
    private const float DotHalfSize = 0.006f;

    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public StaffRenderer()
    {
        shaderProgram = ShaderProgram.CreateSolidColorProgram();
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        ShaderProgram.ConfigurePositionColorVertexLayout();
        GL.BindVertexArray(0);
    }

    public void Render(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan now,
        TextRenderer textRenderer,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        vertices.Clear();
        AppendStaffLines(GrandStaffLayout.TrebleLineYs);
        AppendStaffLines(GrandStaffLayout.BassLineYs);
        AppendGlyph(StaffGlyphs.TrebleClef, GrandStaffLayout.TrebleLineYs[1]);
        AppendGlyph(StaffGlyphs.BassClef, GrandStaffLayout.BassLineYs[3]);
        AppendLiveMeasureGrid(now, timeSignature, tempo);
        AppendPerformedNotes(notes, now, textRenderer, timeSignature, tempo);

        DrawVertices();
    }

    public void RenderScore(
        Score score,
        int firstVisibleMeasure,
        IReadOnlyList<PerformedNote> performedNotes,
        TimeSpan now,
        TextRenderer textRenderer,
        double? cursorBeat = null,
        IReadOnlyDictionary<ScoreNote, Verdict>? verdicts = null)
    {
        vertices.Clear();
        AppendStaffLines(GrandStaffLayout.TrebleLineYs);
        AppendStaffLines(GrandStaffLayout.BassLineYs);
        AppendGlyph(StaffGlyphs.TrebleClef, GrandStaffLayout.TrebleLineYs[1]);
        AppendGlyph(StaffGlyphs.BassClef, GrandStaffLayout.BassLineYs[3]);

        foreach (float barlineX in GrandStaffLayout.GetScoreBarlineXs(firstVisibleMeasure, score.Measures.Count))
        {
            AppendQuad(
                barlineX - StaffLineHalfHeight,
                barlineX + StaffLineHalfHeight,
                GrandStaffLayout.BassLineYs[0],
                GrandStaffLayout.TrebleLineYs[^1],
                StaffColor);
        }

        foreach (var note in score.Measures.SelectMany(measure => measure.Notes))
        {
            var layout = GrandStaffLayout.GetScoreNoteLayout(note, score.TimeSignature, firstVisibleMeasure);
            if (layout is null)
            {
                continue;
            }

            float[] color = verdicts is not null && verdicts.TryGetValue(note, out var verdict)
                ? GetVerdictColor(verdict)
                : NoteColor;
            AppendScoreNote(note, layout.Value, textRenderer, color);
        }

        AppendPerformedNotes(performedNotes, now, textRenderer, score.TimeSignature, score.Tempo);

        if (cursorBeat.HasValue)
        {
            float cursorX = GrandStaffLayout.MapAbsoluteBeatToScoreX(cursorBeat.Value, score.TimeSignature, firstVisibleMeasure);
            if (cursorX >= GrandStaffLayout.ScoreX0 && cursorX <= GrandStaffLayout.ScoreX1)
            {
                AppendQuad(
                    cursorX - StaffLineHalfHeight,
                    cursorX + StaffLineHalfHeight,
                    GrandStaffLayout.BassLineYs[0],
                    GrandStaffLayout.TrebleLineYs[^1],
                    CursorColor);
            }
        }

        DrawVertices();
    }

    public void Dispose()
    {
        GL.DeleteProgram(shaderProgram);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
        GC.SuppressFinalize(this);
    }

    private void AppendStaffLines(IReadOnlyList<float> lineYs)
    {
        foreach (float lineY in lineYs)
        {
            AppendHorizontalLine(-1f, 1f, lineY, StaffLineHalfHeight);
        }
    }

    private void AppendGlyph(StaffGlyph glyph, float anchorY)
    {
        foreach (var quad in ClefGlyphLayout.BuildQuads(glyph, ClefX, anchorY, ClefCellWidth, ClefCellHeight))
        {
            AppendQuad(quad.X0, quad.X1, quad.Y0, quad.Y1, StaffColor);
        }
    }

    private void AppendHorizontalLine(float x0, float x1, float y, float halfHeight) =>
        AppendQuad(x0, x1, y - halfHeight, y + halfHeight, StaffColor);

    private void AppendLiveMeasureGrid(TimeSpan now, TimeSignature timeSignature, Tempo tempo)
    {
        float y0 = GrandStaffLayout.BassLineYs[0];
        float y1 = GrandStaffLayout.TrebleLineYs[^1];
        foreach (var line in GrandStaffLayout.GetLiveMeasureGridLines(now, timeSignature, tempo))
        {
            switch (line.Kind)
            {
                case GridLineKind.Barline:
                    AppendQuad(line.X - StaffLineHalfHeight, line.X + StaffLineHalfHeight, y0, y1, StaffColor);
                    break;
                case GridLineKind.Beat:
                    AppendQuad(
                        line.X - (StaffLineHalfHeight / 2f),
                        line.X + (StaffLineHalfHeight / 2f),
                        y0,
                        y1,
                        BeatColor);
                    break;
                case GridLineKind.Cursor:
                    AppendQuad(line.X - StaffLineHalfHeight, line.X + StaffLineHalfHeight, y0, y1, CursorColor);
                    break;
            }
        }
    }

    private void AppendScoreNote(ScoreNote note, ScoreNoteLayout layout, TextRenderer textRenderer, float[] color)
    {
        foreach (float ledgerY in layout.Position.LedgerLineYs)
        {
            AppendHorizontalLine(layout.X - LedgerHalfWidth, layout.X + LedgerHalfWidth, ledgerY, StaffLineHalfHeight);
        }

        if (layout.HeadStyle == NoteHeadStyle.Filled)
        {
            AppendQuad(
                layout.X - NoteHeadHalfWidth,
                layout.X + NoteHeadHalfWidth,
                layout.Position.Y - NoteHeadHalfHeight,
                layout.Position.Y + NoteHeadHalfHeight,
                color);
        }
        else
        {
            AppendHollowNoteHead(layout.X, layout.Position.Y, color);
        }

        if (layout.HasStem)
        {
            AppendStemAndFlag(layout, color);
        }

        if (layout.HasDot)
        {
            float dotX = layout.X + NoteHeadHalfWidth + (2 * DotHalfSize);
            AppendQuad(
                dotX - DotHalfSize,
                dotX + DotHalfSize,
                layout.Position.Y - DotHalfSize,
                layout.Position.Y + DotHalfSize,
                color);
        }

        if (layout.Position.NeedsAccidental)
        {
            textRenderer.Render(
                GetAccidentalText(note.Pitch.Alter),
                layout.X - AccidentalXOffset,
                layout.Position.Y - AccidentalYOffset,
                AccidentalGlyphWidth,
                AccidentalGlyphHeight,
                color);
        }
    }

    private void AppendPerformedNotes(
        IReadOnlyList<PerformedNote> notes,
        TimeSpan now,
        TextRenderer textRenderer,
        TimeSignature timeSignature,
        Tempo tempo)
    {
        foreach (var note in notes)
        {
            var endTime = note.ReleaseTime ?? now;
            var layout = GrandStaffLayout.GetLiveNoteLayout(
                note.Pitch,
                note.StartTime,
                endTime,
                now,
                timeSignature,
                tempo);
            if (!layout.HasValue)
            {
                continue;
            }

            var position = layout.Value.Position;
            if (layout.Value.DurationEndX > layout.Value.X)
            {
                AppendQuad(
                    layout.Value.X,
                    layout.Value.DurationEndX,
                    position.Y - StaffLineHalfHeight,
                    position.Y + StaffLineHalfHeight,
                    NoteColor);
            }

            foreach (float ledgerY in position.LedgerLineYs)
            {
                AppendHorizontalLine(
                    layout.Value.X - LedgerHalfWidth,
                    layout.Value.X + LedgerHalfWidth,
                    ledgerY,
                    StaffLineHalfHeight);
            }

            AppendQuad(
                layout.Value.X - NoteHeadHalfWidth,
                layout.Value.X + NoteHeadHalfWidth,
                position.Y - NoteHeadHalfHeight,
                position.Y + NoteHeadHalfHeight,
                NoteColor);

            if (position.NeedsAccidental)
            {
                textRenderer.Render(
                    GetAccidentalText(note.Pitch.Alter),
                    layout.Value.X - AccidentalXOffset,
                    position.Y - AccidentalYOffset,
                    AccidentalGlyphWidth,
                    AccidentalGlyphHeight,
                    NoteColor);
            }
        }
    }

    private void AppendHollowNoteHead(float x, float y, float[] color)
    {
        AppendQuad(x - NoteHeadHalfWidth, x + NoteHeadHalfWidth, y - NoteHeadHalfHeight, y - NoteHeadHalfHeight + HollowBorder, color);
        AppendQuad(x - NoteHeadHalfWidth, x + NoteHeadHalfWidth, y + NoteHeadHalfHeight - HollowBorder, y + NoteHeadHalfHeight, color);
        AppendQuad(x - NoteHeadHalfWidth, x - NoteHeadHalfWidth + HollowBorder, y - NoteHeadHalfHeight, y + NoteHeadHalfHeight, color);
        AppendQuad(x + NoteHeadHalfWidth - HollowBorder, x + NoteHeadHalfWidth, y - NoteHeadHalfHeight, y + NoteHeadHalfHeight, color);
    }

    private void AppendStemAndFlag(ScoreNoteLayout layout, float[] color)
    {
        bool isUp = layout.StemDirection == StemDirection.Up;
        float stemX = layout.X + (isUp ? NoteHeadHalfWidth : -NoteHeadHalfWidth);
        float stemEndY = layout.Position.Y + (isUp ? StemLength : -StemLength);
        AppendQuad(
            stemX - StemHalfWidth,
            stemX + StemHalfWidth,
            Math.Min(layout.Position.Y, stemEndY),
            Math.Max(layout.Position.Y, stemEndY),
            color);

        float flagEndX = stemX + (isUp ? FlagWidth : -FlagWidth);
        float flagYDirection = isUp ? -1f : 1f;
        for (int flagIndex = 0; flagIndex < layout.FlagCount; flagIndex++)
        {
            float flagY = stemEndY + (flagIndex * FlagSpacing * flagYDirection);
            AppendQuad(
                Math.Min(stemX, flagEndX),
                Math.Max(stemX, flagEndX),
                flagY - FlagHalfHeight,
                flagY + FlagHalfHeight,
                color);
        }
    }

    private void DrawVertices()
    {
        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / ShaderProgram.PositionColorFloatsPerVertex);
    }

    private void AppendQuad(float x0, float x1, float y0, float y1, float[] color)
    {
        AppendVertex(x0, y0, color);
        AppendVertex(x1, y0, color);
        AppendVertex(x1, y1, color);
        AppendVertex(x0, y0, color);
        AppendVertex(x1, y1, color);
        AppendVertex(x0, y1, color);
    }

    private void AppendVertex(float x, float y, float[] color)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(color[0]);
        vertices.Add(color[1]);
        vertices.Add(color[2]);
    }

    private static string GetAccidentalText(int alter) =>
        alter > 0 ? new string('#', alter) : new string('♭', -alter);

    private static float[] GetVerdictColor(Verdict verdict) => verdict switch
    {
        Verdict.Correct => CorrectColor,
        Verdict.WrongPitch => WrongColor,
        Verdict.Early or Verdict.Late => TimingColor,
        Verdict.TooShort or Verdict.TooLong => DurationColor,
        Verdict.Missed => MissedColor,
        _ => NoteColor,
    };
}
