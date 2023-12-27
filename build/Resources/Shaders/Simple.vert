#version 460 core

out gl_PerVertex
{
    vec4 gl_Position;
    float gl_PointSize;
    float gl_ClipDistance[];
};

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aUV;

layout (location = 0) out vec2 vUV;

layout (location = 0) uniform float uTime;

void main()
{
    vUV = aUV;
    gl_Position = vec4(aPosition * uTime, 1.0);
}