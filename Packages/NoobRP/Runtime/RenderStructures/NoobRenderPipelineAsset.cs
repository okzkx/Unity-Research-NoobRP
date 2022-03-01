using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    public ComputeShader computeShader;

    public enum RenderMode {
        Steps,
        RenderGraph
    }

    public RenderMode renderMode;

    protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline(this);
    }

    public override Shader defaultShader => Shader.Find("NoobRP/Lit");
}

public class NoobRenderPipeline : RenderPipeline {
    public NoobRenderPipelineAsset asset;
    public LightStep lightStep;
    public RendererStep rendererStep;
    public PostprocessStep postprocessStep;
    public RenderGraphPath renderGraphPath;

    public NoobRenderPipeline(NoobRenderPipelineAsset asset) {
        this.asset = asset;
        lightStep = new LightStep(this);
        rendererStep = new RendererStep(this);
        postprocessStep = new PostprocessStep(this);
        renderGraphPath = new RenderGraphPath(this);

        GraphicsSettings.useScriptableRenderPipelineBatching = asset.srpBatch;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        switch (asset.renderMode) {
            case NoobRenderPipelineAsset.RenderMode.Steps:
                NormalPathExecute(context, cameras);
                break;
            case NoobRenderPipelineAsset.RenderMode.RenderGraph:
                renderGraphPath.Execute(context, cameras);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void NormalPathExecute(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            Render(context, camera);
        }
    }

    private void Render(ScriptableRenderContext context, Camera camera) {
        if ((camera.cameraType & CameraType.SceneView) != 0) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera); //This makes the UI Canvas geometry appear on scene view
        }

        camera.allowHDR = true;

        // Cullling
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
        scp.shadowDistance = Mathf.Min(asset.maxShadowDistance, camera.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scp);

        // Execute normal path
        lightStep.Execute(ref context, ref cullingResults);

        CommandBuffer cmb = CommandBufferPool.Get();
        Vector2Int bufferSize = InitBufferSize(context, camera, cmb);

        bool isShadingMode = true;
#if UNITY_EDITOR
        ArrayList sceneViewsArray = SceneView.sceneViews;
        foreach (SceneView sceneView in sceneViewsArray) {
            if (sceneView.camera == camera) {
                if (sceneView.cameraMode.drawMode != DrawCameraMode.Textured) {
                    isShadingMode = false;
                }
            }
        }

#endif

        if (isShadingMode) {
            rendererStep.Execute(ref context, camera, bufferSize, ref cullingResults);
        } else {
            // TODO: Draw objects though specify material by draw mode
            rendererStep.ExecuteDrawCameraMode(ref context, camera, bufferSize, ref cullingResults);
        }


#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        }
#endif
        
        postprocessStep.Execute(ref context, bufferSize);

#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos()) {
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif

        EndRender(context, cmb);
    }

    public Vector2Int InitBufferSize(ScriptableRenderContext context, Camera camera, CommandBuffer cmb) {
        float renderScale = asset.renderScale;
        Vector2Int bufferSize = new Vector2Int((int) (camera.pixelWidth * renderScale), (int) (camera.pixelHeight * renderScale));
        cmb.SetGlobalVector("_BufferSize", new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));
        context.ExecuteCommandBuffer(cmb);
        cmb.Clear();

        return bufferSize;
    }

    private void EndRender(ScriptableRenderContext context, CommandBuffer cmb) {
        lightStep.End(cmb);
        rendererStep.End(cmb);
        postprocessStep.End(cmb);
        context.Submit();
        cmb.Release();
    }
}