using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Draws the current note timeline as scrolling bars. Owns its own shader/VAO/VBO
/// and must be constructed after the GL context is current (i.e. from OnLoad).
/// </summary>
public sealed class PianoRollRenderer : IDisposable
{
    private const int FloatsPerVertex = 5; // vec2 position + vec3 color
    private static readonly float[] NoteColor = [0.2f, 0.8f, 0.4f];

    private readonly int vao;
    private readonly int vbo;
    private readonly int shaderProgram;
    private readonly List<float> vertices = [];

    public PianoRollRenderer()
    {
        shaderProgram = CreateShaderProgram();

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        const int stride = FloatsPerVertex * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public void Render(IReadOnlyList<NoteInstance> notes, TimeSpan now)
    {
        vertices.Clear();

        foreach (var note in notes)
        {
            var rect = PianoRollLayout.GetBarRect(note, now);
            if (rect is null)
            {
                continue;
            }

            AppendQuad(rect.Value);
        }

        if (vertices.Count == 0)
        {
            return;
        }

        GL.UseProgram(shaderProgram);
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / FloatsPerVertex);
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
        vertices.Add(NoteColor[0]);
        vertices.Add(NoteColor[1]);
        vertices.Add(NoteColor[2]);
    }

    private static int CreateShaderProgram()
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec3 aColor;
            out vec3 vColor;
            void main()
            {
                vColor = aColor;
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            in vec3 vColor;
            out vec4 FragColor;
            void main()
            {
                FragColor = vec4(vColor, 1.0);
            }
            """;

        var vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        var program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var success);
        if (success == 0)
        {
            var infoLog = GL.GetProgramInfoLog(program);
            throw new InvalidOperationException($"Shader program link failed: {infoLog}");
        }

        GL.DetachShader(program, vertexShader);
        GL.DetachShader(program, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        var shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out var success);
        if (success == 0)
        {
            var infoLog = GL.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"{type} compilation failed: {infoLog}");
        }

        return shader;
    }
}
