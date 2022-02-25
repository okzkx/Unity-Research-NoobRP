using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class LightStep : RenderStep {
    const int directionalLightCapacity = 1;
    const int spotLightCapacity = 4;
    const int pointLightCapacity = 2;
    const string DIRECTIONAL_SHADOW_MAP = "Directional light Shadow Mapping";
    const string SPOT_POINT_SHADOW_MAP = "Spot Point light Shadow Mapping";

    readonly int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    readonly int _SpotPointShadowAtlas = Shader.PropertyToID("_SpotPointShadowAtlas");
    readonly int _DirectionalLightColor = Shader.PropertyToID("_DirectionalLightColor");
    readonly int _DirectionalLightDirection = Shader.PropertyToID("_DirectionalLightDirection");
    readonly int _SpotLightCount = Shader.PropertyToID("_SpotLightCount");
    readonly int _PointLightCount = Shader.PropertyToID("_PointLightCount");
    readonly int _LightPositions = Shader.PropertyToID("_LightPositions");
    readonly int _LightColors = Shader.PropertyToID("_LightColors");
    readonly int _LightDirections = Shader.PropertyToID("_LightDirections");
    readonly int _DirectionalShadowMatrices = Shader.PropertyToID("_DirectionalShadowMatrices");
    readonly int _CullingSpheres = Shader.PropertyToID("_CullingSpheres");
    readonly int _WorldToShadowMapCoordMatrices = Shader.PropertyToID("_WorldToShadowMapCoordMatrices");


    public string stepName = "LightStep";
    NoobRenderPipeline noobRenderPipeline;
    int directionalLightShadowMapResolution = 1024;
    int spotPointLightShadowMapResolution = 1024;

    public LightStep(NoobRenderPipeline noobRenderPipeline) {
        this.noobRenderPipeline = noobRenderPipeline;
    }

    public void End(CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_DirectionalShadowAtlas);
        cmb.ReleaseTemporaryRT(_SpotPointShadowAtlas);
    }

    public void Excute(ref ScriptableRenderContext context, ref CullingResults cullingResults) {
        var cmb = CommandBufferPool.Get(stepName);

        // Lights Data setting
        {
            int directionalLightCount = 0;
            int spotLightCount = 0;
            int pointLightCount = 0;

            Color directionalLightColor = Color.black;
            Vector4 directionalLightDirection = Vector4.zero;

            Vector4[] lightColors = new Vector4[spotLightCapacity + pointLightCapacity];
            Vector4[] lightPositions = new Vector4[spotLightCapacity + pointLightCapacity];
            Vector4[] lightDirections = new Vector4[spotLightCapacity + pointLightCapacity];

            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            foreach (var visibleLight in visibleLights) {
                switch (visibleLight.lightType) {
                    case LightType.Spot:
                        if (spotLightCount < spotLightCapacity) {
                            int index = ToSpotLightIndex(spotLightCount);

                            lightColors[index] = visibleLight.finalColor;

                            Vector4 direction = visibleLight.localToWorldMatrix.GetColumn(2);
                            direction.w = math.radians(visibleLight.spotAngle);
                            lightDirections[index] = direction;

                            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                            position.w = visibleLight.range;
                            lightPositions[index] = position;

                            spotLightCount++;
                        }

                        break;
                    case LightType.Directional:
                        if (directionalLightCount < directionalLightCapacity) {
                            directionalLightColor = visibleLight.finalColor;
                            directionalLightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                            directionalLightCount++;
                        }

                        break;
                    case LightType.Point:
                        if (pointLightCount < pointLightCapacity) {
                            int index = ToPointLightIndex(pointLightCount);

                            lightColors[index] = visibleLight.finalColor;

                            Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
                            position.w = visibleLight.range;
                            lightPositions[index] = position;

                            pointLightCount++;
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

            cmb.SetGlobalColor(_DirectionalLightColor, directionalLightColor);
            cmb.SetGlobalVector(_DirectionalLightDirection, directionalLightDirection);

            cmb.SetGlobalInt(_SpotLightCount, spotLightCount);
            cmb.SetGlobalInt(_PointLightCount, pointLightCount);

            cmb.SetGlobalVectorArray(_LightPositions, lightPositions);
            cmb.SetGlobalVectorArray(_LightColors, lightColors);
            cmb.SetGlobalVectorArray(_LightDirections, lightDirections);
            ExcuteAndClearCommandBuffer(context, cmb);
        }

        // Direction light shadow map
        using (new ProfilingScope(cmb, new ProfilingSampler(DIRECTIONAL_SHADOW_MAP))) {
            cmb.GetTemporaryRT(_DirectionalShadowAtlas, directionalLightShadowMapResolution, directionalLightShadowMapResolution, 32, FilterMode.Point, RenderTextureFormat.Shadowmap);
            cmb.SetRenderTarget(_DirectionalShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmb.ClearRenderTarget(true, false, Color.clear);

            // Find directional light index
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
                int tileWidth = directionalLightShadowMapResolution / sideSplitCount;
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

                    Vector2Int offset = new Vector2Int(splitIndex % sideSplitCount, splitIndex / sideSplitCount);
                    Rect viewPort = new Rect(offset.x * tileWidth, offset.y * tileWidth, tileWidth, tileWidth);
                    cmb.SetViewport(viewPort);
                    dirShadowMatrices[splitIndex] = GetDirectionalLightCascadeShadowMatrix(viewMatrix, projMatrix, offset, sideSplitCount);
                    cmb.SetViewProjectionMatrices(viewMatrix, projMatrix);
                    ExcuteAndClearCommandBuffer(context, cmb);

                    cullingSpheres[splitIndex] = shadowSplitData.cullingSphere;

                    ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, lightIndex) {
                        splitData = shadowSplitData
                    };

                    context.DrawShadows(ref shadowDrawingSettings);
                }

                cmb.SetGlobalMatrixArray(_DirectionalShadowMatrices, dirShadowMatrices);
                cmb.SetGlobalVectorArray(_CullingSpheres, cullingSpheres);
            }

            ExcuteAndClearCommandBuffer(context, cmb);
        }

        // Render Spot and Point Light ShadowMap
        using (new ProfilingScope(cmb, new ProfilingSampler(SPOT_POINT_SHADOW_MAP))) {
            int width = spotPointLightShadowMapResolution;
            cmb.GetTemporaryRT(_SpotPointShadowAtlas, width, width,32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            cmb.SetRenderTarget(_SpotPointShadowAtlas,RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmb.ClearRenderTarget(true, false, Color.clear);

            int sideSplitCount = 4;
            int splitCount = sideSplitCount * sideSplitCount;
            int tileWidth = width / sideSplitCount;

            int spotLightCount = 0;
            int pointLightCount = 0;

            Matrix4x4[] worldToShadowMapCoordMatrices = new Matrix4x4[16];

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
                        worldToShadowMapCoordMatrices[spotLightCount] = worldToShadowMapCoordMatrix;

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
                            worldToShadowMapCoordMatrices[tileIndex] = worldToShadowMapCoordMatrix;

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

            cmb.SetGlobalMatrixArray(_WorldToShadowMapCoordMatrices, worldToShadowMapCoordMatrices);
        }

        ExcuteAndClearCommandBuffer(context, cmb);
        CommandBufferPool.Release(cmb);
    }

    private Matrix4x4 GetDirectionalLightCascadeShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projMatrix, Vector2Int offset, int sideSplitCount) {
        Matrix4x4 m = projMatrix * viewMatrix;

        if (SystemInfo.usesReversedZBuffer) {
            Vector4 forwardDir = -m.GetRow(2);
            m.SetRow(2, forwardDir);
        }

        float width = 1f / sideSplitCount;
        
        m = Matrix4x4.Scale(math.float3(0.5f * width, 0.5f  * width, 0.5f)) * m;
        m = Matrix4x4.Translate(math.float3(width * (0.5f + offset.x), width * (0.5f + offset.y), 0.5f)) * m;

        return m;
    }

    int ToSpotLightIndex(int index) {
        return index;
    }

    int ToPointLightIndex(int index) {
        return index + spotLightCapacity;
    }

    private Matrix4x4 CreateWorldToShadowMapCoordMatrix(Matrix4x4 viewMatrix, Matrix4x4 projMatrix, Rect viewPort) {
        Matrix4x4 vp = projMatrix * viewMatrix;

        if (SystemInfo.usesReversedZBuffer) {
            vp.SetRow(2, -vp.GetRow(2));
        }

        return vp;
    }

    private Rect GetSpotShadowMapViewport(int i, int sideSplitCount, int tileWidth) {
        int rowIndex = i / sideSplitCount;
        int colIndex = i % sideSplitCount;

        return new Rect(colIndex * tileWidth, rowIndex * tileWidth, tileWidth, tileWidth);
    }
}