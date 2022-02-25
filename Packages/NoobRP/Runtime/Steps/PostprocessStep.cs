using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class PostprocessStep : RenderStep {
    const string BLOOM = "Bloom";
    const string LUT = "LUT";
    const string BLOOM_PYRAMID = "_BloomPyramid";
    const string FINAL_BLIT = "Final Blit";
    const string DRAW_RENDERERS = "RenderLoop.Clear";

    public NoobRenderPipeline noobRenderPipeline;
    public Material postProcessMaterial;

    readonly int _PostMap = Shader.PropertyToID("_PostMap");
    readonly int _PostMap2 = Shader.PropertyToID("_PostMap2");
    readonly int _BloomPrefilter = Shader.PropertyToID("_BloomPrefilter");
    readonly int _BloomIntensity = Shader.PropertyToID("_BloomIntensity");
    readonly int _BloomResult = Shader.PropertyToID("_BloomResult");
    readonly int _ColorGradingLUT = Shader.PropertyToID("_ColorGradingLUT");
    readonly int _ColorLUTResult = Shader.PropertyToID("_ColorLUTResult");
    readonly int _FXAAConfig = Shader.PropertyToID("_FXAAConfig");
    readonly int _AATexture = Shader.PropertyToID("_AATexture");
    readonly int _FinalTexture = Shader.PropertyToID("_FinalTexture");
    int _MotionBlurResult = Shader.PropertyToID("_MotionBlurResult");
    int _TextureInput = Shader.PropertyToID("_TextureInput");
    int _LastTexture = Shader.PropertyToID("_LastTexture");
    private Material motionBlurMaterial;

    public PostprocessStep(NoobRenderPipeline noobRenderPipeline) {
        this.noobRenderPipeline = noobRenderPipeline;
        postProcessMaterial = CoreUtils.CreateEngineMaterial("NoobRP/PostProcess");
        motionBlurMaterial = CoreUtils.CreateEngineMaterial("NoobRP/MotionBlur");
    }

    enum Pass {
        Copy,
        BloomPrefilter,
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        TomeMapping,
        Final,
        FXAA
    }

    public void Excute(ref ScriptableRenderContext context, Vector2Int bufferSize) {
        CommandBuffer cmb = CommandBufferPool.Get("PostprocessStep");
        NoobRenderPipelineAsset asset = noobRenderPipeline.asset;
        int _CameraFrameBuffer = noobRenderPipeline.rendererStep._ColorAttachment;

        // Bloom
        using (new ProfilingScope(cmb, new ProfilingSampler(BLOOM))) {
            RenderTextureFormat renderTextureFormat = RenderTextureFormat.DefaultHDR;

            // Pre filter
            int width = bufferSize.x / 2;
            int height = bufferSize.y / 2;
            NoobRenderPipelineAsset.BloomSettings bloom = asset.bloom;
            {
                int _BloomThreshold = Shader.PropertyToID("_BloomThreshold");

                Vector4 threshold;
                threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
                threshold.y = threshold.x * bloom.thresholdKnee;
                threshold.z = 2f * threshold.y;
                threshold.w = 0.25f / (threshold.y + 0.00001f);
                threshold.y -= threshold.x;
                cmb.SetGlobalVector(_BloomThreshold, threshold);

                cmb.GetTemporaryRT(_BloomPrefilter, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                BlitTexture(cmb, _CameraFrameBuffer, _BloomPrefilter, Pass.BloomPrefilter);
            }

            // Bloom Pyramid
            {
                int GetPyramidShaderID(int id) {
                    return Shader.PropertyToID(BLOOM_PYRAMID + id);
                }

                width /= 2;
                height /= 2;
                int bloomMaxIterations = 4;
                int bloomScaleLimit = 2;

                // Pyramid Generate
                {
                    int from = _BloomPrefilter;
                    int i = 0;
                    for (; i < bloomMaxIterations; i++) {
                        if (height < bloomScaleLimit || width < bloomScaleLimit) {
                            break;
                        }

                        int to = Shader.PropertyToID(BLOOM_PYRAMID + (i * 2 + 1));
                        int intermidiate = Shader.PropertyToID(BLOOM_PYRAMID + i * 2);

                        cmb.GetTemporaryRT(intermidiate, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                        cmb.GetTemporaryRT(to, width, height, 0, FilterMode.Bilinear, renderTextureFormat);
                        BlitTexture(cmb, from, intermidiate, Pass.BloomHorizontal);
                        BlitTexture(cmb, intermidiate, to, Pass.BloomVertical);

                        from = to;
                        width /= 2;
                        height /= 2;
                    }
                }

                // Pyramid Combine
                {
                    int c1Id = bloomMaxIterations * 2 - 1;
                    int c2Id = c1Id - 2;
                    int c3Id = c2Id - 1;

                    cmb.SetGlobalFloat(_BloomIntensity, bloom.intensity);

                    while (c1Id > 0) {
                        int c1 = GetPyramidShaderID(c1Id);
                        int c2 = GetPyramidShaderID(c2Id);
                        int c3 = GetPyramidShaderID(c3Id);

                        cmb.SetGlobalTexture(_PostMap, c1);
                        cmb.SetGlobalTexture(_PostMap2, c2);
                        cmb.SetRenderTarget(c3, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) Pass.BloomCombine, MeshTopology.Triangles, 3);

                        c1Id = c3Id;
                        c2Id = c1Id - 1;
                        c3Id = c2Id - 1;
                    }
                }

                cmb.GetTemporaryRT(_BloomResult, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, renderTextureFormat);
                CombineTexture(cmb, GetPyramidShaderID(0), _CameraFrameBuffer, _BloomResult);

                for (int i = 0; i < bloomMaxIterations * 2; i++) {
                    cmb.ReleaseTemporaryRT(GetPyramidShaderID(i));
                }
            }
        }

        int colorLUTresolution = 32;
        int lutHeight = colorLUTresolution;
        int lutWidth = lutHeight * lutHeight;

        // Color Grading data prepare
        using (new ProfilingScope(cmb, new ProfilingSampler(LUT))) {
            // Color Adjectments
            {
                var colorAdjustments = asset.colorAdjustments;
                cmb.SetGlobalVector("_ColorAdjustments", new Vector4(
                    Mathf.Pow(2f, colorAdjustments.postExposure),
                    colorAdjustments.contrast * 0.01f + 1f,
                    colorAdjustments.hueShift * (1f / 360f),
                    colorAdjustments.saturation * 0.01f + 1f
                ));
                cmb.SetGlobalVector("_ColorFilter", colorAdjustments.colorFilter.linear);
            }

            // White Balance
            {
                var whiteBalance = asset.whiteBalance;
                cmb.SetGlobalVector("_WhiteBalance",
                    ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint)
                );
            }

            // TODO: SplitToning
            // TODO: Channel Mixer
            // TODO: Shadow Midtones Highlights

            // Render LUT with Color Grading and Toon mapping
            cmb.GetTemporaryRT(_ColorGradingLUT, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            Vector4 colorGradingLUTParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f));
            cmb.SetGlobalVector("_ColorGradingLUTParameters", colorGradingLUTParameters);
            // no mater what's input, aim to generate _ColorGradingLUT
            BlitTexture(cmb, _BloomResult, _ColorGradingLUT, Pass.TomeMapping);
        }

        // Apply color LUT
        using (new ProfilingScope(cmb, new ProfilingSampler("Apply color LUT"))) {
            cmb.SetGlobalVector("_LUTScaleOffset", new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1));
            cmb.GetTemporaryRT(_ColorLUTResult, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            BlitTexture(cmb, _BloomResult, _ColorLUTResult, Pass.Final);
        }

        // FXAA
        using (new ProfilingScope(cmb, new ProfilingSampler("FXAA"))) {
            FXAA fxaa = asset.fxaa;
            cmb.SetGlobalVector(_FXAAConfig, fxaa);
            cmb.GetTemporaryRT(_AATexture, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            BlitTexture(cmb, _ColorLUTResult, _AATexture, Pass.FXAA);
        }

        // Motion blur
        using (new ProfilingScope(cmb, new ProfilingSampler("Motion Blur"))) {
            cmb.SetGlobalTexture(_TextureInput, _AATexture);
            // TODO: _LastTexture comes to mistake, how to get a persistence RenderTexture
            // cmb.GetTemporaryRT(_LastTexture, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            cmb.GetTemporaryRT(_MotionBlurResult, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            // cmb.Blit(_AATexture, _MotionBlurResult, motionBlurMaterial);
            cmb.Blit(_AATexture, _MotionBlurResult);
        }

        // Final Copy
        using (new ProfilingScope(cmb, new ProfilingSampler("Final Copy"))) {
            RenderTextureDescriptor colorRTDesc = new RenderTextureDescriptor(bufferSize.x, bufferSize.y);
            colorRTDesc.enableRandomWrite = true; //For compute
            cmb.GetTemporaryRT(_FinalTexture, colorRTDesc);
            cmb.Blit(_MotionBlurResult, _FinalTexture);
        }

        // Execute compute shader
        if (noobRenderPipeline.asset.computeShader != null) {
            using (new ProfilingScope(cmb, new ProfilingSampler("Execute compute shader"))) {
                ComputeShader computeShader = noobRenderPipeline.asset.computeShader;
                cmb.SetComputeTextureParam(computeShader, 0, _FinalTexture, _FinalTexture);
                cmb.DispatchCompute(computeShader, 0, bufferSize.x / 8 + 1, bufferSize.y / 8 + 1, 1);
            }
        }

        // Final blit
        using (new ProfilingScope(cmb, new ProfilingSampler(FINAL_BLIT))) {
            BlitTexture(cmb, _FinalTexture, BuiltinRenderTextureType.CameraTarget, Pass.Copy); 
            // TODO: _LastTexture comes to mistake, how to get a persistence RenderTexture
            // cmb.Blit(_FinalTexture, _LastTexture);
        }

        ExcuteAndClearCommandBuffer(context, cmb);
    }

    private void BlitTexture(CommandBuffer cmb, RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass, Rect? viewPortRect = null) {
        cmb.SetGlobalTexture(_PostMap, from);
        cmb.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        if (viewPortRect != null) {
            cmb.SetViewport(viewPortRect.Value);
        }

        cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) pass, MeshTopology.Triangles, 3);
    }

    private void CombineTexture(CommandBuffer cmb, RenderTargetIdentifier rt1, RenderTargetIdentifier rt2, RenderTargetIdentifier rt3) {
        cmb.SetGlobalTexture(_PostMap, rt1);
        cmb.SetGlobalTexture(_PostMap2, rt2);
        cmb.SetRenderTarget(rt3, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmb.DrawProcedural(Matrix4x4.identity, postProcessMaterial, (int) Pass.BloomCombine, MeshTopology.Triangles, 3);
    }

    public void End(CommandBuffer cmb) {
        cmb.ReleaseTemporaryRT(_PostMap);
        cmb.ReleaseTemporaryRT(_PostMap2);
        cmb.ReleaseTemporaryRT(_BloomPrefilter);
        cmb.ReleaseTemporaryRT(_BloomResult);
        cmb.ReleaseTemporaryRT(_ColorGradingLUT);
        cmb.ReleaseTemporaryRT(_AATexture);
        cmb.ReleaseTemporaryRT(_ColorLUTResult);
        cmb.ReleaseTemporaryRT(_FinalTexture);
        cmb.ReleaseTemporaryRT(_MotionBlurResult);
        cmb.ReleaseTemporaryRT(_LastTexture);
    }
}