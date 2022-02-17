using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class LightStep : RenderStep{
    const int directionalLightCapacity = 1;
    const int spotLightCapacity = 4;
    const int pointLightCapacity = 2;
    const string DIRECTIONAL_SHADOW_MAP = "ShadowMap.Directional";
    const string SPOT_POINT_SHADOW_MAP = "ShadowMap.SpotPoint";

    readonly int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    readonly int _SpotPointShadowAtlas = Shader.PropertyToID("_SpotPointShadowAtlas");

    public string stepName = "LightStep";
    NoobRenderPipeline noobRenderPipeline;

    public LightStep(NoobRenderPipeline noobRenderPipeline) {
        this.noobRenderPipeline = noobRenderPipeline;
    }

    public void End(CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_DirectionalShadowAtlas);
        cmb.ReleaseTemporaryRT(_SpotPointShadowAtlas);
    }

    public void Excute(ref ScriptableRenderContext context, ref CullingResults cullingResults) {
        var cmb = CommandBufferPool.Get(stepName);
        
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

        // cmb.BeginSample("ShadowMap");

        // Render Directianl Light ShadowMap
        // if (isGameCam) 
        {
            // cmb.BeginSample(DIRECTIONAL_SHADOW_MAP);

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

            // cmb.EndSample(DIRECTIONAL_SHADOW_MAP);

            ExcuteAndClearCommandBuffer(context, cmb);
        }

        // Render Spot and Point Light ShadowMap
        // if (false) 
        {
            // cmb.BeginSample(SPOT_POINT_SHADOW_MAP);

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

            // cmb.EndSample(SPOT_POINT_SHADOW_MAP);

            ExcuteAndClearCommandBuffer(context, cmb);
        }

        // cmb.EndSample("ShadowMap");
        CommandBufferPool.Release(cmb);
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

    static Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split) {
        if (SystemInfo.usesReversedZBuffer) {
            m.SetRow(2, -m.GetRow(2));
        }

        m = Matrix4x4.Scale(math.float3(0.25f, 0.25f, 0.5f)) * m;
        m = Matrix4x4.Translate(math.float3(0.25f + 0.5f * offset.x, 0.25f + 0.5f * offset.y, 0.5f)) * m;

        return m;
    }
}