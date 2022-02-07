TEXTURE2D(_PostMap);
SAMPLER(sampler_linear_clamp);

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;

    // if (vertexID == 0)
    output.positionCS = float4(-1, -1, 0, 1);
    output.screenUV = float2(0, 0);

    if (vertexID == 1)
    {
        output.positionCS = float4(-1, +3, 0, 1);
        output.screenUV = float2(0, 2);
    }

    if (vertexID == 2)
    {
        output.positionCS = float4(+3, -1, 0, 1);
        output.screenUV = float2(2, 0);
    }

    // if flipped
    if (_ProjectionParams.x < 0.0) {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }

    return output;
}

float4 CopyPassFragment(Varyings input): SV_Target0
{
    float2 uv = input.screenUV;
    // return float4(input.screenUV.x, input.screenUV.y, 0, 1);
    return SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv);
}
