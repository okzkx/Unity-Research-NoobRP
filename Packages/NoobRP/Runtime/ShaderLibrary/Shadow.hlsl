#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

float4x4 _DirectionalShadowMatrices[4];
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

float GetDirectionalShadowAttenuation(float3 lightDirWS, float3 positionWS)
{
    positionWS += lightDirWS * 0.05;
    float3 positionSTS = mul(_DirectionalShadowMatrices[0], float4(positionWS, 1.0)).xyz;
  return  SAMPLE_TEXTURE2D_SHADOW( _DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS );
}

#endif
