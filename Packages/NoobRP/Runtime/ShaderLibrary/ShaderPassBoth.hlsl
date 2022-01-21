#include "light.hlsl"
#include "Shadow.hlsl"

//-------------------------------------------------------------------------------------
// variable declaration
//-------------------------------------------------------------------------------------

struct AttributesMesh
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0:TEXCOORD;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
    float2 texCoord0 : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
};

//-------------------------------------------------------------------------------------
// properties declaration
//-------------------------------------------------------------------------------------

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)

float _LightIntencity;
float4 _MainTex_ST;
float4 _BaseColor;
float _SpecularPow;
float4 _SpecularColor;
float _CutOff;

CBUFFER_END

//-------------------------------------------------------------------------------------
// functions
//-------------------------------------------------------------------------------------

VaryingsMeshToPS Vert(AttributesMesh inputMesh)
{
    VaryingsMeshToPS varyings;
    varyings.positionCS = TransformObjectToHClip(inputMesh.positionOS);
    varyings.texCoord0 = TRANSFORM_TEX(inputMesh.uv0, _MainTex);
    varyings.normalWS = TransformObjectToWorldNormal(inputMesh.normalOS, true);
    varyings.positionWS = TransformObjectToWorld(inputMesh.positionOS);
    return varyings;
}

float4 Frag(VaryingsMeshToPS input): SV_Target0
{
    DirectionalLight light = GetDirectionalLight();
    float3 lightWS = normalize(light.directionWS);

    // L(Luminance) : Radiance input
    float lightAttenuation = GetDirectionalShadowAttenuation(light.directionWS, input.positionWS);
    float3 Li = light.color * lightAttenuation;
    // E(Illuminance) : To simulate the Irradiance in BRDF
    float3 E = Li * saturate(dot(input.normalWS, lightWS)) * _LightIntencity;

    // Specular
    float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    float3 halfWS = normalize(viewWS + lightWS);
    float specularFactor = pow(max(0.0, dot(input.normalWS, halfWS)), _SpecularPow);
    float3 specular = specularFactor * _SpecularColor.rgb;

    // albedo : material surface color
    float4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texCoord0);
    float4 albedo = sample * _BaseColor;
    clip(albedo.a - _CutOff);
    // Resolve render equation in fake brdf
    float3 Lo = (albedo.xyz / PI + specular) * E;

    // Reflection
    float3 envLightDir = reflect(-viewWS, input.normalWS);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, envLightDir, 0.0);
    Lo += environment.xyz; // unity_SpecCube0 doesn't work well  

    return float4(Lo, 1);
}
