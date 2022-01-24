Shader "NoobRP/Lit" {
    Properties {
        _LightIntencity("_LightIntencity", Float) = 4
        [KeywordEnum(Lambert, Half_Lambert)] _Diffuse("_Diffuse", Float) = 0
        [MainColor]_BaseColor("_BaseColor",Color)=(1,1,1,1)
        [MainTexture]_MainTex("_MainTex",2D)="white"{}
        [KeywordEnum(None, Phone, Bling_Phone)] _Specular("_Specular", Float) = 0
        _SpecularPow ("_SpecularPow", Range(1,90)) =30
        _SpecularColor ("_SpecularColor", color) =(1.0,1.0,1.0,1.0)
        _CutOff ("_CutOff", Range(0,1)) =0
        _Roughness ("_Roughness", Range(0,1)) =0

    }

    HLSLINCLUDE
    //-------------------------------------------------------------------------------------
    // library include
    //-------------------------------------------------------------------------------------

    // Libraries

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
                "LightMode"="Both"
            }

            HLSLPROGRAM
            
            #pragma multi_compile _DIFFUSE_LAMBERT _DIFFUSE_HALF_LAMBERT
            #pragma multi_compile _SPECULAR_NONE _SPECULAR_PHONE _SPECULAR_BLING_PHONE

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/ShaderPassBoth.hlsl"
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }

            HLSLPROGRAM
            
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/ShaderPassShadowCaster.hlsl"
            ENDHLSL
        }
    }
}