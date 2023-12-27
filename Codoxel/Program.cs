using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using MoreLinq.Extensions;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Extensions;
using Buffer = Silk.NET.OpenGL.Buffer;
using ShaderProgram = Silk.NET.OpenGL.Program;

const int windowCount = 1;
const int multiSampleCount = 4;

const string resourcesDirectory = "Resources";
var shadersDirectory = Path.Combine(resourcesDirectory, "Shaders");

Vertex[] vertices =
[
    new Vertex(new Vector3(0.5f, 0.5f, 0.0f), Color.Red),
    new Vertex(new Vector3(0.0f, -0.5f, 0.0f), Color.Green),
    new Vertex(new Vector3(-0.5f, -0.5f, 0.0f), Color.Blue),
    new Vertex(new Vector3(-0.5f, 0.5f, 0.0f), Color.White)
];

uint[] indices =
[
    0u, 1u, 3u,
    1u, 2u, 3u
];

var windowManager = new WindowManager();
Enumerable.Range(0, windowCount).ForEach(_ => windowManager.CreateWindow(WindowOptions.Default with
{
    API = GraphicsAPI.Default with
    {
        Version = new APIVersion(4, 6),
        Flags = Debugger.IsAttached ? ContextFlags.Debug : ContextFlags.Default
    },
    Samples = multiSampleCount
}));

windowManager.Windows.ForEach(window => window.Load += () =>
{
    var input = window.CreateInput();
    input.Keyboards.ForEach(keyboard => keyboard.KeyDown += (_, key, _) =>
    {
        if (key == Key.Escape)
            window.Close();
    });

    window.Closing += () =>
    {
        if (windowManager.Windows.Count == 1)
            windowManager.Stop();
    };

    var gl = window.CreateOpenGL();

    if (Debugger.IsAttached)
    {
        gl.Enable(EnableCap.DebugOutput);
        gl.Enable(GLEnum.DebugOutputSynchronous);
        unsafe
        {
            gl.DebugMessageCallback((_, type, id, severity, _, message, _) =>
                Console.WriteLine
                (
                    $"[{severity.ToString()[13..]}:{type.ToString()[9..]}]({id}): {Marshal.PtrToStringAnsi(message)}"
                ), null);
        }
    }

    // Shaders
    var vertexShaderBinary = File.ReadAllBytes(Path.Combine(shadersDirectory, "Simple.vert.spv"));
    var fragmentShaderBinary = File.ReadAllBytes(Path.Combine(shadersDirectory, "Simple.frag.spv"));

    var vertexShaderProgram = CreateShader(gl, ShaderType.VertexShader, vertexShaderBinary, "main");
    var fragmentShaderProgram = CreateShader(gl, ShaderType.FragmentShader, fragmentShaderBinary, "main");

    var pipeline = new ProgramPipeline(gl.CreateProgramPipeline());
    gl.UseProgramStages(pipeline.Handle, UseProgramStageMask.VertexShaderBit, vertexShaderProgram.Handle);
    gl.UseProgramStages(pipeline.Handle, UseProgramStageMask.FragmentShaderBit, fragmentShaderProgram.Handle);

    // Buffers
    Span<Buffer> buffers = stackalloc Buffer[2];
    gl.CreateBuffers(buffers);
    var vertexBuffer = buffers[0];
    var elementBuffer = buffers[1];

    gl.NamedBufferData(vertexBuffer.Handle, vertices.AsReadOnlySpan(), GLEnum.StaticDraw);
    gl.NamedBufferData(elementBuffer.Handle, indices.AsReadOnlySpan(), GLEnum.StaticDraw);

    // Vertex Array
    var vertexArray = new Buffer(gl.CreateVertexArray());

    gl.VertexArrayVertexBuffer(vertexArray.Handle, 0, vertexBuffer.Handle, 0, (uint)Marshal.SizeOf<Vertex>());
    gl.VertexArrayElementBuffer(vertexArray.Handle, elementBuffer.Handle);

    gl.EnableVertexArrayAttrib(vertexArray.Handle, 0);
    gl.VertexArrayAttribFormat(vertexArray.Handle, 0, 3, GLEnum.Float, false,
        (uint)Marshal.OffsetOf<Vertex>("_position"));
    gl.VertexArrayAttribBinding(vertexArray.Handle, 0, 0);

    gl.EnableVertexArrayAttrib(vertexArray.Handle, 1);
    gl.VertexArrayAttribFormat(vertexArray.Handle, 1, 3, GLEnum.Float, false, (uint)Marshal.OffsetOf<Vertex>("_color"));
    gl.VertexArrayAttribBinding(vertexArray.Handle, 1, 0);

    // Initialization
    gl.BindVertexArray(vertexArray.Handle);
    gl.BindProgramPipeline(pipeline.Handle);
    gl.ClearColor(Color.CornflowerBlue);

    window.Render += _ =>
    {
        gl.Clear(ClearBufferMask.ColorBufferBit);
        unsafe
        {
            gl.ProgramUniform1(vertexShaderProgram.Handle, 0,
                (float)Math.Sin(window.Time));
            gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Length, DrawElementsType.UnsignedInt, (void*)0);
        }
    };
    window.FramebufferResize += size => gl.Viewport(Point.Empty, new Size(size.X, size.Y));
});

windowManager.Run();
return;

ShaderProgram CreateShader(GL gl, ShaderType shaderType, ReadOnlySpan<byte> binary, string entryPoint)
{
    var fragmentShader = new Shader(gl.CreateShader(shaderType));
    Shader[] shaders = [fragmentShader];
    gl.ShaderBinary(shaders.AsReadOnlySpan(), GLEnum.ShaderBinaryFormatSpirV, binary);
    gl.SpecializeShader(fragmentShader.Handle, entryPoint, ReadOnlySpan<uint>.Empty, ReadOnlySpan<uint>.Empty);
    var shaderProgram = new ShaderProgram(gl.CreateProgram());
    gl.ProgramParameter(shaderProgram.Handle, GLEnum.ProgramSeparable, 1);
    gl.AttachShader(shaderProgram.Handle, fragmentShader.Handle);
    gl.LinkProgram(shaderProgram.Handle);
    gl.DeleteShader(fragmentShader.Handle);
    return shaderProgram;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Vertex(Vector3 position, Color color)
{
    // ReSharper disable once UnusedMember.Local
    private Vector3 _position = position;
    private Vector3 _color = new Vector3(color.R, color.G, color.B) / 255f;
}

internal static class ArrayExtensions
{
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this T[] self)
    {
        return self.AsSpan();
    }
}