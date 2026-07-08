using OpenTK.Graphics.OpenGL4;

namespace PianoMapper.Rendering;

/// <summary>
/// Compiles and links the simple vec2-position + vec3-color shader shared by the
/// piano-roll, oscilloscope, and spectrum renderers.
/// </summary>
internal static class ShaderProgram
{
    /// <summary>Floats per vertex in the shared vec2-position + vec3-color layout.</summary>
    public const int PositionColorFloatsPerVertex = 5;

    private const string VertexShaderSource = """
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

    private const string FragmentShaderSource = """
        #version 330 core
        in vec3 vColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(vColor, 1.0);
        }
        """;

    public static int CreateSolidColorProgram()
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

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

    /// <summary>
    /// Wires vertex attribute pointers for the shared vec2-position + vec3-color
    /// layout against the currently bound VAO/VBO.
    /// </summary>
    public static void ConfigurePositionColorVertexLayout()
    {
        const int stride = PositionColorFloatsPerVertex * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);
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
