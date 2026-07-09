using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Draws text built from <see cref="BitmapFont"/>/<see cref="TextLayout"/> pixel quads.
/// Owns its own shader/VAO/VBO and must be constructed after the GL context is current
/// (i.e. from OnLoad). A single shared instance is used for all on-screen text (bar
/// labels, octave indicator, panel labels) rather than one per purpose.
/// </summary>
public sealed class TextRenderer : IDisposable
{
    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public TextRenderer()
    {
        shaderProgram = ShaderProgram.CreateSolidColorProgram();

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        ShaderProgram.ConfigurePositionColorVertexLayout();

        GL.BindVertexArray(0);
    }

    public void Render(string text, float x, float y, float glyphWidth, float glyphHeight, float[] color)
    {
        vertices.Clear();

        foreach (var quad in TextLayout.BuildQuads(text, x, y, glyphWidth, glyphHeight))
        {
            AppendQuad(quad, color);
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
