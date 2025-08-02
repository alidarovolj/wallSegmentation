Shader "Custom/WallPaintPhotorealistic"
{
    Properties
    {
        _SegmentationMask ("Segmentation Mask", 2D) = "black" {}
        _PaintColor ("Paint Color", Color) = (0, 0.5, 1, 1)
        _GlobalBrightness ("Global Brightness", Range(0, 2)) = 1
        _RealWorldLightColor ("Real World Light Color", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.1
        _FlipHorizontally ("Flip Horizontally", Float) = 0
        _FlipVertically ("Flip Vertically", Float) = 0
        _InvertMask ("Invert Mask", Float) = 1
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
                float _FlipHorizontally;
                float _FlipVertically;
                float _InvertMask;
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
                // Apply flip transformations to UV coordinates
                float2 flippedUV = input.uv;
                if (_FlipHorizontally > 0.5)
                    flippedUV.x = 1.0 - flippedUV.x;
                if (_FlipVertically > 0.5)
                    flippedUV.y = 1.0 - flippedUV.y;
                
                // Sample segmentation mask with potentially flipped coordinates
                half mask = SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV).r;
                
                // Инвертируем маску при необходимости
                if (_InvertMask > 0.5)
                {
                    mask = 1.0 - mask;
                }
                
                // Calculate smooth alpha for edges with manual blur
                half finalAlpha;
                
                if (_EdgeSoftness > 0.01)
                {
                    // Простое 3x3 размытие для сглаживания краев
                    float2 texelSize = 1.0 / float2(512, 512) * _EdgeSoftness; // Предполагаем маска 512x512
                    
                    // 9-точечное размытие
                    half blur = 0;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(-texelSize.x, -texelSize.y)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(0, -texelSize.y)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(texelSize.x, -texelSize.y)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(-texelSize.x, 0)).r;
                    blur += mask; // Центральный пиксель
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(texelSize.x, 0)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(-texelSize.x, texelSize.y)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(0, texelSize.y)).r;
                    blur += SAMPLE_TEXTURE2D(_SegmentationMask, sampler_SegmentationMask, flippedUV + float2(texelSize.x, texelSize.y)).r;
                    
                    blur /= 9.0; // Усредняем
                    
                    if (_InvertMask > 0.5)
                    {
                        blur = 1.0 - blur;
                    }
                    
                    finalAlpha = blur;
                }
                else
                {
                    // Без размытия - резкие края
                    finalAlpha = mask;
                }
                
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