float4 _BufferSize;

struct AttributesMesh
{
    float3 positionOS : POSITION;
    float3 positionOSLast : TEXCOORD4;
};

struct VaryingsMeshToPS
{
    float4 positionCS : SV_POSITION;
    float4 positionCSLast : TEXCOORD0;
};

float4x4 _MLast;
float4x4 _VPLast;

float4 TransformObjectToHClipLast(float3 positionOSLast)
{
    // Bug : UNITY_PREV_MATRIX_M doesn't work, so I can't generate motion object's motion vector
    // return mul(_VPLast, mul(UNITY_PREV_MATRIX_M, float4(positionOSLast, 1.0)));
    return mul(_VPLast, mul(UNITY_MATRIX_M, float4(positionOSLast, 1.0)));
}

VaryingsMeshToPS Vertex(AttributesMesh input)
{
    VaryingsMeshToPS output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    // output.positionCSLast = TransformObjectToHClipLast(input.positionOSLast);
    output.positionCSLast = TransformObjectToHClipLast(input.positionOS);

    return output;
}

float4 Fragment(VaryingsMeshToPS input) : SV_Target0
{
    float2 screenUV = input.positionCS.xy * _BufferSize.xy;
    float4 positionNDCLast = input.positionCSLast / input.positionCSLast.w;
    float2 screenUVLast = positionNDCLast.xy / 2 + float2(0.5, 0.5);

    #if UNITY_UV_STARTS_AT_TOP
    screenUVLast.y = 1.0 - screenUVLast.y;
    #endif

    float2 motionVector = screenUV - screenUVLast;
    // return Debug(screenUV - screenUVLast) * 100;
    return float4(motionVector, 0, 1);
}
