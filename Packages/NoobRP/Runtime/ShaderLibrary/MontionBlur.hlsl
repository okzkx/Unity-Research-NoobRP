float4 _BufferSize;

struct AttributesMesh
{
    float3 positionOS : POSITION;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
};

VaryingsMeshToPS Vertex(AttributesMesh input)
{
    VaryingsMeshToPS output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    return output;
}

TEXTURE2D(_LastTexture);
TEXTURE2D(_TextureInput);

SAMPLER(sampler_point_clamp);
SAMPLER(sampler_linear_clamp);

float4 Fragment(VaryingsMeshToPS input) : SV_Target0
{
    float2 screenUV = input.positionCS.xy * _BufferSize.xy;
    float3 frameBuffer = SAMPLE_TEXTURE2D(_TextureInput, sampler_linear_clamp, screenUV).rgb;
    float3 frameBufferLast = SAMPLE_TEXTURE2D(_LastTexture, sampler_linear_clamp, screenUV).rgb;
    float3 color = lerp(frameBuffer, frameBufferLast, 0.9);
    return Debug(color);
}
