TEXTURE2D(_PostMap);
SAMPLER(sampler_linear_clamp);

float4 _PostMap_TexelSize;

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
    if (_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }

    return output;
}

float4 SamplePostMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv);
}

float4 CopyPassFragment(Varyings input): SV_Target0
{
    float2 uv = input.screenUV;
    // return float4(input.screenUV.x, input.screenUV.y, 0, 1);
    // return SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv);
    return SamplePostMap(uv);
}

float4 _BloomThreshold;

float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);

    return color * contribution;
}

float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float2 uv = input.screenUV;
    float3 postMap = SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv).rgb;
    float3 color = ApplyBloomThreshold(postMap);
    return float4(color, 1.0);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };
    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * _PostMap_TexelSize.x;
        float2 uv = input.screenUV + float2(offset, 0.0);
        float3 postMap = SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv).rgb;
        color += postMap * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; i++)
    {
        float2 offset = float2(0, offsets[i] * _PostMap_TexelSize.y);
        float2 uv = input.screenUV + offset;
        float3 postMap = SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv).rgb;
        color += postMap * weights[i];
    }
    return float4(color, 1.0);
}

float _BloomIntensity;
TEXTURE2D(_PostMap2);

float4 BloomCombinePassFragment(Varyings input) : SV_TARGET
{
    float2 uv = input.screenUV;
    float3 postMap = SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv).rgb;
    float3 postMap2 = SAMPLE_TEXTURE2D(_PostMap2, sampler_linear_clamp, uv).rgb;

    return float4(postMap * _BloomIntensity + postMap2, 1.0);
}

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET {
    float2 uv = input.screenUV;
    float4 color = SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv);
    color.rgb = min(color.rgb, 60.0);
    color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
    return color;
}