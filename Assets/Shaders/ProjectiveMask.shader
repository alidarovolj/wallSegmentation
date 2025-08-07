Shader "Custom/ProjectiveMask"
{
    Properties
    {
        _MainTex ("Camera Texture", 2D) = "white" {}
        _SegmentationTex ("Segmentation Mask", 2D) = "black" {}
        _EnvironmentDepthTex ("Environment Depth", 2D) = "black" {}
        _MaskOpacity ("Mask Opacity", Range(0, 1)) = 0.5
        _DebugMode ("Debug Mode", Int) = 0
        _ScreenAspect ("Screen Aspect", Float) = 1.77
        _MaskAspect ("Mask Aspect", Float) = 1.0
        _AspectRatio ("Aspect Ratio", Float) = 1.0
        _ForceFullscreen ("Force Fullscreen", Int) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ DEBUG_DEPTH DEBUG_SEGMENTATION DEBUG_PROJECTION

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            // Текстуры
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _SegmentationTex;
            float4 _SegmentationTex_ST;
            sampler2D _EnvironmentDepthTex;
            float4 _EnvironmentDepthTex_ST;

            // Параметры
            float _MaskOpacity;
            int _DebugMode;
            float _ScreenAspect;
            float _MaskAspect;
            float _AspectRatio;
            int _ForceFullscreen;

            // Матрицы камеры (устанавливаются из скрипта)
            float4x4 _ProjectionMatrix;
            float4x4 _InverseProjectionMatrix;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            // Функция для получения цвета класса по стандартной цветовой карте ADE20K
            float3 getClassColor(int classId)
            {
                // Первые 30 классов ADE20K цветовой карты (основные для интерьеров)
                if (classId == 0) return float3(120/255.0, 120/255.0, 120/255.0); // wall (серый)
                if (classId == 1) return float3(180/255.0, 120/255.0, 120/255.0); // building
                if (classId == 2) return float3(6/255.0, 230/255.0, 230/255.0);   // sky (голубой)
                if (classId == 3) return float3(80/255.0, 50/255.0, 50/255.0);    // floor (темно-коричневый)
                if (classId == 4) return float3(4/255.0, 200/255.0, 3/255.0);     // tree (зеленый)
                if (classId == 5) return float3(120/255.0, 120/255.0, 80/255.0);  // ceiling (желто-серый)
                if (classId == 6) return float3(140/255.0, 140/255.0, 140/255.0); // road
                if (classId == 7) return float3(204/255.0, 5/255.0, 255/255.0);   // bed (фиолетовый)
                if (classId == 8) return float3(230/255.0, 230/255.0, 230/255.0); // windowpane
                if (classId == 9) return float3(10/255.0, 255/255.0, 71/255.0);   // grass
                if (classId == 10) return float3(255/255.0, 20/255.0, 147/255.0); // cabinet (розовый)
                if (classId == 11) return float3(20/255.0, 255/255.0, 20/255.0);  // sidewalk
                if (classId == 12) return float3(255/255.0, 0/255.0, 0/255.0);    // person (красный)
                if (classId == 13) return float3(255/255.0, 235/255.0, 205/255.0); // earth
                if (classId == 14) return float3(120/255.0, 120/255.0, 70/255.0); // door (оливковый)
                if (classId == 15) return float3(255/255.0, 165/255.0, 0/255.0);  // table (оранжевый)
                if (classId == 16) return float3(112/255.0, 128/255.0, 144/255.0); // mountain
                if (classId == 17) return float3(34/255.0, 139/255.0, 34/255.0);  // plant
                if (classId == 18) return float3(222/255.0, 184/255.0, 135/255.0); // curtain
                if (classId == 19) return float3(255/255.0, 105/255.0, 180/255.0); // chair (розовый)
                if (classId == 20) return float3(0/255.0, 0/255.0, 128/255.0);    // car (темно-синий)
                if (classId == 21) return float3(0/255.0, 0/255.0, 255/255.0);    // water (синий)
                if (classId == 22) return float3(255/255.0, 215/255.0, 0/255.0);  // painting (золотой)
                if (classId == 23) return float3(138/255.0, 43/255.0, 226/255.0); // sofa (фиолетовый)
                if (classId == 24) return float3(245/255.0, 222/255.0, 179/255.0); // shelf (бежевый)
                if (classId == 25) return float3(210/255.0, 105/255.0, 30/255.0); // house (коричневый)
                if (classId == 26) return float3(0/255.0, 191/255.0, 255/255.0);  // sea (светло-синий)
                if (classId == 27) return float3(192/255.0, 192/255.0, 192/255.0); // mirror (серебро)
                if (classId == 28) return float3(165/255.0, 42/255.0, 42/255.0);  // rug (темно-красный)
                if (classId == 29) return float3(240/255.0, 230/255.0, 140/255.0); // field (хаки)

                // Для остальных классов используем HSV генерацию
                float hue = frac((float)classId * 0.37f); 
                float saturation = 0.75f + frac((float)classId * 0.21f) * 0.25f;
                float value = 0.8f + frac((float)classId * 0.13f) * 0.2f;
                
                float h = hue * 6.0f;
                float c = value * saturation;
                float x = c * (1.0f - abs(fmod(h, 2.0f) - 1.0f));
                float m = value - c;
                float3 rgb;

                if (h < 1.0f) rgb = float3(c, x, 0);
                else if (h < 2.0f) rgb = float3(x, c, 0);
                else if (h < 3.0f) rgb = float3(0, c, x);
                else if (h < 4.0f) rgb = float3(0, x, c);
                else if (h < 5.0f) rgb = float3(x, 0, c);
                else rgb = float3(c, 0, x);

                return rgb + m;
            }

            // Функция восстановления мировой позиции из depth и screen coordinates
            float3 reconstructWorldPosition(float2 screenUV, float depth)
            {
                // Преобразуем UV в NDC (Normalized Device Coordinates)
                float2 ndcPos = screenUV * 2.0 - 1.0;
                
                // Создаем точку в clip space
                float4 clipPos = float4(ndcPos, depth, 1.0);
                
                // Преобразуем в view space
                float4 viewPos = mul(_InverseProjectionMatrix, clipPos);
                viewPos /= viewPos.w;
                
                // Возвращаем позицию в view space (достаточно для проекции)
                return viewPos.xyz;
            }

            // Функция для коррекции UV координат с учетом аспекта
            float2 correctAspectUV(float2 uv)
            {
                float2 correctedUV = uv;
                
                if (_ForceFullscreen == 1)
                {
                    // Центрируем UV координаты относительно центра
                    correctedUV = (correctedUV - 0.5);
                    
                    // Применяем коррекцию аспекта
                    if (_AspectRatio > 1.0)
                    {
                        // Маска уже, чем экран - растягиваем по горизонтали
                        correctedUV.x /= _AspectRatio;
                    }
                    else if (_AspectRatio < 1.0)
                    {
                        // Маска шире, чем экран - растягиваем по вертикали
                        correctedUV.y *= _AspectRatio;
                    }
                    
                    // Возвращаем в диапазон [0,1]
                    correctedUV = correctedUV + 0.5;
                }
                
                return saturate(correctedUV);
            }

            // Функция для сэмплирования маски сегментации на основе 3D-позиции
            float4 sampleSegmentationMask(float3 worldPos, float2 originalUV)
            {
                // Используем стабилизированное сэмплирование:
                // В реальном мире это была бы проекция через camera matrices,
                // но для стабильности используем screen UV с небольшой коррекцией
                
                float2 maskUV = originalUV;
                
                // БЕЗ ИНВЕРСИИ - как в palette ветке
                
                // Применяем коррекцию аспекта
                maskUV = correctAspectUV(maskUV);
                
                // Добавляем небольшую стабилизацию на основе глубины
                // Чем дальше объект, тем меньше смещение UV
                float depthFactor = saturate(length(worldPos) / 10.0); // нормализация до 10 метров
                maskUV += (originalUV - 0.5) * depthFactor * 0.01; // небольшая коррекция перспективы
                maskUV = saturate(maskUV); // обрезаем в пределах [0,1]
                
                // Читаем значение из маски сегментации
                float4 maskValue = tex2D(_SegmentationTex, maskUV);
                
                return maskValue;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Получаем базовое изображение с камеры
                fixed4 cameraColor = tex2D(_MainTex, i.uv);
                
                // Получаем screen UV координаты
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                
                // РАННИЙ ВОЗВРАТ В ТЕСТОВОМ РЕЖИМЕ: показываем чистую маску без зависимости от глубины
                if (_DebugMode == 3)
                {
                    float2 uv = saturate(i.screenPos.xy / i.screenPos.w);
                    // БЕЗ ИНВЕРСИИ КООРДИНАТ - как в рабочей palette ветке
                    // Применяем коррекцию аспекта в тестовом режиме тоже
                    uv = correctAspectUV(uv);
                    // Преобразуем идентификатор класса в цвет
                    float m = tex2D(_SegmentationTex, uv).r;
                    int classId = (int)(m * 255.0 + 0.5);
                    float3 col = getClassColor(classId);
                    return float4(col, 1.0);
                }
                
                // Читаем depth
                float depth = tex2D(_EnvironmentDepthTex, screenUV).r;
                
                // Отладочные режимы
                if (_DebugMode == 1) // Показать глубину
                {
                    return fixed4(depth, depth, depth, 1.0);
                }
                
                if (_DebugMode == 2) // Показать UV координаты
                {
                    return fixed4(screenUV, 0, 1.0);
                }
                
                // Проверяем валидность depth
                if (depth <= 0.0 || depth >= 1.0)
                {
                    // Если глубина недоступна, показываем обычное изображение
                    return cameraColor;
                }
                
                // Восстанавливаем 3D-позицию
                float3 worldPos = reconstructWorldPosition(screenUV, depth);
                
                // Получаем маску сегментации
                float4 maskValue = sampleSegmentationMask(worldPos, i.uv);
                
                // Извлекаем ID класса из маски (предполагаем, что это в красном канале)
                int classId = (int)(maskValue.r * 255.0 + 0.5);
                
                // Если класс 0 (фон) или нет маски, показываем обычное изображение
                if (classId == 0 || maskValue.r < 0.01)
                {
                    return cameraColor;
                }
                
                // Получаем цвет для класса
                float3 classColor = getClassColor(classId);
                
                // Смешиваем цвет камеры с цветом маски
                float3 finalColor = lerp(cameraColor.rgb, classColor, _MaskOpacity);
                
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    
    Fallback "Unlit/Texture"
}
