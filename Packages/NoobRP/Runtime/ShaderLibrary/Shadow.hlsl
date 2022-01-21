#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define SPLIT_COUNT 4
#define SHADOW_BIAS 0.2
#define TENT_SAMEPLE_COUNT 16

float4x4 _DirectionalShadowMatrices[SPLIT_COUNT];
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

float4 _CullingSpheres[SPLIT_COUNT];


float GetDirectionalShadowAttenuation(float3 lightDirWS, float3 positionWS)
{
    int index;
    for (index = 0; index < SPLIT_COUNT - 1; index++)
    {
        float4 sphere = _CullingSpheres[index];
        float dictance = distance(positionWS, sphere.xyz);
        if (dictance < sphere.w)
        {
            break;
        }
    }

    positionWS += lightDirWS * SHADOW_BIAS;
    float3 positionSTS = mul(_DirectionalShadowMatrices[index], float4(positionWS, 1.0)).xyz;

    #if defined(HARD_SHADOW)
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
    #else

    float4 size = float4(1 / (float)1024, 1 / (float)1024, 1024, 1024);

    float weights[TENT_SAMEPLE_COUNT];
    float2 positions[TENT_SAMEPLE_COUNT];

    SampleShadow_ComputeSamples_Tent_7x7(size, positionSTS.xy, weights, positions);

    float shadow = 0;
    for (int i = 0; i < TENT_SAMEPLE_COUNT; i++)
    {
        float3 coord3 = float3(positions[i], positionSTS.z);
        float value = SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, coord3);
        shadow += weights[i] * value;
    }
    return shadow;
    #endif
}

#endif
