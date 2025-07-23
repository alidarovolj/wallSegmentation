Shader "Unlit/SurfacePaintShader"
{
    Properties
    {
        _PaintColor ("Paint Color", Color) = (1, 0, 0, 0.5)
        _BlendMode ("Blend Mode", Int) = 0
        // 0 = Normal, 1 = Multiply, 2 = Overlay, 3 = Soft Light
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ BLEND_MULTIPLY BLEND_OVERLAY BLEND_SOFT_LIGHT
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            // Глобальные переменные (устанавливаются из C#)
            sampler2D _GlobalCameraTexture;     // Видеопоток с камеры
            sampler2D _GlobalSegmentationTexture; // Маска сегментации
            int _GlobalTargetClassID;           // ID класса для покраски
            float4 _GlobalPaintColor;           // Цвет покраски
            int _GlobalBlendMode;               // Режим смешивания

            // Локальные свойства (для совместимости)
            float4 _PaintColor;
            int _BlendMode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            // Функции смешивания цветов
            float3 BlendMultiply(float3 base, float3 blend)
            {
                return base * blend;
            }

            float3 BlendOverlay(float3 base, float3 blend)
            {
                return lerp(
                    2.0 * base * blend,
                    1.0 - 2.0 * (1.0 - base) * (1.0 - blend),
                    step(0.5, base)
                );
            }

            float3 BlendSoftLight(float3 base, float3 blend)
            {
                float3 result1 = 2.0 * base * blend + base * base * (1.0 - 2.0 * blend);
                float3 result2 = sqrt(base) * (2.0 * blend - 1.0) + 2.0 * base * (1.0 - blend);
                return lerp(result1, result2, step(0.5, blend));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем UV координаты экрана
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // Инвертируем Y координату для правильной ориентации
                screenUV.y = 1.0 - screenUV.y;

                // Сэмплируем текстуру сегментации
                float4 segmentationSample = tex2D(_GlobalSegmentationTexture, screenUV);
                
                // Извлекаем класс из R канала (предполагаем, что класс закодирован как 0-1)
                int pixelClass = (int)(segmentationSample.r * 255.0);
                
                // Проверяем, соответствует ли пиксель целевому классу
                if (pixelClass != _GlobalTargetClassID || _GlobalTargetClassID < 0)
                {
                    discard; // Не рисуем этот пиксель
                }

                // Получаем исходный цвет с камеры
                float3 cameraColor = tex2D(_GlobalCameraTexture, screenUV).rgb;
                
                // Применяем выбранный режим смешивания
                float3 paintColor = _GlobalPaintColor.rgb;
                float3 blendedColor;
                
                int blendMode = _GlobalBlendMode;
                
                if (blendMode == 1) // Multiply
                {
                    blendedColor = BlendMultiply(cameraColor, paintColor);
                }
                else if (blendMode == 2) // Overlay
                {
                    blendedColor = BlendOverlay(cameraColor, paintColor);
                }
                else if (blendMode == 3) // Soft Light
                {
                    blendedColor = BlendSoftLight(cameraColor, paintColor);
                }
                else // Normal (Alpha Blend)
                {
                    blendedColor = lerp(cameraColor, paintColor, _GlobalPaintColor.a);
                }
                
                return fixed4(blendedColor, _GlobalPaintColor.a);
            }
            ENDCG
        }
    }
} 