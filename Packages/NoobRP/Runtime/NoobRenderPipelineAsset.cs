using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Noob Render Pipeline Asset")]
public class NoobRenderPipelineAsset : RenderPipelineAsset {
    protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline();
    }
}

public class NoobRenderPipeline : RenderPipeline {
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    private static readonly ShaderTagId NoobRPLightMode = new ShaderTagId("Both");

    private static void Render(ScriptableRenderContext context, Camera camera) {
        CommandBuffer cmb = CommandBufferPool.Get("ClearRenderTarget");

        var clearFlags = camera.clearFlags;
        bool shouldClearDepth = clearFlags == CameraClearFlags.Depth;
        bool shouldClearColor = clearFlags == CameraClearFlags.Color;
        cmb.ClearRenderTarget(shouldClearDepth, shouldClearColor, Color.blue);
        ExcuteAndClearCommandBuffer(context, cmb);

        // Set up shader properties
        context.SetupCameraProperties(camera);

        // Cullling
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
        // p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
        var cullingResults = context.Cull(ref scp);

        // Draw Setting
        var sortingSettings = new SortingSettings(camera);
        var drawingSettings = new DrawingSettings(NoobRPLightMode, default);

        // Filter Setting
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        // Light Setting
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        Color _DirectionalLightColor = Color.black;
        Vector4 _DirectionalLightDirection = Vector4.zero;
        foreach (var visibleLight in visibleLights) {
            if (visibleLight.lightType == LightType.Directional) {
                _DirectionalLightColor = visibleLight.finalColor;
                _DirectionalLightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
            }
        }

        cmb.SetGlobalColor("_DirectionalLightColor", _DirectionalLightColor);
        cmb.SetGlobalVector("_DirectionalLightDirection", _DirectionalLightDirection);
        ExcuteAndClearCommandBuffer(context, cmb);

        // Render Renderers
        // if (false) 
        {
            // Draw opaque
            sortingSettings.criteria = SortingCriteria.CommonOpaque;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            context.DrawSkybox(camera);

            // Draw transparent
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }

        EndRender(context, cmb);
    }

    private static void ExcuteAndClearCommandBuffer(ScriptableRenderContext context, CommandBuffer cmb) {
        context.ExecuteCommandBuffer(cmb);
        cmb.Clear();
    }

    private static void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        context.Submit();
        cmb.Release();
    }
}