using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class NoobRenderPipeline : RenderPipeline {
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching, useGPUInstancing;
    public NoobRenderPipeline(
        bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher) {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
        foreach (Camera camera in cameras) {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing);
        }
    }
}

