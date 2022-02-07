#include "light.hlsl"
#include "Shadow.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

//-------------------------------------------------------------------------------------
// variable declaration
//-------------------------------------------------------------------------------------

struct AttributesMesh
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0:TEXCOORD;
    float4 tangentOS : TANGENT;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
    float2 texCoord0 : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
};

//-------------------------------------------------------------------------------------
// properties declaration
//-------------------------------------------------------------------------------------

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

CBUFFER_START(UnityPerMaterial)

float4 _BaseMap_ST;

float4 _EmissionColor;
float4 _BaseColor;
float _SpecularPow;
float _CutOff;
float _Roughness;
float _EnvWeight;
float _Metallic; // Fresnel effect
float _Occlusion;
float _Smoothness; // High Light range
float _Emission;
float _Fresnel;

CBUFFER_END

//-------------------------------------------------------------------------------------
// functions
//-------------------------------------------------------------------------------------

real PerceptualRoughnessToMipmapLevel(real perceptualRoughness)
{
    perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);

    return perceptualRoughness * 6;
}

float3 DecodeNormal(float4 sample, float scale)
{
    #if defined(UNITY_NO_DXT5nm)
    return UnpackNormalRGB(sample, scale);
    #else
    return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

VaryingsMeshToPS Vert(AttributesMesh inputMesh)
{
    VaryingsMeshToPS varyings;
    varyings.positionCS = TransformObjectToHClip(inputMesh.positionOS);
    varyings.texCoord0 = TRANSFORM_TEX(inputMesh.uv0, _BaseMap);
    varyings.normalWS = TransformObjectToWorldNormal(inputMesh.normalOS, true);
    varyings.positionWS = TransformObjectToWorld(inputMesh.positionOS);
    varyings.tangentWS = float4(TransformObjectToWorldDir(inputMesh.tangentOS), inputMesh.tangentOS.w);
    return varyings;
}

float4 Debug(float f)
{
    return float4(f,f,f, 1);
}

float4 Debug(float3 f)
{
    return float4(f, 1);
}

float4 Debug(float4 f)
{
    return f;
}

float4 Frag(VaryingsMeshToPS input): SV_Target0
{
    float2 uv = input.texCoord0;
    DirectionalLight light = GetDirectionalLight();
    float3 lightWS = normalize(light.directionWS);
    float4 maskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);

    float metallic = lerp(0.01, maskMap.r, _Metallic);
    float occlusion = lerp(0.01, maskMap.g, _Occlusion);
    float smoothness = lerp(0.01, maskMap.a, _Smoothness);

    float occlusionLightFactor = 1 - occlusion;

    float3x3 tangentToWorld = CreateTangentToWorld(input.normalWS, input.tangentWS.xyz, 1);
    float3 normalTS = DecodeNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), 1);
    float3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
    float3 position = input.positionWS;

    float3 Lo = 0;

    // Directional Light BRDF

    // L(Luminance) : Radiance input
    float lightAttenuation = GetDirectionalShadowAttenuation(light.directionWS, input.positionWS);
    float3 Li = light.color * lightAttenuation;
    // E(Illuminance) : To simulate the Irradiance in BRDF
    float3 E = Li * saturate(dot(normalWS, lightWS));

    // Specular
    float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    float3 halfWS = normalize(viewWS + lightWS);
    float specularPow = lerp(0, 32, smoothness);
    float specularFactor = pow(max(0.0, dot(normalWS, halfWS)), specularPow);
    float3 specular = specularFactor * light.color.rgb * smoothness;

    // albedo : material surface color
    float4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    float4 albedo = sample * _BaseColor;
    clip(albedo.a - _CutOff);

    // Fresnel
    float fresnelFactor = Pow4(1.0 - saturate(dot(normalWS, viewWS)));
    specular *= lerp(1, fresnelFactor, metallic);

    // Resolve render equation in fake brdf
    float3 DirectionalLo = (albedo.xyz / PI + specular) * E;
    Lo += DirectionalLo;

    // SpotLight

    for (int i = 0; i < _SpotLightCount; i++)
    {
        SpotLight spotLight = CreateSpotLight(i);
        float3 lightPosToFaceDir = normalize(position - spotLight.position);
        float3 lightDir = -lightPosToFaceDir;
        float shadowAttenuation = GetSpotShadowAttenuation(i, position, lightDir);
        float spotLightAttenuation = 1 - saturate( distance(position, spotLight.position) / spotLight.range);
        float3 lightColor = spotLight.color * spotLightAttenuation * shadowAttenuation;
        float minAngleCos = cos(spotLight.angle * 0.5); // * 0.5 is hack for shadowMap
        float angleCos = dot(lightPosToFaceDir, spotLight.direction);
        float lightArea = Smoothstep01(saturate(100 * (angleCos - minAngleCos)));
        float3 spotLightE =  saturate(dot(normalWS, lightDir)) * lightColor;

        float3 halfVL = normalize(viewWS + lightDir);
        float specularFactorSL = pow(max(0.0, dot(normalWS, halfVL)), specularPow);
        float3 specularSL = specularFactorSL * lightColor * smoothness;
        
        Lo += lightArea * (albedo.xyz / PI + specularSL) * spotLightE;
    }

    for (int i = 0; i < _PointLightCount; i++)
    {
        PointLight pointLight = CreatePointLight(i);
        float3 lightPosToFaceDir = normalize(position - pointLight.position);
        float3 lightDir = -lightPosToFaceDir;
        float shadowAttenuation = GetPointShadowAttenuation(i, position, lightDir);
        float pointLightAttenuation = 1 - saturate( distance(position, pointLight.position) / pointLight.range);
        float3 lightColor = pointLight.color * pointLightAttenuation * shadowAttenuation;
        float3 pointLightE =  saturate(dot(normalWS, lightDir)) * lightColor;

        float3 halfVL = normalize(viewWS + lightDir);
        float specularFactorSL = pow(saturate(dot(normalWS, halfVL)), specularPow);
        float3 specularSL = specularFactorSL * lightColor * smoothness;
        
        Lo += (albedo.xyz / PI + specularSL) * pointLightE;
    }

    // Environment Light

    // Reflection
    float3 envLightDir = reflect(-viewWS, normalWS);
    float roughness = lerp(lerp(1, 1 - fresnelFactor, metallic), 0, smoothness);
    real mipMapLevel = PerceptualRoughnessToMipmapLevel(roughness);
    float4 envLo = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, envLightDir, mipMapLevel);
    // Probe Bake mistake now

    // Emmision
    float4 emmisionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
    float3 emmision = lerp(0, emmisionMap, _Emission).rgb * _EmissionColor.rgb;

    float3 color = lerp(Lo, envLo, _EnvWeight) * occlusionLightFactor + emmision;

    return float4(color, 1);
}
