using System;
using System.IO;
using System.Runtime.InteropServices;
using MoreLinq.Extensions;
using Nuke.Common;
using Silk.NET.Shaderc;

class Build : NukeBuild
{
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
        });

    Target Restore => _ => _
        .Executes(() =>
        {
        });

    Target Compile => _ => _
        .DependsOn(CompileShaders)
        .DependsOn(Restore)
        .Executes(() =>
        {
        });

    Target CompileShaders => _ => _
        .Executes(() =>
        {
            var inputPath = RootDirectory / "build" / "Resources" / "Shaders";
            var outputPath = RootDirectory / "Codoxel" / "Resources" / "Shaders";
            var shaderPaths = Directory.GetFiles(inputPath);

            var shaderc = Shaderc.GetApi();
            unsafe
            {
                var compiler = shaderc.CompilerInitialize();
                var compilerOptions = shaderc.CompileOptionsInitialize();
                shaderPaths.ForEach(shaderPath =>
                {
                    var outputFile = outputPath / Path.GetFileName(shaderPath) +
                                     ".spv";
                    var shaderSourceCode = File.ReadAllText(shaderPath);
                    var shaderKind = Path.GetExtension(shaderPath)[1..] switch
                    {
                        "vert" => ShaderKind.VertexShader,
                        "frag" => ShaderKind.FragmentShader,
                        _ => throw new NotImplementedException()
                    };
                    shaderc.CompileOptionsSetTargetEnv(compilerOptions, TargetEnv.Opengl, 450);
                    var compilationResult = shaderc.CompileIntoSpv(compiler, shaderSourceCode,
                        (nuint)shaderSourceCode.Length, shaderKind,
                        shaderPath,
                        "main", compilerOptions);
                    var compilationStatus = shaderc.ResultGetCompilationStatus(compilationResult);
                    switch (compilationStatus)
                    {
                        case CompilationStatus.Success:

                            break;
                        default:
                            // write to stderr
                            Console.Error.WriteLine(
                                $"{compilationStatus}:\n{shaderc.ResultGetErrorMessageS(compilationResult)}");
                            Environment.Exit(1);
                            break;
                    }

                    var shaderBinary = shaderc.ResultGetBytes(compilationResult);
                    var shaderBinarySize = (int)shaderc.ResultGetLength(compilationResult);
                    var byteArray = new byte[shaderBinarySize];
                    Marshal.Copy((IntPtr)shaderBinary, byteArray, 0, shaderBinarySize);
                    File.WriteAllBytes(outputFile, byteArray);
                });
            }
        });

    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Compile);
}