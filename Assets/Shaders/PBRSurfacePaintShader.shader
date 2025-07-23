Shader "Custom/PBRSurfacePaintShader"
{
    Properties
    {
        // Эти свойства больше не нужны, так как данные приходят из массивов
        // _PaintColor ("Paint Color", Color) = (1, 0, 0, 0.75)
        // _PaintRoughness ("Paint Roughness", Range(0.0, 1.0)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        // Максимальное количество классов, должно совпадать со значением в PaintManager.cs
        #define MAX_CLASSES 32

        sampler2D _GlobalCameraFeedTex;
        sampler2D _GlobalSegmentationTexture;
        sampler2D _CameraDepthTexture;

        // Централизованные массивы данных
        float4 _PaintColors[MAX_CLASSES];
        float4 _BlendModes[MAX_CLASSES]; // Передаем как float4, используем .x
        float4 _MetallicValues[MAX_CLASSES]; // Передаем как float4, используем .x
        float4 _SmoothnessValues[MAX_CLASSES]; // Передаем как float4, используем .x

        struct Input
        {
            float4 screenPos;
        };

        // --- Функции смешивания ---
        float3 ApplyMultiply(float3 base, float3 blend)
        {
            return base * blend;
        }

        float3 ApplyOverlay(float3 base, float3 blend)
        {
            float t = step(0.5, base);
            float3 screen = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
            float3 multiply = 2.0 * base * blend;
            return lerp(multiply, screen, t);
        }
        
        float3 ApplySoftLight(float3 base, float3 blend)
        {
            return (1.0 - 2.0 * blend) * base * base + 2.0 * blend * base;
        }


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // --- Окклюзия ---
            float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
            #if UNITY_AR_BACKGROUND_ARCORE
                float sceneDepth = 1 - tex2D(_CameraDepthTexture, screenUV).r;
            #else
                float sceneDepth = tex2D(_CameraDepthTexture, screenUV).r;
            #endif
            clip(IN.screenPos.z - sceneDepth);
            
            // --- Логика покраски ---
            // Получаем ID класса из маски сегментации
            float classIdFloat = tex2D(_GlobalSegmentationTexture, screenUV).r;
            int classId = (int)round(classIdFloat);

            // Получаем свойства для этого класса из массивов
            float4 paintColor = _PaintColors[classId];
            int blendMode = (int)_BlendModes[classId].x;
            float metallic = _MetallicValues[classId].x;
            float smoothness = _SmoothnessValues[classId].x;
            
            // Если альфа цвета близка к 0, значит класс не окрашен. Отбрасываем пиксель.
            clip(paintColor.a - 0.01);

            // Сэмплируем цвет с камеры
            float4 cameraColor = tex2D(_GlobalCameraFeedTex, screenUV);

            // Выбираем режим смешивания
            float3 blendedAlbedo = cameraColor.rgb;
            if (blendMode == 0) // Overlay
            {
                blendedAlbedo = ApplyOverlay(cameraColor.rgb, paintColor.rgb);
            }
            else if (blendMode == 1) // Multiply
            {
                blendedAlbedo = ApplyMultiply(cameraColor.rgb, paintColor.rgb);
            }
            else if (blendMode == 2) // Soft Light
            {
                blendedAlbedo = ApplySoftLight(cameraColor.rgb, paintColor.rgb);
            }

            // Интерполируем для эффекта прозрачности краски
            float3 finalAlbedo = lerp(cameraColor.rgb, blendedAlbedo, paintColor.a);

            // Устанавливаем PBR свойства
            o.Albedo = finalAlbedo;
            o.Metallic = metallic;
            o.Smoothness = smoothness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Transparent/VertexLit"
} 