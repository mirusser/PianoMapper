using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Draws a live oscilloscope trace for the primary note's current sample window. Owns
/// its own shader/VAO/VBO and must be constructed after the GL context is current
/// (i.e. from OnLoad).
/// </summary>
public sealed class OscilloscopeRenderer : IDisposable
{
    private static readonly float[] TraceColor = [0.9f, 0.7f, 0.1f];

    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public OscilloscopeRenderer()
    {
        shaderProgram = ShaderProgram.CreateSolidColorProgram();

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        ShaderProgram.ConfigurePositionColorVertexLayout();

        GL.BindVertexArray(0);
    }

    public void Render(IReadOnlyList<short> sampleWindow)
    {
        vertices.Clear();

        foreach (var point in OscilloscopeLayout.BuildPolyline(sampleWindow))
        {
            vertices.Add(point.X);
            vertices.Add(point.Y);
            vertices.Add(TraceColor[0]);
            vertices.Add(TraceColor[1]);
            vertices.Add(TraceColor[2]);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.LineStrip, 0, vertices.Count / ShaderProgram.PositionColorFloatsPerVertex);
    }

    public void Dispose()
    {
        GL.DeleteProgram(shaderProgram);
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
        GC.SuppressFinalize(this);
    }
}
