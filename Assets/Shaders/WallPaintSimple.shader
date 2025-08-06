Shader "Custom/WallPaintPhotorealistic"
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
                float _BlendMode;
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
            
            // Luminance-based recoloring function (preserves lighting and texture)
            half3 BlendLuminance(half3 base, half3 blend)
            {
                // Calculate luminance using Rec.709 weights
                half luminance = dot(base, half3(0.2126, 0.7152, 0.0722));
                return blend * luminance;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Apply flip transformations to UV coordinates
                float2 flippedUV = input.uv;
                
                // Swap X and Y coordinates and invert Y to fix portrait orientation mapping
                flippedUV = float2(flippedUV.y, 1.0 - flippedUV.x);
                
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
                
                // DEBUG: Диагностика Edge Softness
                half finalAlpha;
                
                // Calculate edge width based on screen-space derivatives
                half edgeWidth = fwidth(mask) * _EdgeSoftness;
                
                // DEBUG: Показываем разные методы сглаживания в зависимости от _EdgeSoftness
                if (_EdgeSoftness < 1.0) {
                    // Резкие края - используем исходную маску
                    finalAlpha = mask;
                } else if (_EdgeSoftness < 3.0) {
                    // Умеренное сглаживание - простой smoothstep  
                    finalAlpha = smoothstep(0.3, 0.7, mask);
                } else {
                    // Сильное размытие - очень мягкий переход
                    finalAlpha = smoothstep(0.1, 0.9, mask);
                }
                
                // Sample scene color (camera background)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 sceneColor = SampleSceneColor(screenUV);
                
                // Calculate lit paint color
                half3 litPaintColor = _PaintColor.rgb * _RealWorldLightColor.rgb * _GlobalBrightness;
                
                // DEBUG: Визуальная диагностика режимов смешивания
                half3 luminanceResult = BlendLuminance(sceneColor, litPaintColor);
                half3 overlayResult = BlendOverlay(sceneColor, litPaintColor);
                
                // Добавляем цветовые индикаторы для режимов
                if (_BlendMode < 0.33) {
                    // Luminance режим - добавляем зеленый оттенок для диагностики
                    luminanceResult.g += 0.2;
                } else if (_BlendMode > 0.66) {
                    // Overlay режим - добавляем красный оттенок для диагностики  
                    overlayResult.r += 0.2;
                } else {
                    // Гибридный режим - добавляем синий оттенок для диагностики
                    luminanceResult.b += 0.2;
                    overlayResult.b += 0.2;
                }
                
                half3 blendedColor = lerp(luminanceResult, overlayResult, _BlendMode);
                
                return half4(blendedColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}