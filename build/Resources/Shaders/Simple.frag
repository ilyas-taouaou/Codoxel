#version 460 core

layout (location = 0) out vec4 out_color;

layout (location = 0) in vec2 vUV;

layout (location = 0) uniform sampler2D uTexture;

void main()
{
    out_color = texture(uTexture, vUV);
}