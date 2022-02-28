using System.Dynamic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using RendererListDesc = UnityEngine.Rendering.RendererUtils.RendererListDesc;
using UnityEngine.Rendering;

public class RenderGraphPath {
    private readonly NoobRenderPipelineAsset asset;
    private readonly NoobRenderPipeline renderPipeline;
    private readonly RenderGraph renderGraph = new RenderGraph();
    private readonly ShaderTagId passName = new ShaderTagId("Both");

    private TextureHandle colorTexture;
    private Vector2Int bufferSize;

    public RenderGraphPath(NoobRenderPipeline renderPipeline) {
        this.asset = renderPipeline.asset;
        this.renderPipeline = renderPipeline;
    }

    class RenderersPassData {
        public RendererListHandle opaqueRenderList;
    }

    class PostProcessPassData {
        public TextureHandle colorTexture;
    }

    private TextureHandle CreateColorTexture(RenderGraph graph, string name) {
        bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);

        //Texture description
        TextureDesc colorRTDesc = new TextureDesc(bufferSize.x, bufferSize.y) {
            colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB),
            depthBufferBits = 0,
            msaaSamples = MSAASamples.None,
            enableRandomWrite = false,
            clearBuffer = true,
            clearColor = Color.black,
            name = name
        };

        return graph.CreateTexture(colorRTDesc);
    }

    private TextureHandle CreateDepthTexture(RenderGraph graph) {
        bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);

        //Texture description
        TextureDesc colorRTDesc = new TextureDesc(bufferSize.x, bufferSize.y) {
            colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Depth, colorRT_sRGB),
            depthBufferBits = DepthBits.Depth24,
            msaaSamples = MSAASamples.None,
            enableRandomWrite = false,
            clearBuffer = true,
            clearColor = Color.black,
            name = "Depth"
        };

        return graph.CreateTexture(colorRTDesc);
    }

    public void Execute(ScriptableRenderContext context, Camera[] cameras) {
        foreach (var camera in cameras) {
            context.SetupCameraProperties(camera);

            // Cullling
            if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scp)) return;
            scp.shadowDistance = Mathf.Min(asset.maxShadowDistance, camera.farClipPlane);
            CullingResults cullingResults = context.Cull(ref scp);

            CommandBuffer commandBuffer = CommandBufferPool.Get();

            bufferSize = renderPipeline.InitBufferSize(context, camera, commandBuffer);

            RenderGraphParameters renderGraphParameters = new RenderGraphParameters() {
                commandBuffer = commandBuffer,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount,
            };

            using (renderGraph.RecordAndExecute(renderGraphParameters)) {
                RecordRenderersStep(camera, cullingResults);
                RecordPostProcessStep();
            }

            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);

            context.Submit();
        }

        renderGraph.EndFrame();
    }

    private void RecordRenderersStep(Camera camera, CullingResults cullingResults) {
        colorTexture = CreateColorTexture(renderGraph, "Color Texture");
        TextureHandle depthTexture = CreateDepthTexture(renderGraph);
        RendererListDesc rendererListDesc = new RendererListDesc(passName, cullingResults, camera) {
            sortingCriteria = SortingCriteria.CommonOpaque,
            renderQueueRange = RenderQueueRange.opaque
        };
        RendererListHandle renderList = renderGraph.CreateRendererList(rendererListDesc);

        using (var builder = renderGraph.AddRenderPass<RenderersPassData>("Renderers Step", out var passData)) {
            builder.UseColorBuffer(colorTexture, 0);
            builder.UseDepthBuffer(depthTexture, DepthAccess.Write);
            passData.opaqueRenderList = builder.UseRendererList(renderList);
            builder.SetRenderFunc((RenderersPassData contextPassData, RenderGraphContext context) => {
                context.renderContext.DrawSkybox(camera);
                CoreUtils.DrawRendererList(context.renderContext, context.cmd, contextPassData.opaqueRenderList);
            });
        }
    }

    private void RecordPostProcessStep() {
        using (var builder = renderGraph.AddRenderPass<PostProcessPassData>("PostProcess Step", out var passData)) {
            passData.colorTexture = builder.ReadTexture(colorTexture);
            builder.SetRenderFunc((PostProcessPassData contextPassData, RenderGraphContext context) => {
                RenderTargetIdentifier renderTargetIdentifier = BuiltinRenderTextureType.CameraTarget;
                context.cmd.Blit(contextPassData.colorTexture, renderTargetIdentifier);
            });
        }
    }
}