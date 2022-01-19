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
        
    }
}