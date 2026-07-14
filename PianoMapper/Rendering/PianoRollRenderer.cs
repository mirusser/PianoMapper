using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Draws the current note timeline as scrolling bars. Owns its own shader/VAO/VBO
/// and must be constructed after the GL context is current (i.e. from OnLoad).
/// </summary>
internal sealed class PianoRollRenderer : IDisposable
{
    private static readonly float[] LabelColor = [1f, 1f, 1f];
    private const float LabelGlyphWidth = 0.012f;
    private const float LabelGlyphHeight = 0.03f;
    private const float LabelYOffset = 0.01f;

    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public PianoRollRenderer()
    {
        shaderProgram = ShaderProgram.CreateSolidColorProgram();

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        ShaderProgram.ConfigurePositionColorVertexLayout();

        GL.BindVertexArray(0);
    }

    public void Render(IReadOnlyList<PerformedNote> notes, TimeSpan now, TextRenderer textRenderer)
    {
        vertices.Clear();

        foreach (var note in notes)
        {
            var rect = PianoRollLayout.GetBarRect(note, now);
            if (rect is null)
            {
                continue;
            }

            AppendQuad(rect.Value, NoteColors.GetColor(note.Pitch));
            textRenderer.Render(note.Pitch.ToString(), rect.Value.X0, rect.Value.Y1 + LabelYOffset, LabelGlyphWidth, LabelGlyphHeight, LabelColor);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / ShaderProgram.PositionColorFloatsPerVertex);
    }

    public void Dispose()
    {
        GL.DeleteProgram(shaderProgram);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
        GC.SuppressFinalize(this);
    }

    private void AppendQuad(BarRect rect, float[] color)
    {
        AppendVertex(rect.X0, rect.Y0, color);
        AppendVertex(rect.X1, rect.Y0, color);
        AppendVertex(rect.X1, rect.Y1, color);

        AppendVertex(rect.X0, rect.Y0, color);
        AppendVertex(rect.X1, rect.Y1, color);
        AppendVertex(rect.X0, rect.Y1, color);
    }

    private void AppendVertex(float x, float y, float[] color)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(color[0]);
        vertices.Add(color[1]);
        vertices.Add(color[2]);
    }
}
