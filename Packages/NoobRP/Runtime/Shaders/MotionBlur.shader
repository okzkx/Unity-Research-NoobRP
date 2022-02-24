Shader "NoobRP/MotionBlur" {

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/UnityInput.hlsl"

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
    
    #include "../ShaderLibrary/Debug.hlsl"
    #include "../ShaderLibrary/MontionBlur.hlsl"
    ENDHLSL

    SubShader {
        Tags {
            "RenderPipeline"="NoobRP"
        }

        Pass {
            Tags {
                "LightMode" = "Both"
            }

            HLSLPROGRAM

            #pragma vertex Vertex
            #pragma fragment Fragment
            
            ENDHLSL
        }
    }
}