Shader "NoobRP/Particle/UnLit" {
    Properties {
        [MainColor]_BaseColor("_BaseColor",Color)=(1,1,1,1)
        [MainTexture]_BaseMap("_BaseMap",2D)="white"{}
        _CutOff ("_CutOff", Range(0,1)) = 0.1
        _EnvWeight ("_EnvWeight", Range(0,1)) =0

        [HDR]_EmissionColor("_EmissionColor", Color) = (1,1,1,1)
        [NoScaleOffset] _EmissionMap("EmissionMap",2D)="white"{}
        [NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}

        _Emission("Emission", Range(0, 1)) = 0
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Occlusion ("Occlusion", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Fresnel("FresnelXX", Range(0, 1)) = 0
    }

    HLSLINCLUDE
    
    //-------------------------------------------------------------------------------------
    // library include
    //-------------------------------------------------------------------------------------

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/UnityInput.hlsl"

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
    
    ENDHLSL

    SubShader {
        Tags {
            "RenderPipeline"="NoobRP"
        }

        Pass {
            Name "Both"
            Tags {
                "LightMode" = "Both"
                "RenderQueue" = "Transparent"
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off 

            HLSLPROGRAM
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_HALF_LAMBERT
            #pragma multi_compile _SPECULAR_NONE _SPECULAR_PHONE _SPECULAR_BLING_PHONE

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/ParticleUnlit.hlsl"

            ENDHLSL
        }
    }
}