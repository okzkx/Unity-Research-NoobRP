using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Noob Render Pipeline Asset")]
public class NoobRenderPipelineAsset : RenderPipelineAsset {
    public float maxShadowDistance = 100;
    public bool enablePostProcess => bloom != null && bloom.intensity > 0;

    [Serializable]
    public class BloomSettings {
        [Min(0f)] public float threshold = 0.5f;
        [Range(0f, 1f)] public float thresholdKnee = 0.5f;
        [Min(0f)] public float intensity = 1;
    }

    [Serializable]
    public class ColorAdjustments {
        public float postExposure = 0;
        [Range(-100f, 100f)] public float contrast = 0;
        [ColorUsage(false, true)] public Color colorFilter = Color.white;
        [Range(-180f, 180f)] public float hueShift = 0;
        [Range(-100f, 100f)] public float saturation = 0;
    }

    [Serializable]
    public class WhiteBalance {
        [Range(-100f, 100f)] public float temperature;
        public float tint;
    }

    public BloomSettings bloom;
    public ColorAdjustments colorAdjustments;
    public WhiteBalance whiteBalance;

    [Range(0.5f, 2f)] public float renderScale;

    protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline(this);
    }
}

public class NoobRenderPipeline : RenderPipeline {
    private readonly NoobRenderPipelineAsset asset;
    public Material postProcessMaterial;

    enum Pass {
        Copy,
        BloomPrefilter,
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        TomeMapping,
        Final
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    readonly ShaderTagId NoobRPLightMode = new ShaderTagId("Both");
    readonly int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    readonly int _SpotPointShadowAtlas = Shader.PropertyToID("_SpotPointShadowAtlas");
    readonly int _CameraFrameBuffer = Shader.PropertyToID("_CameraFrameBuffer");
    readonly int _DepthBuffer = Shader.PropertyToID("_DepthBuffer");
    readonly int _ColorMap = Shader.PropertyToID("_ColorMap");
    readonly int _DepthMap = Shader.PropertyToID("_DepthMap");
    readonly int _PostMap = Shader.PropertyToID("_PostMap");
    readonly int _PostMap2 = Shader.PropertyToID("_PostMap2");
    readonly int _BloomPrefilter = Shader.PropertyToID("_BloomPrefilter");
    readonly int _BloomIntensity = Shader.PropertyToID("_BloomIntensity");
    readonly int _BloomResult = Shader.PropertyToID("_BloomResult");
    readonly int _ColorGradingLUT = Shader.PropertyToID("_ColorGradingLUT");

    public NoobRenderPipeline(NoobRenderPipelineAsset asset) {
        this.asset = asset;
        postProcessMaterial = CoreUtils.CreateEngineMaterial("NoobRP/PostProcess");
    }

    const string BLOOM = "Bloom";
    const string LUT = "LUT";
    const string BLOOM_PYRAMID = "_BloomPyramid";
    const string FINAL_BLIT = "Final Blit";
    const string DIRECTIONAL_SHADOW_MAP = "ShadowMap.Directional";
    const string SPOT_POINT_SHADOW_MAP = "ShadowMap.SpotPoint";
    const string DRAW_RENDERERS = "RenderLoop.Clear";
    const int directionalLightCapacity = 1;
    const int spotLightCapacity = 4;
    const int pointLightCapacity = 2;

    private void Render(ScriptableRenderContext context, Camera camera) {
        bool isGameCam = camera.cameraType == CameraType.Game;
        camera.allowHDR = true;
        CommandBuffer cmb = CommandBufferPool.Get();

        // Cullling
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
        scp.shadowDistance = Mathf.Min(asset.maxShadowDistance, camera.farClipPlane);
        var cullingResults = context.Cull(ref scp);

        // Draw Setting
        var sortingSettings = new SortingSettings(camera);
        var drawingSettings = new DrawingSettings(NoobRPLightMode, default);
        drawingSettings.perObjectData =
            PerObjectData.ReflectionProbes |
            PerObjectData.Lightmaps | PerObjectData.ShadowMask |
            PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.OcclusionProbeProxyVolume;

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
                cmb.GetTemporaryRT(_DirectionalShadowAtlas, rtWidth, rtWidth,
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
                cmb.SetRenderTarget(_DirectionalShadowAtlas,
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

        float renderScale = asset.renderScale;
        Vector2Int bufferSize = new Vector2Int((int) (camera.pixelWidth * renderScale), (int) (camera.pixelHeight * renderScale));

        // Render Renderers
        // if (false) 
        {
            // Set up shader properties
            context.SetupCameraProperties(camera);

            cmb.SetGlobalVector("_BufferSize", new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));
            
            cmb.GetTemporaryRT(_CameraFrameBuffer, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            cmb.GetTemporaryRT(_DepthBuffer, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            cmb.SetRenderTarget(_CameraFrameBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                _DepthBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmb.ClearRenderTarget(true, true, Color.clear);
            ExcuteAndClearCommandBuffer(context, cmb);

            // Draw opaque

            sortingSettings.criteria = SortingCriteria.CommonOpaque;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            context.DrawSkybox(camera);

            // Store Color and Depth map
            {
                cmb.GetTemporaryRT(_ColorMap, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                cmb.GetTemporaryRT(_DepthMap, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
                cmb.CopyTexture(_CameraFrameBuffer, _ColorMap);
                cmb.CopyTexture(_DepthBuffer, _DepthMap);
                ExcuteAndClearCommandBuffer(context, cmb);
            }

            // Draw transparent
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            ExcuteAndClearCommandBuffer(context, cmb);
        }


#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
#endif

        // Final Blit
        {
            // Without PostProcess
            if (!asset.enablePostProcess) {
                // Copy To Camera Target
                // cmb.Blit(frameBufferId, BuiltinRenderTextureType.CameraTarget);
                BlitTexture(cmb, _CameraFrameBuffer,
                    BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            } else

                // Enable PostProcess
            {
                // Bloom
                cmb.BeginSample(BLOOM);

                RenderTextureFormat renderTextureFormat = RenderTextureFormat.DefaultHDR;

                // Pre filter
                int width = bufferSize.x / 2;
                int height = bufferSize.y / 2;
                NoobRenderPipelineAsset.BloomSettings bloom = asset.bloom;
                {
                    int _BloomThreshold = Shader.PropertyToID("_BloomThreshold");

                    Vector4 threshold;
                    threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
                    threshold.y = threshold.x * bloom.thresholdKnee;
                    threshold.z = 2f * threshold.y;
                    threshold.w = 0.25f / (threshold.y + 0.00001f);
                    threshold.y -= threshold.x;
                    cmb.SetGlobalVector(_BloomThreshold, threshold);

                    cmb.GetTemporaryRT(_BloomPrefilter, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                    BlitTexture(cmb, _CameraFrameBuffer, _BloomPrefilter, Pass.BloomPrefilter);
                }

                // Bloom Pyramid
                {
                    int GetPyramidShaderID(int id) {
                        return Shader.PropertyToID(BLOOM_PYRAMID + id);
                    }

                    width /= 2;
                    height /= 2;
                    int bloomMaxIterations = 4;
                    int bloomScaleLimit = 2;

                    // Pyramid Generate
                    {
                        int from = _BloomPrefilter;
                        int i = 0;
                        for (; i < bloomMaxIterations; i++) {
                            if (height < bloomScaleLimit || width < bloomScaleLimit) {
                                break;
                            }

                            int to = Shader.PropertyToID(BLOOM_PYRAMID + (i * 2 + 1));
                            int intermidiate = Shader.PropertyToID(BLOOM_PYRAMID + i * 2);

                            cmb.GetTemporaryRT(intermidiate, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                            cmb.GetTemporaryRT(to, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                            BlitTexture(cmb, from, intermidiate, Pass.BloomHorizontal);
                            BlitTexture(cmb, intermidiate, to, Pass.BloomVertical);

                            from = to;
                            width /= 2;
                            height /= 2;
                        }
                    }

                    // Pyramid Combine
                    {
                        int c1Id = bloomMaxIterations * 2 - 1;
                        int c2Id = c1Id - 2;
                        int c3Id = c2Id - 1;

                        cmb.SetGlobalFloat(_BloomIntensity, bloom.intensity);

                        while (c1Id > 0) {
                            int c1 = GetPyramidShaderID(c1Id);
                            int c2 = GetPyramidShaderID(c2Id);
                            int c3 = GetPyramidShaderID(c3Id);

                            cmb.SetGlobalTexture(_PostMap, c1);
                            cmb.SetGlobalTexture(_PostMap2, c2);
                            cmb.SetRenderTarget(c3, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                            cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) Pass.BloomCombine, MeshTopology.Triangles, 3);

                            c1Id = c3Id;
                            c2Id = c1Id - 1;
                            c3Id = c2Id - 1;
                        }
                    }

                    cmb.GetTemporaryRT(_BloomResult, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, renderTextureFormat);
                    CombineTexture(cmb, GetPyramidShaderID(0), _CameraFrameBuffer, _BloomResult);

                    for (int i = 0; i < bloomMaxIterations * 2; i++) {
                        cmb.ReleaseTemporaryRT(GetPyramidShaderID(i));
                    }
                }

                cmb.EndSample(BLOOM);

                int colorLUTresolution = 32;
                int lutHeight = colorLUTresolution;
                int lutWidth = lutHeight * lutHeight;

                // Color Grading data prepare
                {
                    cmb.BeginSample(LUT);

                    // Color Adjectments
                    {
                        var colorAdjustments = asset.colorAdjustments;
                        cmb.SetGlobalVector("_ColorAdjustments", new Vector4(
                            Mathf.Pow(2f, colorAdjustments.postExposure),
                            colorAdjustments.contrast * 0.01f + 1f,
                            colorAdjustments.hueShift * (1f / 360f),
                            colorAdjustments.saturation * 0.01f + 1f
                        ));
                        cmb.SetGlobalVector("_ColorFilter", colorAdjustments.colorFilter.linear);
                    }

                    // White Balance
                    {
                        var whiteBalance = asset.whiteBalance;
                        cmb.SetGlobalVector("_WhiteBalance",
                            ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint)
                        );
                    }

                    // TODO: SplitToning
                    // TODO: Channel Mixer
                    // TODO: Shadow Midtones Highlights

                    // Render LUT with Color Grading and Toon mapping
                    cmb.GetTemporaryRT(_ColorGradingLUT, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                    Vector4 colorGradingLUTParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f));
                    cmb.SetGlobalVector("_ColorGradingLUTParameters", colorGradingLUTParameters);
                    // no mater what's input, aim to generate _ColorGradingLUT
                    BlitTexture(cmb, _BloomResult, _ColorGradingLUT, Pass.TomeMapping);

                    cmb.EndSample(LUT);
                }

                // Final Blit
                {
                    cmb.BeginSample(FINAL_BLIT);

                    // Blit Bloom Result to Camera target with LUT
                    cmb.SetGlobalVector("_LUTScaleOffset", new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1));
                    BlitTexture(cmb, _BloomResult, BuiltinRenderTextureType.CameraTarget, Pass.Final, camera.pixelRect);

                    cmb.EndSample(FINAL_BLIT);
                }
            }

            ExcuteAndClearCommandBuffer(context, cmb);
        }

#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif

        EndRender(context, cmb);
    }

    private void BlitTexture(CommandBuffer cmb, RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass, Rect? viewPortRect = null) {
        cmb.SetGlobalTexture(_PostMap, from);
        cmb.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        if (viewPortRect != null) {
            cmb.SetViewport(viewPortRect.Value);
        }

        cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) pass, MeshTopology.Triangles, 3);
    }

    private void CombineTexture(CommandBuffer cmb, RenderTargetIdentifier rt1, RenderTargetIdentifier rt2, RenderTargetIdentifier rt3) {
        cmb.SetGlobalTexture(_PostMap, rt1);
        cmb.SetGlobalTexture(_PostMap2, rt2);
        cmb.SetRenderTarget(rt3, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) Pass.BloomCombine, MeshTopology.Triangles, 3);
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

    private void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_DirectionalShadowAtlas);
        cmb.ReleaseTemporaryRT(_SpotPointShadowAtlas);
        cmb.ReleaseTemporaryRT(_CameraFrameBuffer);
        cmb.ReleaseTemporaryRT(_DepthBuffer);
        cmb.ReleaseTemporaryRT(_ColorMap);
        cmb.ReleaseTemporaryRT(_DepthMap);
        cmb.ReleaseTemporaryRT(_PostMap);
        cmb.ReleaseTemporaryRT(_PostMap2);
        cmb.ReleaseTemporaryRT(_BloomPrefilter);
        cmb.ReleaseTemporaryRT(_BloomResult);
        cmb.ReleaseTemporaryRT(_ColorGradingLUT);
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