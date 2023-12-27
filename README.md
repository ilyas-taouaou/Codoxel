My first little project with Silk.NET
it's using OpenGL 4.6 with DSA, Shader Pipelines and Shader SPIRV Binaries
it's also using latest C# features
The shaders are compiled offline using Silk.NET shaderc with nuke build system
There is 2 projects in the solution, run the one called "_build" first and everytime you change the shaders there in _build/Resources/Shaders
after running it successfully, the compiled shader will be put in the resources of the main project
then you can run the main project "Codoxel"
