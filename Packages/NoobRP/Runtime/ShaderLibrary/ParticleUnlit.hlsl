﻿//-------------------------------------------------------------------------------------
// variable declaration
//-------------------------------------------------------------------------------------

struct AttributesMesh
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 uv0:TEXCOORD;
    float4 color:COLOR;
    float animBlend : TEXCOORD1;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
    float4 texCoord0 : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float4 color : TEXCOORD3;
    float animBlend : TEXCOORD4;
};

//-------------------------------------------------------------------------------------
// properties declaration
//-------------------------------------------------------------------------------------

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DepthMap);
TEXTURE2D(_ColorMap);

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

CBUFFER_START(UnityPerMaterial)

float _LightIntencity;
float4 _BaseMap_ST;
float4 _BaseColor;
float _SpecularPow;
float4 _SpecularColor;

CBUFFER_END

//-------------------------------------------------------------------------------------
// functions
//-------------------------------------------------------------------------------------

VaryingsMeshToPS Vert(AttributesMesh inputMesh)
{
    VaryingsMeshToPS varyings;
    varyings.positionCS = TransformObjectToHClip(inputMesh.positionOS);
    varyings.texCoord0 = inputMesh.uv0;
    varyings.normalWS = TransformObjectToWorldNormal(inputMesh.normalOS, true);
    varyings.positionWS = TransformObjectToWorld(inputMesh.positionOS);
    varyings.color = inputMesh.color;
    varyings.animBlend = inputMesh.animBlend;
    return varyings;
}

float4 Frag(VaryingsMeshToPS input): SV_Target0
{
    // Color
    float4 baseColor = _BaseColor;
    float4 meshColor = input.color;
    float4 baseMap1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texCoord0.xy);
    float4 baseMap2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texCoord0.zw);
    float4 baseMap = lerp(baseMap1, baseMap2, input.animBlend);
    float4 color = baseMap * baseColor * meshColor;

    // Depth attenuation fade
    float2 uvSS = input.positionCS.xy / _ScreenParams.xy;
    float depth = input.positionCS.w;
    float depthMap = SAMPLE_DEPTH_TEXTURE_LOD(_DepthMap, sampler_point_clamp, uvSS, 0);
    float backGroundDepth = LinearEyeDepth(depthMap, _ZBufferParams);
    float depthDelta = backGroundDepth - depth;
    float depthAttenuation = saturate(depthDelta / 1.5);
    color.a *= depthAttenuation;
    
    float3 colorMap = SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_linear_clamp, uvSS, 0);
    
    return color;
}
