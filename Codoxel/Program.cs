using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using MoreLinq.Extensions;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Buffer = Silk.NET.OpenGL.Buffer;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using ShaderProgram = Silk.NET.OpenGL.Program;
using Size = System.Drawing.Size;

const float deg2Rad = MathF.PI / 180;

const int windowCount = 1;
const int multiSampleCount = 4;

const float fieldOfView = 70 * deg2Rad;
const float nearPlane = 0.1f;
const float farPlane = 100f;
const float cameraDistance = 3f;

const string resourcesDirectory = "Resources";
var shadersDirectory = Path.Combine(resourcesDirectory, "Shaders");
var imagesDirectory = Path.Combine(resourcesDirectory, "Images");
var modelsDirectory = Path.Combine(resourcesDirectory, "Models");

var windowManager = new WindowManager();
Enumerable.Range(0, windowCount).ForEach(_ => windowManager.CreateWindow(WindowOptions.Default with
{
    API = GraphicsAPI.Default with
    {
        Version = new APIVersion(4, 6),
        Flags = Debugger.IsAttached ? ContextFlags.Debug : ContextFlags.Default
    },
    Samples = multiSampleCount,
    PreferredDepthBufferBits = 24,
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

    var positions = new List<Vector3>();
    var uvs = new List<Vector2>();
    var vertices = new List<Vertex>();

    File.ReadLines(Path.Combine(modelsDirectory, "Cube.obj"))
        .Select(line => line.Split()).ForEach(tokens =>
        {
            switch (tokens[0])
            {
                case "v":
                    positions.Add(new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
                    break;
                case "vt":
                    uvs.Add(new Vector2(float.Parse(tokens[1]), float.Parse(tokens[2])));
                    break;
                case "f":
                    var face = tokens[1..].Select(token => token.Split('/').Select(int.Parse).ToArray());
                    face.ForEach((indices, _) =>
                    {
                        vertices.Add(new Vertex(positions[indices[0] -1 ], uvs[indices[1] - 1]));
                    });
                    break;
            }
        });

    var indices = new List<uint>();
    vertices.ForEach((_, index) => indices.Add((uint)index));

    var texture = new Texture(gl.CreateTexture(GLEnum.Texture2D));
    gl.TextureParameterI(texture.Handle, TextureParameterName.TextureWrapS, new[] { (int)GLEnum.ClampToEdge });
    gl.TextureParameterI(texture.Handle, TextureParameterName.TextureWrapT, new[] { (int)GLEnum.ClampToEdge });
    gl.TextureParameterI(texture.Handle, TextureParameterName.TextureMinFilter,
        new[] { (int)GLEnum.LinearMipmapLinear });
    gl.TextureParameterI(texture.Handle, TextureParameterName.TextureMagFilter, new[] { (int)GLEnum.Linear });

    using (var image = Image.Load<Rgba32>(Path.Combine(imagesDirectory, "BrickAlbedo.png")))
    {
        var width = (uint)image.Width;
        var height = (uint)image.Height;
        gl.TextureStorage2D(texture.Handle, 1, SizedInternalFormat.Rgba8, width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                gl.TextureSubImage2D<Rgba32>(texture.Handle, 0, 0, y, width, 1, PixelFormat.Rgba,
                    PixelType.UnsignedByte, pixelRow);
            }
        });
    }

    gl.GenerateTextureMipmap(texture.Handle);

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
    gl.VertexArrayAttribFormat(vertexArray.Handle, 1, 2, GLEnum.Float, false, (uint)Marshal.OffsetOf<Vertex>("_uv"));
    gl.VertexArrayAttribBinding(vertexArray.Handle, 1, 0);

    // Initialization
    gl.Enable(EnableCap.DepthTest);
    gl.ClearColor(Color.CornflowerBlue);
    gl.Viewport(Point.Empty, new Size(window.Size.X, window.Size.Y));
    gl.BindVertexArray(vertexArray.Handle);
    gl.BindProgramPipeline(pipeline.Handle);
    gl.BindTextureUnit(0, texture.Handle);

    var projection =
        Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, (float)window.Size.X / window.Size.Y, nearPlane, farPlane);
    var view = Matrix4x4.CreateTranslation(0f, 0f, -cameraDistance);

    gl.ProgramUniformMatrix4(vertexShaderProgram.Handle, 0, false,
        MemoryMarshal.CreateReadOnlySpan(ref projection.M11, 16));
    gl.ProgramUniformMatrix4(vertexShaderProgram.Handle, 1, false,
        MemoryMarshal.CreateReadOnlySpan(ref view.M11, 16));

    window.Render += _ =>
    {
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var model = Matrix4x4.CreateRotationZ((float)window.Time) * Matrix4x4.CreateRotationX((float)window.Time) *
                    Matrix4x4.CreateRotationY((float)window.Time);
        gl.ProgramUniformMatrix4(vertexShaderProgram.Handle, 2, false,
            MemoryMarshal.CreateReadOnlySpan(ref model.M11, 16));
        // gl.ProgramUniform1(vertexShaderProgram.Handle, 3, (float)window.Time);
        unsafe
        {
            gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Count, DrawElementsType.UnsignedInt, (void*)0);
        }
    };
    window.FramebufferResize += size =>
    {
        gl.Viewport(Point.Empty, new Size(size.X, size.Y));
        projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, (float)size.X / size.Y, nearPlane, farPlane);
        gl.ProgramUniformMatrix4(vertexShaderProgram.Handle, 0, false,
            MemoryMarshal.CreateReadOnlySpan(ref projection.M11, 16));
    };
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
internal struct Vertex(Vector3 position, Vector2 uv)
{
    // ReSharper disable once UnusedMember.Local
    public Vector3 _position = position;
    private Vector2 _uv = uv;
}

internal static class ArrayExtensions
{
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this T[] self)
    {
        return self.AsSpan();
    }
}

internal static class ListExtensions
{
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this List<T> self)
    {
        return CollectionsMarshal.AsSpan(self);
    }
}