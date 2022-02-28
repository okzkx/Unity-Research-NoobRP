using UnityEngine;
using UnityEngine.Rendering;

public class RendererStep : RenderStep {
    public readonly ShaderTagId Both = new ShaderTagId("Both");
    public readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit"); //The shader pass tag for replacing shaders without pass
    public readonly int _ColorAttachment = Shader.PropertyToID("_CameraFrameBuffer");
    public readonly int _DepthAttachment = Shader.PropertyToID("_DepthBuffer");
    public readonly int _ColorMap = Shader.PropertyToID("_ColorMap");
    public readonly int _DepthMap = Shader.PropertyToID("_DepthMap");
    public readonly int _MotionVectorMap = Shader.PropertyToID("_MotionVectorMap");

    public readonly ShaderTagId[] multiPassTags = new ShaderTagId[10];

    private NoobRenderPipeline noobRenderPipeline;
    private Material motionVectorMaterial;
    private Matrix4x4 _NonJitteredVP;
    private Matrix4x4 _PreviousVP;
    private int _VPLast = Shader.PropertyToID("_VPLast");
    private Matrix4x4 vpLast = Matrix4x4.identity;


    public RendererStep(NoobRenderPipeline noobRenderPipeline) {
        for (int i = 0; i < multiPassTags.Length; i++) {
            multiPassTags[i] = new ShaderTagId("MultiPass" + i);
        }

        this.noobRenderPipeline = noobRenderPipeline;
        motionVectorMaterial = CoreUtils.CreateEngineMaterial("NoobRP/MotionVector");
    }

    // Render Renderers
    // if (false) 
    public void Execute(ref ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, ref CullingResults cullingResults) {
        // Draw Setting
        var sortingSettings = new SortingSettings(camera);
        var drawingSettings = new DrawingSettings(Both, default);
        drawingSettings.perObjectData = PerObjectData.None | PerObjectData.MotionVectors;
        
        // Filter Setting
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        var cmb = CommandBufferPool.Get("RendererStep");

        // Set up shader properties
        context.SetupCameraProperties(camera);

        // ExcuteAndClearCommandBuffer(context, cmb);

        //************************** Rendering motion vectors ************************************
        // if(false){
        using (new ProfilingScope(cmb, new ProfilingSampler("Rendering motion vectors "))) {
            DrawingSettings drawSettingsMotionVector = new DrawingSettings(defaultPassName, sortingSettings) {
                perObjectData = PerObjectData.MotionVectors,
                overrideMaterial = motionVectorMaterial,
                overrideMaterialPassIndex = 0
            };

            FilteringSettings filterSettingsMotionVector = new FilteringSettings(RenderQueueRange.all) {
                excludeMotionVectorObjects = false
            };

            // Opaques motion vector
            {
                cmb.GetTemporaryRT(_MotionVectorMap, bufferSize.x, bufferSize.y, 24, FilterMode.Bilinear, RenderTextureFormat.Default);
                cmb.SetRenderTarget(_MotionVectorMap);
                cmb.ClearRenderTarget(true, true, Color.black);
                cmb.SetGlobalMatrix(_VPLast, vpLast);
                ExcuteAndClearCommandBuffer(context, cmb);

                //Opaque objects
                sortingSettings.criteria = SortingCriteria.CommonOpaque;
                drawSettingsMotionVector.sortingSettings = sortingSettings;
                filterSettingsMotionVector.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cullingResults, ref drawSettingsMotionVector, ref filterSettingsMotionVector);
            }
            // camera.worldToCameraMatrix
            Matrix4x4 V = camera.worldToCameraMatrix;
            V.SetColumn(1, V.GetColumn(1) * -1);
            // V.set
            vpLast = camera.projectionMatrix * V;
        }

        using (new ProfilingScope(cmb, new ProfilingSampler("RendererStep"))) {

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

            for (int i = 0; i < multiPassTags.Length; i++) {
                drawingSettings.SetShaderPassName(i + 1, multiPassTags[i]);
            }

            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            if (noobRenderPipeline.asset.enableDefaultPass) {
                DrawingSettings drawSettingsDefault = new DrawingSettings(SRPDefaultUnlit, default);
                drawSettingsDefault.SetShaderPassName(1, SRPDefaultUnlit);
                drawSettingsDefault.sortingSettings = sortingSettings;
                context.DrawRenderers(cullingResults, ref drawSettingsDefault, ref filteringSettings);
            }
        }

        ExcuteAndClearCommandBuffer(context, cmb);
    }

    public void End(CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_ColorAttachment);
        cmb.ReleaseTemporaryRT(_DepthAttachment);
        cmb.ReleaseTemporaryRT(_ColorMap);
        cmb.ReleaseTemporaryRT(_DepthMap);
        cmb.ReleaseTemporaryRT(_MotionVectorMap);
    }
}