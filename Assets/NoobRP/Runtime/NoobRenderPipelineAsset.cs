using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Noob Render Pipeline")]
public class NoobRenderPipelineAsset : RenderPipelineAsset {
	[SerializeField]
	bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true;
	[SerializeField]
	ShadowSettings shadows = default;
	protected override RenderPipeline CreatePipeline() {
        return new NoobRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadows);
    }
}