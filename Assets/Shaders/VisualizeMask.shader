Shader "Unlit/VisualizeMask"
{
    Properties
    {
        _MaskTex ("Segmentation Mask (RFloat)", 2D) = "white" {}
        _SelectedClass ("Selected Class (-1 for all)", Int) = -1
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _PaintColor ("Paint Color", Color) = (1, 0, 0, 1)
        [Toggle] _ShowRawValues ("Show Raw Values (Debug)", Float) = 0
        
        [HideInInspector] _IsPortrait ("Is Portrait Mode", Float) = 0
        [HideInInspector] _IsRealDevice ("Is Real Device", Float) = 0
        [HideInInspector] _RotationMode ("Rotation Mode (0=+90, 1=-90, 2=180, 3=none)", Int) = 0
        [HideInInspector] _ForceFullscreen ("Force Fullscreen", Int) = 1
        [HideInInspector] _ScreenAspect ("Screen Aspect", Float) = 1.0
        [HideInInspector] _MaskAspect ("Mask Aspect", Float) = 1.0
        [HideInInspector] _AspectRatio ("Aspect Ratio", Float) = 1.0
        [HideInInspector] _CropOffsetX ("Crop Offset X", Float) = 0.0
        [HideInInspector] _CropOffsetY ("Crop Offset Y", Float) = 0.0
        [HideInInspector] _CropScale ("Crop Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MaskTex;
            int _SelectedClass;
            float _Opacity;
            fixed4 _PaintColor;
            float _IsPortrait;
            float _IsRealDevice;
            float _ShowRawValues;
            int _RotationMode;
            int _ForceFullscreen;
            float _ScreenAspect;
            float _MaskAspect;
            float _AspectRatio;
            float _CropOffsetX;
            float _CropOffsetY;
            float _CropScale;
            
            float3 getClassColor(int classId)
            {
                // ИСПРАВЛЕННАЯ цветовая карта ADE20K для точного распознавания
                if (classId == 0) return float3(120/255.0, 120/255.0, 120/255.0); // wall (серый)
                if (classId == 1) return float3(180/255.0, 120/255.0, 120/255.0); // building
                if (classId == 2) return float3(6/255.0, 230/255.0, 230/255.0);   // background/sky (голубой)
                if (classId == 3) return float3(80/255.0, 50/255.0, 50/255.0);    // floor (темно-коричневый)
                if (classId == 4) return float3(4/255.0, 200/255.0, 3/255.0);     // tree (зеленый)
                if (classId == 5) return float3(120/255.0, 120/255.0, 80/255.0);  // ceiling (желто-серый)
                if (classId == 6) return float3(140/255.0, 140/255.0, 140/255.0); // road
                if (classId == 7) return float3(204/255.0, 5/255.0, 255/255.0);   // bed (фиолетовый)
                if (classId == 8) return float3(230/255.0, 230/255.0, 230/255.0); // windowpane
                if (classId == 9) return float3(10/255.0, 255/255.0, 71/255.0);   // grass
                if (classId == 10) return float3(255/255.0, 20/255.0, 147/255.0); // cabinet (розовый)
                if (classId == 11) return float3(20/255.0, 255/255.0, 20/255.0);  // sidewalk
                if (classId == 12) return float3(255/255.0, 0/255.0, 0/255.0);    // person (ЯРКО-КРАСНЫЙ для выделения)
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

            // ИСПРАВЛЕННАЯ функция для коррекции UV координат с учетом crop квадратной области
            float2 correctAspectUV(float2 uv)
            {
                if (_ForceFullscreen != 1)
                {
                    return uv;
                }
                
                float2 correctedUV = uv;
                
                // 1. СНАЧАЛА: Компенсируем crop, примененный к камере
                // Обратная трансформация crop: маска соответствует центральному квадрату камеры
                correctedUV = correctedUV * _CropScale + float2(_CropOffsetX, _CropOffsetY);
                
                // ТОЧНАЯ НАСТРОЙКА: Выравниваем маску с камерой
                correctedUV += float2(0, -0.03);

                // 2. ПОТОМ: Растягиваем маску для соответствия экрану
                float screenAspect = _ScreenAspect;  // 0.462 для iPhone
                
                if (screenAspect < 1.0) // Портретный экран
                {
                    // ТОЧНАЯ коррекция: растягиваем UV по X для покрытия ширины
                    float correction = 1.0 / screenAspect * 1.15; // Уменьшили до 1.15 для точности
                    correctedUV.x = (correctedUV.x - 0.5) * correction + 0.5;
                }
                else // Ландшафтный экран
                {
                    // Растягиваем UV по Y для ландшафта
                    correctedUV.y = (correctedUV.y - 0.5) * screenAspect + 0.5;
                }
                
                return correctedUV;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // ИСПРАВЛЕНИЕ ПОВОРОТА: Поворачиваем UV координаты для правильной ориентации маски
                float2 uv = v.uv;
                
                // Выбираем тип поворота на основе _RotationMode
                if (_RotationMode == 0) {
                    // +90 градусов (по часовой стрелке)
                    uv = float2(1.0 - uv.y, uv.x);
                } else if (_RotationMode == 1) {
                    // -90 градусов (против часовой стрелки)
                    uv = float2(uv.y, 1.0 - uv.x);
                } else if (_RotationMode == 2) {
                    // 180 градусов
                    uv = float2(1.0 - uv.x, 1.0 - uv.y);
                } else {
                    // Без поворота
                    // uv остается как есть
                }
                
                // ИСПРАВЛЕНИЕ РАМОК: Применяем коррекцию аспекта после поворота
                o.uv = correctAspectUV(uv);
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Используем UV как есть, без коррекций
                float class_index_float = tex2D(_MaskTex, i.uv).r;
                int class_index = (int)round(class_index_float);
                
                // DEBUG: Показываем raw значения как градации серого
                if (_ShowRawValues > 0.5)
                {
                    return fixed4(frac(class_index_float / 20.0), frac(class_index_float / 20.0), frac(class_index_float / 20.0), _Opacity);
                }

                // Если выбран режим "скрыть все", делаем пиксель прозрачным
                if (_SelectedClass == -2)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // Если мы в режиме "показать один класс"
                if (_SelectedClass >= 0)
                {
                    if (class_index == _SelectedClass)
                    {
                        return fixed4(_PaintColor.rgb, _Opacity);
                    }
                    else
                    {
                        // УЛУЧШЕНИЕ: Показываем другие классы полупрозрачными для контекста
                        float3 otherColor = getClassColor(class_index) * 0.3; // Приглушенные
                        return fixed4(otherColor, _Opacity * 0.2);
                    }
                }
                
                // Если мы в режиме "показать все классы"
                if (_SelectedClass == -1)
                {
                    // Получаем цвет для текущего класса
                    float3 color = getClassColor(class_index);
                    return fixed4(color, _Opacity);
                }

                // Во всех остальных случаях - делаем пиксель прозрачным.
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
