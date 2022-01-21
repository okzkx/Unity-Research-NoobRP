#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define SPLIT_COUNT 4
#define SHADOW_BIAS 0.05

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
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

#endif
