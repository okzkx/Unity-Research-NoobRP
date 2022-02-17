#include "PostProcessPasses.hlsl"

float4 SampleSourceMap(float2 uv)
{
    return SAMPLE_TEXTURE2D(_PostMap, sampler_linear_clamp, uv);
}

struct LumaNeighborhood
{
    float m, n, e, s, w, ne, se, sw, nw;
    float high, low, range;
};

float GetLuma(float2 uv, float2 offset)
{
    uv += offset * _PostMap_TexelSize.xy;
    float4 postMap = SampleSourceMap(uv);
    float luminance = Luminance(postMap.rgb);
    return luminance;
}

float4 _FXAAConfig;

bool CanSkipFXAA(LumaNeighborhood luma)
{
    return luma.range < max(_FXAAConfig.x, _FXAAConfig.y * luma.high);
}

LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood luma;
    luma.m = GetLuma(uv, float2(+0.0, +0.0));
    luma.n = GetLuma(uv, float2(+0.0, +1.0));
    luma.e = GetLuma(uv, float2(+1.0, +0.0));
    luma.s = GetLuma(uv, float2(+0.0, -1.0));
    luma.w = GetLuma(uv, float2(-1.0, +0.0));
    luma.ne = GetLuma(uv, float2(+1.0, +1.0));
    luma.se = GetLuma(uv, float2(+1.0, -1.0));
    luma.sw = GetLuma(uv, float2(-1.0, -1.0));
    luma.nw = GetLuma(uv, float2(-1.0, +1.0));

    luma.high = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.low = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.high - luma.low;
    return luma;
}

bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizontal =
        2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
        abs(luma.ne + luma.se - 2.0 * luma.e) +
        abs(luma.nw + luma.sw - 2.0 * luma.w);
    float vertical =
        2.0 * abs(luma.e + luma.w - 2.0 * luma.m) +
        abs(luma.ne + luma.nw - 2.0 * luma.n) +
        abs(luma.se + luma.sw - 2.0 * luma.s);
    return horizontal >= vertical;
}

struct FXAAEdge
{
    bool isHorizontal;
    float pixelStep;
    float lumaGradient, otherLuma;
};

FXAAEdge InitFXAAEdge(const LumaNeighborhood luma)
{
    FXAAEdge edge;
    edge.isHorizontal = IsHorizontalEdge(luma);
    float lumaP, lumaN;
    if (edge.isHorizontal)
    {
        edge.pixelStep = _PostMap_TexelSize.y;
        lumaP = luma.n;
        lumaN = luma.s;
    }
    else
    {
        edge.pixelStep = _PostMap_TexelSize.x;
        lumaP = luma.e;
        lumaN = luma.w;
    }
    float gradientP = abs(lumaP - luma.m);
    float gradientN = abs(lumaN - luma.m);

    if (gradientP < gradientN)
    {
        edge.pixelStep = -edge.pixelStep;
        edge.lumaGradient = gradientN;
        edge.otherLuma = lumaN;
    }
    else
    {
        edge.lumaGradient = gradientP;
        edge.otherLuma = lumaP;
    }

    return edge;
}

float GetEdgeBlendFactor(LumaNeighborhood luma)
{
    float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
    filter += luma.ne + luma.nw + luma.se + luma.sw;
    filter *= 1.0 / 12.0;
    filter = abs(filter - luma.m);
    filter = saturate(filter / luma.range);
    filter = smoothstep(0, 1, filter);
    return filter * filter * _FXAAConfig.z;
}

float4 FXAAFragment(Varyings input): SV_Target0
{
    float2 uv = input.screenUV;
    //  Get Lumas
    LumaNeighborhood lumaN = GetLumaNeighborhood(uv);
    // Skip low contrast
    if (CanSkipFXAA(lumaN))
    {
        return SampleSourceMap(uv);
    }

    // FXAA Edge
    FXAAEdge edge = InitFXAAEdge(lumaN);

    // Blend Factor
    float blendFactor = GetEdgeBlendFactor(lumaN);
    float2 blendUV = input.screenUV;
    if (edge.isHorizontal)
    {
        blendUV.y += blendFactor * edge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * edge.pixelStep;
    }

    return SampleSourceMap(blendUV);
}
