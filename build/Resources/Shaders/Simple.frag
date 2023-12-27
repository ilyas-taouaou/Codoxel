#version 460 core

layout (location = 0) out vec4 out_color;

layout (location = 0) in vec3 vColor;

void main()
{
    out_color = vec4(vColor, 1.0);
}