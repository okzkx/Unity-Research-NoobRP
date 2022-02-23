#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define SPLIT_COUNT 4
#define SHADOW_BIAS 0.2
#define TENT_SAMEPLE_COUNT 16

float4x4 _DirectionalShadowMatrices[SPLIT_COUNT];
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_point_clamp
SAMPLER_CMP(SHADOW_SAMPLER);

float4 _CullingSpheres[SPLIT_COUNT];


float SampleShadowAuttenuation(Texture2D atlas, float3 positionSTS)
{
    #if defined(HARD_SHADOW)
    return SAMPLE_TEXTURE2D_SHADOW(atlas, SHADOW_SAMPLER, positionSTS);
    #else

    float4 size = float4(1 / (float)1024, 1 / (float)1024, 1024, 1024);

    float weights[TENT_SAMEPLE_COUNT];
    float2 positions[TENT_SAMEPLE_COUNT];

    SampleShadow_ComputeSamples_Tent_7x7(size, positionSTS.xy, weights, positions);

    float shadow = 0;
    for (int i = 0; i < TENT_SAMEPLE_COUNT; i++)
    {
        float3 coord3 = float3(positions[i], positionSTS.z);
        float value = SAMPLE_TEXTURE2D_SHADOW(atlas, SHADOW_SAMPLER, coord3);
        shadow += weights[i] * value;
    }
    return saturate(shadow) ;
    #endif
}

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
    return SampleShadowAuttenuation(_DirectionalShadowAtlas, positionSTS);
}

float4x4 _WorldToShadowMapCoordMatrices[16];

TEXTURE2D_SHADOW(_SpotPointShadowAtlas);

float GetSpotShadowAttenuation(int index, float3 positionWS, float3 lightDir)
{
    positionWS += lightDir * SHADOW_BIAS;
    float4 positionSTS = mul(_WorldToShadowMapCoordMatrices[index], float4(positionWS, 1.0));
    positionSTS = positionSTS / positionSTS.w;
    // -1 ~ 1 => -0.5 ~ 0.5
    // -0.5 ~ 0.5 => 0 ~ 0.25
    positionSTS.xy = (positionSTS.xy * 0.5 + 0.5) * 0.25 + float2(0.25 * index, 0);
    positionSTS.z = positionSTS.z * 0.5 + 0.5;

    return SampleShadowAuttenuation(_SpotPointShadowAtlas, positionSTS);
}

float GetPointShadowAttenuation(int pointLightIndex, float3 positionWS, float3 lightDir)
{
    float3 lightToPosDir = -lightDir;
    int faceIndex = 0;
    float absX = abs(lightToPosDir.x);
    float absY = abs(lightToPosDir.y);
    float absZ = abs(lightToPosDir.z);

    if (absX >= absY && absX >= absZ)
    {
        if (lightToPosDir.x >= 0)
        {
            faceIndex = 0;
        }
        else
        {
            faceIndex = 1;
        }
    }

    if (absY >= absX && absY >= absZ)
    {
        if (lightToPosDir.y >= 0)
        {
            faceIndex = 2;
        }
        else
        {
            faceIndex = 3;
        }
    }

    if (absZ >= absX && absZ >= absY)
    {
        if (lightToPosDir.z >= 0)
        {
            faceIndex = 4;
        }
        else
        {
            faceIndex = 5;
        }
    }

    positionWS += lightDir * SHADOW_BIAS;
    int tileIndex = 4 + pointLightIndex * 6 + faceIndex;
    float4 positionSTS = mul(_WorldToShadowMapCoordMatrices[tileIndex], float4(positionWS, 1.0));
    positionSTS = positionSTS / positionSTS.w;

    int colIndex = tileIndex % 4;
    int rowIndex = tileIndex / 4;

    // -1 ~ 1 => -0.5 ~ 0.5
    // -0.5 ~ 0.5 => 0 ~ 0.25
    positionSTS.xy = (positionSTS.xy * 0.5 + 0.5) * 0.25 + float2(0.25 * colIndex, 0.25 * rowIndex);
    positionSTS.z = positionSTS.z * 0.5 + 0.5;

    return SampleShadowAuttenuation(_SpotPointShadowAtlas, positionSTS);
}

#endif
