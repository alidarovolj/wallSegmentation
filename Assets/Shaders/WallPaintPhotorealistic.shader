Shader "Custom/WallPaintPhotorealistic"
{
    Properties
    {
        _SegmentationMask ("Segmentation Mask", 2D) = "white" {}
        _PaintColor ("Paint Color", Color) = (0, 0.5, 1, 1)
        _GlobalBrightness ("Global Brightness", Range(0, 2)) = 1
        _RealWorldLightColor ("Real World Light Color", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };
            
            TEXTURE2D(_SegmentationMask);
            SAMPLER(sampler_SegmentationMask);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _PaintColor;
                float _GlobalBrightness;
                float4 _RealWorldLightColor;
                float _EdgeSoftness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            
            // Overlay blend mode function
            half3 BlendOverlay(half3 base, half3 blend)
            {
                return lerp(2.0 * base * blend, 1.0 - 2.0 * (1.0 - base) * (1.0 - blend), step(0.5, base));
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample segmentation mask
                half4 maskValue = SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, input.uv);
                half maskAlpha = maskValue.a;
                
                // Calculate smooth alpha for edges
                half finalAlpha = smoothstep(0.1, 0.1 + _EdgeSoftness, maskAlpha);
                
                // Sample scene color (camera background)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 sceneColor = SampleSceneColor(screenUV);
                
                // Calculate lit paint color
                half3 litPaintColor = _PaintColor.rgb * _RealWorldLightColor.rgb * _GlobalBrightness;
                
                // Apply overlay blend mode
                half3 blendedColor = BlendOverlay(sceneColor, litPaintColor);
                
                return half4(blendedColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}