Shader "NoobRP/UnLit" {
    Properties {
        _LightIntencity("光照强度", Float) = 4
        [KeywordEnum(Lambert, Half_Lambert)] _Diffuse("漫反射模型", Float) = 0
        [MainColor]_BaseColor("漫反射颜色",Color)=(1,1,1,1)
        [MainTexture]_MainTex("表面纹理",2D)="white"{}
        [KeywordEnum(None, Phone, Bling_Phone)] _Specular("漫反射模型", Float) = 0
        _SpecularPow ("高光锐利度", Range(1,90)) =30
        _SpecularColor ("高光颜色", color) =(1.0,1.0,1.0,1.0)
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
            Name "FORWARD"
            Tags {
                "LightMode"="NoobRPLightMode"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/ShaderPassBoth.hlsl"
            ENDHLSL
        }
    }
}