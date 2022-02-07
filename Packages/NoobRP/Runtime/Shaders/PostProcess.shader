Shader "NoobRP/PostProcess" {
    SubShader {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        //-------------------------------------------------------------------------------------
        // library include
        //-------------------------------------------------------------------------------------

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.noobrp.core/Runtime/ShaderLibrary/UnityInput.hlsl"

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

        #include "../ShaderLibrary/PostProcessPasses.hlsl"
        ENDHLSL

        Pass {
            Name "Copy"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }
    }
}