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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // UV-координаты передаются без изменений
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
