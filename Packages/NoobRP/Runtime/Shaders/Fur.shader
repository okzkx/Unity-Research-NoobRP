Shader "NoobRP/Fur" {
    Properties {
		_MainTex ("Main Texture", 2D) = "white" {}
		[NoScaleOffset] _NoiseTex ("Noise Texture", 2D) = "white" {}
		_FurLayer ("Fur Layer", Range(0.1,1)) = 1
		_FurFactor ("Fur Factor", Range(1,10)) = 1
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
		Tags { "RenderType"="Opaque" }

		Pass
		{
			Tags { "LightMode" = "MultiPass0" }
			HLSLPROGRAM
			#define FURLAYER 0.0
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass1" }
			HLSLPROGRAM
			#define FURLAYER 0.01
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass2" }
			HLSLPROGRAM
			#define FURLAYER 0.02
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass3" }
			HLSLPROGRAM
			#define FURLAYER 0.03
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass4" }
			HLSLPROGRAM
			#define FURLAYER 0.04
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass5" }
			HLSLPROGRAM
			#define FURLAYER 0.05
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass6" }
			HLSLPROGRAM
			#define FURLAYER 0.06
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass7" }
			HLSLPROGRAM
			#define FURLAYER 0.07
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass8" }
			HLSLPROGRAM
			#define FURLAYER 0.08
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
		Pass
		{
			Tags { "LightMode" = "MultiPass9" }
			HLSLPROGRAM
			#define FURLAYER 0.09
			#pragma vertex vert
			#pragma fragment frag
			#include "FurPass.hlsl"
			ENDHLSL
		}
    }
    FallBack "Diffuse"
}