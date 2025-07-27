Shader "Unlit/VisualizeMask"
{
    Properties
    {
        _MaskTex ("Segmentation Mask (RFloat)", 2D) = "white" {}
        _SelectedClass ("Selected Class (-1 for all)", Int) = -1
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _PaintColor ("Paint Color", Color) = (1, 0, 0, 1)
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MaskTex;
            int _SelectedClass;
            float _Opacity;
            fixed4 _PaintColor;
            
            // Функция для генерации процедурного цвета на основе ID класса
            float3 getClassColor(int classId)
            {
                // Используем HSV в RGB конвертацию для получения приятных, различных цветов
                float hue = frac((float)classId * 0.61803398875f); // Золотое сечение
                float saturation = 0.8f;
                float value = 0.95f;

                float3 hsv = float3(hue, saturation, value);
                float3 rgb = cos(2.0 * 3.14159265 * (hsv.x + float3(0.0, -1.0/3.0, 1.0/3.0)));
                rgb = hsv.z * (1.0 - hsv.y + hsv.y * (0.5 + 0.5 * rgb));
                return rgb;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Считываем индекс класса из RFloat текстуры
                float class_index_float = tex2Dlod(_MaskTex, float4(i.uv, 0, 0)).r;
                int class_index = (int)round(class_index_float);

                // Если _SelectedClass = -2, ничего не показываем
                if (_SelectedClass == -2)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // Если _SelectedClass = -1, показываем все классы
                // Если _SelectedClass >= 0, показываем только выбранный класс
                if (_SelectedClass >= 0)
                {
                    if (class_index == _SelectedClass)
                    {
                        // Показываем выбранный класс, используя цвет для покраски
                        return fixed4(_PaintColor.rgb, _Opacity);
                    }
                    else
                    {
                        // Скрываем все остальные классы
                        return fixed4(0, 0, 0, 0);
                    }
                }
                else
                {
                    // Показываем все классы (кроме background)
                    // Для этой модели нет background класса, поэтому показываем все с ID > -1
                    if (class_index >= 0)
                    {
                        return fixed4(getClassColor(class_index), _Opacity);
                    }
                    else
                    {
                        return fixed4(0, 0, 0, 0);
                    }
                }
            }
            ENDCG
        }
    }
} 