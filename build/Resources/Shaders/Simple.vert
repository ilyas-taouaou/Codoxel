#version 460 core

out gl_PerVertex
{
    vec4 gl_Position;
    float gl_PointSize;
    float gl_ClipDistance[];
};

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

layout (location = 0) out vec3 vColor;

layout (location = 0) uniform float uTime;

void main()
{
    vColor = aColor;
    gl_Position = vec4(aPosition * uTime, 1.0);
}