//-------------------------------------------------------------------------------------
// variable declaration
//-------------------------------------------------------------------------------------

struct AttributesMesh
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0:TEXCOORD;
    float4 color:COLOR;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
    float2 texCoord0 : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float4 color : TEXCOORD3;
};

//-------------------------------------------------------------------------------------
// properties declaration
//-------------------------------------------------------------------------------------

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

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
    varyings.texCoord0 = TRANSFORM_TEX(inputMesh.uv0, _BaseMap);
    varyings.normalWS = TransformObjectToWorldNormal(inputMesh.normalOS, true);
    varyings.positionWS = TransformObjectToWorld(inputMesh.positionOS);
    varyings.color = inputMesh.color;
    return varyings;
}

float4 Frag(VaryingsMeshToPS input): SV_Target0
{
    float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texCoord0) * _BaseColor * input.color;
    return albedo;

    //  SimpleLight simpleLight = GetSimpleLight();
    //  float3 lightWS = normalize(simpleLight.directionWS);
    //
    //  // L(Luminance) : Radiance input
    //  float3 Li = simpleLight.color;
    //  // E(Illuminance) : To simulate the Irradiance in BRDF
    //  float3 E = Li * saturate(dot(input.normalWS, lightWS)) * _LightIntencity;
    //
    //  #if defined(_DIFFUSE_HALF_LAMBERT)
    //      E = E * 0.5 + 0.5;
    //  #endif
    //
    //  float3 specular = 0;
    //
    //  // Specular
    //  #if !defined(_SPECULAR_NONE)
    //      float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    //      float specularFactor = 0;
    //  #if defined(_SPECULAR_PHONE)
    //      float3 reflectWS = reflect(-lightWS, input.normalWS);
    //      specularFactor = pow(max(0.0, dot(reflectWS, viewWS)), _SpecularPow);
    //  #else // Defined _SPECULAR_BLING_PHONE
    //      float3 halfWS = normalize(viewWS + lightWS);
    //      specularFactor = pow(max(0.0, dot(input.normalWS, halfWS)), _SpecularPow);
    //  #endif
    //      specular = specularFactor * _SpecularColor.rgb;
    //  #endif
    //
    //  // albedo : material surface color
    //  float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texCoord0).rgb * _BaseColor.rgb;
    //  // Resolve render equation in fake brdf
    //  float3 Lo = (albedo / PI + specular) * E;
    //
    //  return float4(Lo, 1);
}
