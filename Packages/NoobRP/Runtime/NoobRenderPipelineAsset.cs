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

    private static readonly ShaderTagId NoobRPLightMode = new ShaderTagId("NoobRPLightMode");

    private static void Render(ScriptableRenderContext context, Camera camera) {
        CommandBuffer cmb = CommandBufferPool.Get("ClearRenderTarget");
        var clearFlags = camera.clearFlags;
        bool shouldClearDepth = clearFlags == CameraClearFlags.Depth;
        bool shouldClearColor = clearFlags == CameraClearFlags.Color;
        cmb.ClearRenderTarget(shouldClearDepth, shouldClearColor, Color.blue);
        context.ExecuteCommandBuffer(cmb);
        cmb.Release();

        // Render Renderers
        // if (false) 
        {
            // Set up shader properties
            context.SetupCameraProperties(camera);

            // Cullling
            if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
            // p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            var cullingResults = context.Cull(ref scp);

            // Draw Setting
            var sortingSettings = new SortingSettings(camera);
            var drawingSettings = new DrawingSettings(NoobRPLightMode, default);

            // Filter
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

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

        context.Submit();
    }
}