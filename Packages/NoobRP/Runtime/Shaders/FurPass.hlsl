#ifndef FurManyPass
#define FurManyPass

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float4 normal: NORMAL;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
};

// SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_linear_repeat);

TEXTURE2D(_MainTex);
TEXTURE2D(_NoiseTex);

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float _FurFactor;
float _FurLayer;
CBUFFER_END

v2f vert(appdata v)
{
    v2f o;
    v.vertex += FURLAYER * v.normal * _FurLayer;
    o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

float4 frag(v2f i) : SV_Target
{
    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_linear_repeat, i.uv);
    float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_linear_repeat, i.uv).r;

    clip(noise - FURLAYER * _FurFactor);

    return col;
}

#endif // FurManyPass
