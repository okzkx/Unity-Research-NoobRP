using UnityEngine;
using UnityEngine.Rendering;

public class RendererStep : RenderStep {
    public readonly ShaderTagId NoobRPLightMode = new ShaderTagId("Both");
    public readonly ShaderTagId m_PassNameDefault = new ShaderTagId("SRPDefaultUnlit"); //The shader pass tag for replacing shaders without pass
    public readonly int _ColorAttachment = Shader.PropertyToID("_CameraFrameBuffer");
    public readonly int _DepthAttachment = Shader.PropertyToID("_DepthBuffer");
    public readonly int _ColorMap = Shader.PropertyToID("_ColorMap");
    public readonly int _DepthMap = Shader.PropertyToID("_DepthMap");

    public RendererStep(NoobRenderPipeline noobRenderPipeline) {
    }

    // Render Renderers
    // if (false) 
    public void Excute(ref ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, ref CullingResults cullingResults) {
        // Draw Setting
        var sortingSettings = new SortingSettings(camera);
        var drawingSettings = new DrawingSettings(NoobRPLightMode, default);
        drawingSettings.perObjectData =
            PerObjectData.ReflectionProbes |
            PerObjectData.Lightmaps | PerObjectData.ShadowMask |
            PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
            PerObjectData.LightProbeProxyVolume |
            PerObjectData.OcclusionProbeProxyVolume;
        DrawingSettings drawSettingsDefault = drawingSettings;
        drawSettingsDefault.SetShaderPassName(1, m_PassNameDefault);

        // Filter Setting
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        var cmb = CommandBufferPool.Get("RendererStep");

        // Set up shader properties
        context.SetupCameraProperties(camera);

        cmb.SetGlobalVector("_BufferSize", new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));

        cmb.GetTemporaryRT(_ColorAttachment, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        cmb.GetTemporaryRT(_DepthAttachment, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
        cmb.SetRenderTarget(_ColorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            _DepthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
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
            cmb.CopyTexture(_ColorAttachment, _ColorMap);
            cmb.CopyTexture(_DepthAttachment, _DepthMap);
            ExcuteAndClearCommandBuffer(context, cmb);
        }

        // Draw transparent
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        drawSettingsDefault.sortingSettings = sortingSettings;
        context.DrawRenderers(cullingResults, ref drawSettingsDefault, ref filteringSettings);

        ExcuteAndClearCommandBuffer(context, cmb);
    }

    public void End(CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_ColorAttachment);
        cmb.ReleaseTemporaryRT(_DepthAttachment);
        cmb.ReleaseTemporaryRT(_ColorMap);
        cmb.ReleaseTemporaryRT(_DepthMap);
    }
}