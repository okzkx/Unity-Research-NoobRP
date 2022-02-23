using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class FXAA {
    [Range(0.0312f, 0.0833f)] public float fixedThreshold = 0.04f;

    [Range(0.063f, 0.333f)] public float relativeThreshold = 0.07f;

    [Range(0f, 1f)] public float subpixelBlending = 0.25f;

    public static implicit operator Vector4(FXAA fxaa) {
        return new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending);
    }
}

[CreateAssetMenu(menuName = "Rendering/Noob Render Pipeline Asset")]
public class NoobRenderPipelineAsset : RenderPipelineAsset {
    public bool srpBatch = false;
    public float maxShadowDistance = 100;

    [Serializable]
    public class BloomSettings {
        [Min(0f)] public float threshold = 0.5f;
        [Range(0f, 1f)] public float thresholdKnee = 0.5f;
        [Min(0f)] public float intensity = 1;
    }

    [Serializable]
    public class ColorAdjustments {
        public float postExposure = 0.5f;
        [Range(-100f, 100f)] public float contrast = 17;
        [ColorUsage(false, true)] public Color colorFilter = Color.white;
        [Range(-180f, 180f)] public float hueShift = 0;
        [Range(-100f, 100f)] public float saturation = 23;
    }

    [Serializable]
    public class WhiteBalance {
        [Range(-100f, 100f)] public float temperature;
        public float tint;
    }

    public BloomSettings bloom;
    public ColorAdjustments colorAdjustments;
    public WhiteBalance whiteBalance;

    [Range(0.5f, 2f)] public float renderScale = 1;

    public FXAA fxaa;
    public bool enableDefaultPass;

    protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline(this);
    }
}

public class NoobRenderPipeline : RenderPipeline {
    public NoobRenderPipelineAsset asset;

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    public LightStep lightStep;
    public RendererStep rendererStep;
    public PostprocessStep postprocessStep;

    public NoobRenderPipeline(NoobRenderPipelineAsset asset) {
        this.asset = asset;
        lightStep = new LightStep(this);
        rendererStep = new RendererStep(this);
        postprocessStep = new PostprocessStep(this);

        GraphicsSettings.useScriptableRenderPipelineBatching = asset.srpBatch;
    }

    private void Render(ScriptableRenderContext context, Camera camera) {
        if ((camera.cameraType & CameraType.SceneView) != 0) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera); //This makes the UI Canvas geometry appear on scene view
        }

        camera.allowHDR = true;
        CommandBuffer cmb = CommandBufferPool.Get();

        // Cullling
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
        scp.shadowDistance = Mathf.Min(asset.maxShadowDistance, camera.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scp);

        lightStep.Excute(ref context, ref cullingResults);

        float renderScale = asset.renderScale;
        Vector2Int bufferSize = new Vector2Int((int) (camera.pixelWidth * renderScale), (int) (camera.pixelHeight * renderScale));

        rendererStep.Excute(ref context, camera, bufferSize, ref cullingResults);

#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
#endif

        postprocessStep.Excute(ref context, bufferSize);

#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif

        EndRender(context, cmb);
    }

    private void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        lightStep.End(cmb);
        rendererStep.End(cmb);
        postprocessStep.End(cmb);
        context.Submit();
        cmb.Release();
    }
}