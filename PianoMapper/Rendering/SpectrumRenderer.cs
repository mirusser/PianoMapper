using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Draws magnitude-spectrum bars for the primary note's current FFT window. Owns its
/// own shader/VAO/VBO and must be constructed after the GL context is current (i.e.
/// from OnLoad).
/// </summary>
public sealed class SpectrumRenderer : IDisposable
{
    private static readonly float[] BarColor = [0.3f, 0.6f, 0.9f];

    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public SpectrumRenderer()
    {
        shaderProgram = ShaderProgram.CreateSolidColorProgram();

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        ShaderProgram.ConfigurePositionColorVertexLayout();

        GL.BindVertexArray(0);
    }

    public void Render(IReadOnlyList<double> magnitudes)
    {
        vertices.Clear();

        foreach (var bar in SpectrumLayout.BuildBars(magnitudes))
        {
            AppendQuad(bar);
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

    private void AppendQuad(BarRect rect)
    {
        AppendVertex(rect.X0, rect.Y0);
        AppendVertex(rect.X1, rect.Y0);
        AppendVertex(rect.X1, rect.Y1);

        AppendVertex(rect.X0, rect.Y0);
        AppendVertex(rect.X1, rect.Y1);
        AppendVertex(rect.X0, rect.Y1);
    }

    private void AppendVertex(float x, float y)
    {
        vertices.Add(x);
        vertices.Add(y);
        vertices.Add(BarColor[0]);
        vertices.Add(BarColor[1]);
        vertices.Add(BarColor[2]);
    }
}
