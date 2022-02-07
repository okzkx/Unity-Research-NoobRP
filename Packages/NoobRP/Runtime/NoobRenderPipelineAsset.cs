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
    public Material postProcessMaterial;

    enum Pass {
        Copy,
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    private static readonly ShaderTagId NoobRPLightMode = new ShaderTagId("Both");

    static readonly int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

    static readonly int _SpotPointShadowAtlas = Shader.PropertyToID("_SpotPointShadowAtlas");
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    private int _PostMap = Shader.PropertyToID("_PostMap");


    public NoobRenderPipeline(NoobRenderPipelineAsset asset) {
        this.asset = asset;
        postProcessMaterial = CoreUtils.CreateEngineMaterial("NoobRP/PostProcess");
    }

    const string DIRECTIONAL_SHADOW_MAP = "Directional Light ShadowMap";
    const string SPOT_POINT_SHADOW_MAP = "Spot Point Light ShadowMap";
    private const string DRAW_RENDERERS = "RenderLoop.Clear";
    const int directionalLightCapacity = 1;
    const int spotLightCapacity = 4;
    const int pointLightCapacity = 2;

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
                                int index = ToPointLightIndex(_PointLightCount);

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

            cmb.BeginSample("ShadowMap");

            // Render Directianl Light ShadowMap
            // if (isGameCam) 
            {
                cmb.BeginSample(DIRECTIONAL_SHADOW_MAP);

                int rtWidth = 1024;
                cmb.GetTemporaryRT(dirShadowAtlasId, rtWidth, rtWidth,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
                cmb.SetRenderTarget(dirShadowAtlasId,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmb.ClearRenderTarget(true, false, Color.clear);

                int lightIndex = -1;

                NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
                for (int i = 0; i < visibleLights.Length; i++) {
                    if (visibleLights[i].lightType == LightType.Directional) {
                        lightIndex = i;
                    }
                }

                if (lightIndex >= 0 && cullingResults.GetShadowCasterBounds(lightIndex, out Bounds bounds)) {
                    int sideSplitCount = 2;
                    int splitCount = sideSplitCount * sideSplitCount;
                    int tileWidth = rtWidth / sideSplitCount;
                    float shadowNearPlaneOffset = 0.003f;
                    Matrix4x4[] dirShadowMatrices = new Matrix4x4[splitCount];
                    Vector4[] cullingSpheres = new Vector4[splitCount];
                    Vector3 splitRatio = new Vector3(0.25f, 0.5f, 0.75f);

                    for (int splitIndex = 0; splitIndex < splitCount; splitIndex++) {
                        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                            lightIndex, splitIndex, splitCount, splitRatio, tileWidth,
                            shadowNearPlaneOffset, out Matrix4x4 viewMatrix,
                            out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData
                        );


                        Vector2 offset = new Vector2(splitIndex % sideSplitCount, splitIndex / sideSplitCount);
                        Rect viewPort = new Rect(offset.x * tileWidth, offset.y * tileWidth, tileWidth, tileWidth);
                        cmb.SetViewport(viewPort);

                        dirShadowMatrices[splitIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, sideSplitCount);
                        // cmb.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
                        cmb.SetViewProjectionMatrices(viewMatrix, projMatrix);
                        ExcuteAndClearCommandBuffer(context, cmb);

                        cullingSpheres[splitIndex] = shadowSplitData.cullingSphere;

                        ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, lightIndex) {
                            splitData = shadowSplitData
                        };

                        context.DrawShadows(ref shadowDrawingSettings);
                    }

                    cmb.SetGlobalMatrixArray("_DirectionalShadowMatrices", dirShadowMatrices);
                    cmb.SetGlobalVectorArray("_CullingSpheres", cullingSpheres);
                }

                cmb.EndSample(DIRECTIONAL_SHADOW_MAP);

                ExcuteAndClearCommandBuffer(context, cmb);
            }

            // Render Spot and Point Light ShadowMap
            // if (false) 
            {
                cmb.BeginSample(SPOT_POINT_SHADOW_MAP);

                int rtWidth = 1024;
                cmb.GetTemporaryRT(_SpotPointShadowAtlas, rtWidth, rtWidth,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
                cmb.SetRenderTarget(_SpotPointShadowAtlas,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmb.ClearRenderTarget(true, false, Color.clear);

                int sideSplitCount = 4;
                int splitCount = sideSplitCount * sideSplitCount;
                int tileWidth = rtWidth / sideSplitCount;

                int spotLightCount = 0;
                int pointLightCount = 0;

                Matrix4x4[] _WorldToShadowMapCoordMatrices = new Matrix4x4[16];

                NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
                for (int lightIndex = 0; lightIndex < visibleLights.Length; lightIndex++) {
                    if (!cullingResults.GetShadowCasterBounds(lightIndex, out Bounds outBounds)) {
                        continue;
                    }

                    if (visibleLights[lightIndex].lightType == LightType.Spot) {
                        if (spotLightCount < spotLightCapacity) {
                            cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(lightIndex,
                                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData);

                            Rect viewPort = GetSpotShadowMapViewport(spotLightCount, sideSplitCount, tileWidth);
                            Matrix4x4 worldToShadowMapCoordMatrix = CreateWorldToShadowMapCoordMatrix(viewMatrix, projMatrix, viewPort);
                            _WorldToShadowMapCoordMatrices[spotLightCount] = worldToShadowMapCoordMatrix;

                            cmb.SetViewProjectionMatrices(viewMatrix, projMatrix);
                            cmb.SetViewport(viewPort);
                            ExcuteAndClearCommandBuffer(context, cmb);
                            ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, lightIndex) {
                                splitData = shadowSplitData
                            };

                            context.DrawShadows(ref shadowDrawingSettings);
                            spotLightCount++;
                        }
                    }

                    if (visibleLights[lightIndex].lightType == LightType.Point) {
                        if (pointLightCount < pointLightCapacity) {
                            const int faceCount = 6;
                            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++) {
                                cullingResults.ComputePointShadowMatricesAndCullingPrimitives(lightIndex, (CubemapFace) faceIndex,
                                    0, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData);

                                int tileIndex = spotLightCapacity + pointLightCount * faceCount + faceIndex;
                                Rect viewPort = GetSpotShadowMapViewport(tileIndex, sideSplitCount, tileWidth);
                                Matrix4x4 worldToShadowMapCoordMatrix = CreateWorldToShadowMapCoordMatrix(viewMatrix, projMatrix, viewPort);
                                _WorldToShadowMapCoordMatrices[tileIndex] = worldToShadowMapCoordMatrix;

                                cmb.SetViewProjectionMatrices(viewMatrix, projMatrix);
                                cmb.SetViewport(viewPort);
                                ExcuteAndClearCommandBuffer(context, cmb);
                                ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, lightIndex) {
                                    splitData = shadowSplitData
                                };

                                context.DrawShadows(ref shadowDrawingSettings);
                            }

                            pointLightCount++;
                        }
                    }
                }

                cmb.SetGlobalMatrixArray("_WorldToShadowMapCoordMatrices", _WorldToShadowMapCoordMatrices);

                cmb.EndSample(SPOT_POINT_SHADOW_MAP);

                ExcuteAndClearCommandBuffer(context, cmb);
            }

            cmb.EndSample("ShadowMap");
        }

        // Render Renderers
        // if (false) 
        {
            cmb.BeginSample(DRAW_RENDERERS);

            // Set up shader properties
            context.SetupCameraProperties(camera);

            cmb.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear, RenderTextureFormat.Default
            );
            cmb.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );

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
            cmb.EndSample(DRAW_RENDERERS);
        }

        {
#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos()) {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            }
#endif


            cmb.BeginSample("Post-Process");
            // cmb.Blit(frameBufferId, BuiltinRenderTextureType.CameraTarget);

            cmb.SetGlobalTexture(_PostMap, frameBufferId);
            cmb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) Pass.Copy, MeshTopology.Triangles, 3);

            cmb.EndSample("Post-Process");

#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos()) {
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
#endif
            ExcuteAndClearCommandBuffer(context, cmb);
        }

        EndRender(context, cmb);
    }

    private Matrix4x4 CreateWorldToShadowMapCoordMatrix(Matrix4x4 viewMatrix, Matrix4x4 projMatrix, Rect viewPort) {
        Matrix4x4 vp = projMatrix * viewMatrix;

        if (SystemInfo.usesReversedZBuffer) {
            vp.SetRow(2, -vp.GetRow(2));
        }

        // Vector2 position = viewPort.position / 1024;
        // Vector2 side = new Vector2(viewPort.width, viewPort.height)  / 1024;
        //
        // float3 scale = math.float3(0.5f * side.x, 0.5f * side.y, 1);
        // Matrix4x4 m = Matrix4x4.Scale(scale) * vp;
        // m = Matrix4x4.Translate(math.float3(scale.x + position.x, scale.y + position.y, 0)) * m;
        //
        // return m;

        return vp;
    }

    private Rect GetSpotShadowMapViewport(int i, int sideSplitCount, int tileWidth) {
        int rowIndex = i / sideSplitCount;
        int colIndex = i % sideSplitCount;

        return new Rect(colIndex * tileWidth, rowIndex * tileWidth, tileWidth, tileWidth);
    }

    private static void ExcuteAndClearCommandBuffer(ScriptableRenderContext context, CommandBuffer cmb) {
        context.ExecuteCommandBuffer(cmb);
        cmb.Clear();
    }

    private static void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(dirShadowAtlasId);
        cmb.ReleaseTemporaryRT(_SpotPointShadowAtlas);
        cmb.ReleaseTemporaryRT(frameBufferId);
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

    int ToPointLightIndex(int index) {
        return index + spotLightCapacity;
    }
}