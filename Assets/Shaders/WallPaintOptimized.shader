Shader "Custom/WallPaintOptimized"
{
    Properties
    {
        _SegmentationMask ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (0, 0.5, 1, 1)
        _GlobalBrightness ("Global Brightness", Range(0, 2)) = 1
        _RealWorldLightColor ("Real World Light Color", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.1, 5.0)) = 1.0
        _FlipHorizontally ("Flip Horizontally", Float) = 0
        _FlipVertically ("Flip Vertically", Float) = 0
        _InvertMask ("Invert Mask", Float) = 1
        _BlendMode ("Blend Mode", Range(0, 1)) = 1
        
        // Precomputed values for optimization
        _LitPaintColor ("Lit Paint Color (Precomputed)", Color) = (0, 0.5, 1, 1)
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
            #pragma target 3.0
            
            // Mobile optimization pragmas
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                half2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_SegmentationMask);
            SAMPLER(sampler_SegmentationMask);
            
            CBUFFER_START(UnityPerMaterial)
                half4 _PaintColor;
                half _GlobalBrightness;
                half4 _RealWorldLightColor;
                half _EdgeSoftness;
                half _FlipHorizontally;
                half _FlipVertically;
                half _InvertMask;
                half _BlendMode;
                half4 _LitPaintColor; // Precomputed on CPU
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            
            // Optimized Overlay blend mode function using half precision
            half3 BlendOverlayOptimized(half3 base, half3 blend)
            {
                // Optimized: use step and lerp instead of conditional
                half3 multiply = 2.0h * base * blend;
                half3 screen = 1.0h - 2.0h * (1.0h - base) * (1.0h - blend);
                half3 mask = step(0.5h, base);
                return lerp(multiply, screen, mask);
            }
            
            // Optimized Luminance-based recoloring using half precision
            half3 BlendLuminanceOptimized(half3 base, half3 blend)
            {
                // Use half precision for luminance calculation
                half luminance = dot(base, half3(0.2126h, 0.7152h, 0.0722h));
                return blend * luminance;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Optimize UV flipping using step and lerp
                half2 flippedUV = input.uv;
                flippedUV.x = lerp(flippedUV.x, 1.0h - flippedUV.x, step(0.5h, _FlipHorizontally));
                flippedUV.y = lerp(flippedUV.y, 1.0h - flippedUV.y, step(0.5h, _FlipVertically));
                
                // Sample segmentation mask
                half mask = SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV).r;
                
                // Optimize mask inversion using lerp
                mask = lerp(mask, 1.0h - mask, step(0.5h, _InvertMask));
                
                // Optimized edge smoothing with less expensive operations
                half finalAlpha;
                half softness = _EdgeSoftness * 0.1h; // Scale down for better control
                
                // Use simple smoothstep for edge smoothing (avoiding fwidth in mobile)
                finalAlpha = smoothstep(0.5h - softness, 0.5h + softness, mask);
                
                // Sample scene color using half precision
                half2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 sceneColor = SampleSceneColor(screenUV).rgb;
                
                // Use precomputed lit paint color from CPU
                half3 litPaintColor = _LitPaintColor.rgb;
                
                // Optimized hybrid blending using lerp
                half3 luminanceResult = BlendLuminanceOptimized(sceneColor, litPaintColor);
                half3 overlayResult = BlendOverlayOptimized(sceneColor, litPaintColor);
                half3 blendedColor = lerp(luminanceResult, overlayResult, _BlendMode);
                
                return half4(blendedColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}