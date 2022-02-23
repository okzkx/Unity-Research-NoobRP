Shader "NoobRP/Particle/UnLit" {
    Properties {
        [MainColor]_BaseColor("_BaseColor",Color)=(1,1,1,1)
        [MainTexture]_BaseMap("_BaseMap",2D)="white"{}
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
        _Distortion("_Distortion", Range(0, 0.05)) = 0.005
        _BufferSize ("BufferSize", Vector) = (1,1,1,1)
    }

    HLSLINCLUDE
    //-------------------------------------------------------------------------------------
    // library include
    //-------------------------------------------------------------------------------------

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/UnityInput.hlsl"

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
    #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/ParticleUnlit.hlsl"
    ENDHLSL

    SubShader {
        Tags {
            "RenderPipeline"="NoobRP"
        }

        Pass {
            Tags {
                "RenderQueue" = "Transparent"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off

            HLSLPROGRAM
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_HALF_LAMBERT
            #pragma multi_compile _SPECULAR_NONE _SPECULAR_PHONE _SPECULAR_BLING_PHONE

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        
        Pass {
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
            ENDHLSL
        }

        Pass {
            Tags {
                "LightMode" = "MultiPass0"
                "RenderQueue" = "Transparent"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off

            HLSLPROGRAM
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_HALF_LAMBERT
            #pragma multi_compile _SPECULAR_NONE _SPECULAR_PHONE _SPECULAR_BLING_PHONE

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass {
            Tags {
                "LightMode" = "MultiPass1"
                "RenderQueue" = "Transparent"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off

            HLSLPROGRAM
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_HALF_LAMBERT
            #pragma multi_compile _SPECULAR_NONE _SPECULAR_PHONE _SPECULAR_BLING_PHONE

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}