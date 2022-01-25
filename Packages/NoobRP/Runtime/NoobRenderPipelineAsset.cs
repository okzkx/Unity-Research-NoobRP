using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Noob Render Pipeline Asset")]
public class NoobRenderPipelineAsset : RenderPipelineAsset {
    public float maxShadowDistance = 100;

    protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline(this);
    }
}

public class NoobRenderPipeline : RenderPipeline {
    private readonly NoobRenderPipelineAsset asset;

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    private static readonly ShaderTagId NoobRPLightMode = new ShaderTagId("Both");

    static readonly int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static readonly int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

    public NoobRenderPipeline(NoobRenderPipelineAsset asset) {
        this.asset = asset;
    }

    const string PREPARE_DIRECTIONAL_CASCADE_SHADOW_MAP = "Directional Light ShadowMap";
    private const string DRAW_RENDERERS = "RenderLoop.Clear";
    const  int directionalLightCapacity = 1;
    const  int spotLightCapacity = 4;
    const    int pointLightCapacity = 2;

    private void Render(ScriptableRenderContext context, Camera camera) {
        bool isGameCam = camera.cameraType == CameraType.Game;

        CommandBuffer cmb = CommandBufferPool.Get();

        // Cullling
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
        scp.shadowDistance = Mathf.Min(asset.maxShadowDistance, camera.farClipPlane);
        var cullingResults = context.Cull(ref scp);

        // Draw Setting
        var sortingSettings = new SortingSettings(camera);
        var drawingSettings = new DrawingSettings(NoobRPLightMode, default);

        // Filter Setting
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);


        // Lighting Setting
        {
            // Lights Setup
            {

                int directionalLightCount = 0;
                int _SpotLightCount = 0;
                int _PointLightCount = 0;

                Color _DirectionalLightColor = Color.black;
                Vector4 _DirectionalLightDirection = Vector4.zero;

                Vector4[] _LightColors = new Vector4[spotLightCapacity + pointLightCapacity];
                Vector4[] _LightPositions = new Vector4[spotLightCapacity + pointLightCapacity];
                Vector4[] _LightDirections = new Vector4[spotLightCapacity + pointLightCapacity];

                NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
                foreach (var visibleLight in visibleLights) {
                    switch (visibleLight.lightType) {
                        case LightType.Spot:
                            if (_SpotLightCount < spotLightCapacity) {
                                int index = ToSpotLightIndex(_SpotLightCount);
                                
                                _LightColors[index] = visibleLight.finalColor;
                                
                                Vector4 direction = visibleLight.localToWorldMatrix.GetColumn(2);
                                direction.w = math.radians(visibleLight.spotAngle);
                                _LightDirections[index] = direction;
                                
                                Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                                position.w = visibleLight.range;
                                _LightPositions[index] = position;
                                
                                _SpotLightCount++;
                            }

                            break;
                        case LightType.Directional:
                            if (directionalLightCount < directionalLightCapacity) {
                                _DirectionalLightColor = visibleLight.finalColor;
                                _DirectionalLightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                                directionalLightCount++;
                            }

                            break;
                        case LightType.Point:
                            if (_PointLightCount < pointLightCapacity) {
                               int index=   ToPointLightIndex(_PointLightCount);
                                
                                _LightColors[index] = visibleLight.finalColor;
                                
                                Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                                position.w = visibleLight.range;
                                _LightPositions[index] = position;
                                
                                _PointLightCount++;
                            }

                            break;
                        case LightType.Area:
                            break;
                        case LightType.Disc:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                cmb.SetGlobalColor("_DirectionalLightColor", _DirectionalLightColor);
                cmb.SetGlobalVector("_DirectionalLightDirection", _DirectionalLightDirection);
                
                cmb.SetGlobalInt("_SpotLightCount", _SpotLightCount);
                cmb.SetGlobalInt("_PointLightCount", _PointLightCount);
                
                cmb.SetGlobalVectorArray("_LightPositions", _LightPositions);
                cmb.SetGlobalVectorArray("_LightColors", _LightColors);
                cmb.SetGlobalVectorArray("_LightDirections", _LightDirections);
                ExcuteAndClearCommandBuffer(context, cmb);
            }

            // Render Shadow map1
            // if (isGameCam) 
            {
                cmb.BeginSample(PREPARE_DIRECTIONAL_CASCADE_SHADOW_MAP);

                int rtSide = 1024;
                cmb.GetTemporaryRT(dirShadowAtlasId, rtSide, rtSide,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
                cmb.SetRenderTarget(dirShadowAtlasId,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmb.ClearRenderTarget(true, false, Color.clear);

                int activeLightIndex = 0;

                if (cullingResults.GetShadowCasterBounds(activeLightIndex, out Bounds bounds)) {
                    int sideSplitCount = 2;
                    int splitCount = sideSplitCount * sideSplitCount;
                    int shadowResolution = rtSide / sideSplitCount;
                    float shadowNearPlaneOffset = 0.003f;
                    Matrix4x4[] dirShadowMatrices = new Matrix4x4[splitCount];
                    Vector4[] cullingSpheres = new Vector4[splitCount];
                    Vector3 splitRatio = new Vector3(0.25f, 0.5f, 0.75f);

                    for (int splitIndex = 0; splitIndex < splitCount; splitIndex++) {
                        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                            activeLightIndex, splitIndex, splitCount, splitRatio, shadowResolution,
                            shadowNearPlaneOffset, out Matrix4x4 viewMatrix,
                            out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData
                        );

                        Vector2 offset = SetTileViewport(cmb, splitIndex, sideSplitCount, shadowResolution);

                        dirShadowMatrices[splitIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, sideSplitCount);
                        // cmb.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
                        cmb.SetViewProjectionMatrices(viewMatrix, projMatrix);
                        ExcuteAndClearCommandBuffer(context, cmb);

                        cullingSpheres[splitIndex] = shadowSplitData.cullingSphere;

                        ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, activeLightIndex);

                        shadowDrawingSettings.splitData = shadowSplitData;
                        context.DrawShadows(ref shadowDrawingSettings);
                    }

                    cmb.SetGlobalMatrixArray("_DirectionalShadowMatrices", dirShadowMatrices);
                    cmb.SetGlobalVectorArray("_CullingSpheres", cullingSpheres);
                }

                cmb.EndSample(PREPARE_DIRECTIONAL_CASCADE_SHADOW_MAP);

                ExcuteAndClearCommandBuffer(context, cmb);
            }
        }

        // Render Renderers
        // if (false) 
        {
            cmb.BeginSample(DRAW_RENDERERS);

            // Set up shader properties
            context.SetupCameraProperties(camera);

            cmb.ClearRenderTarget(true, true, Color.clear);

            cmb.EndSample(DRAW_RENDERERS);

            ExcuteAndClearCommandBuffer(context, cmb);

            // Draw opaque
            cmb.BeginSample(DRAW_RENDERERS);

            sortingSettings.criteria = SortingCriteria.CommonOpaque;
            drawingSettings.sortingSettings = sortingSettings;
            drawingSettings.perObjectData =
                PerObjectData.ReflectionProbes |
                PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume;
            filteringSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            context.DrawSkybox(camera);

            // Draw transparent
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            cmb.BeginSample(DRAW_RENDERERS);
        }

        EndRender(context, cmb);
    }

    static Vector2 SetTileViewport(CommandBuffer buffer, int index, int side, float tileSize) {
        Vector2Int offset = new Vector2Int(index % side, index / side);
        Rect viewPort = new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize);
        buffer.SetViewport(viewPort);
        return offset;
    }

    private static void ExcuteAndClearCommandBuffer(ScriptableRenderContext context, CommandBuffer cmb) {
        context.ExecuteCommandBuffer(cmb);
        cmb.Clear();
    }

    private static void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        context.Submit();
        cmb.Release();
    }

    static Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
        if (SystemInfo.usesReversedZBuffer) {
            m.SetRow(2, -m.GetRow(2));
        }

        m = Matrix4x4.Scale(math.float3(0.25f, 0.25f, 0.5f)) * m;
        m = Matrix4x4.Translate(math.float3(0.25f + 0.5f * offset.x, 0.25f + 0.5f * offset.y, 0.5f)) * m;

        return m;
    }

    int ToSpotLightIndex(int index) {
        return index;
    }

    int ToPointLightIndex(int index)
    {
        return index + spotLightCapacity;
    }

}