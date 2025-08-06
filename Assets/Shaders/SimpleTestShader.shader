Shader "Custom/SimpleTestShader"
{
    Properties
    {
        _SegmentationMask ("Segmentation Mask", 2D) = "white" {}
        _TestColor ("Test Color", Color) = (1, 0, 0, 1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_SegmentationMask);
            SAMPLER(sampler_SegmentationMask);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _TestColor;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 maskValue = SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, input.uv);
                half alpha = maskValue.a;
                
                return half4(_TestColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}