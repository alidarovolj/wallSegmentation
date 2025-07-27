Shader "Unlit/VisualizeMask"
{
    Properties
    {
        _MaskTex ("Segmentation Mask (RFloat)", 2D) = "white" {}
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
            
            // Простая цветовая карта на 16 классов
            static const float3 colorMap[16] = {
                float3(0, 0, 0),        // 0: background
                float3(1, 0, 0),        // 1: wall (Red)
                float3(0, 1, 0),        // 2: floor (Green)
                float3(0, 0, 1),        // 3: ceiling (Blue)
                float3(1, 1, 0),        // 4: door (Yellow)
                float3(0, 1, 1),        // 5: window (Cyan)
                float3(0.5, 0.5, 0.5),  // 6: cabinet (Gray)
                float3(1, 0, 1),        // 7: chair (Magenta)
                float3(0.5, 0, 1),      // 8: sofa (Purple)
                float3(0, 0.5, 0.5),    // 9: table (Teal)
                float3(1, 0.5, 0),      // 10: bed (Orange)
                float3(0.5, 1, 0),      // 11: person (Lime)
                float3(0.2, 0.8, 0.2),  // 12: plant (Forest Green)
                float3(1, 0.75, 0.8),   // 13: furniture_other (Pink)
                float3(0.8, 0.6, 0.4),  // 14: decoration (Brown)
                float3(0.3, 0.3, 0.8)   // 15: electronics (Indigo)
            };

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

                // Показываем только класс 'wall' (ID 1)
                if (class_index == 1)
                {
                    // Используем красный цвет для стен с полупрозрачностью
                    return fixed4(1.0, 0.0, 0.0, 0.5); 
                }
                else
                {
                    // Все остальные классы делаем невидимыми
                    return fixed4(0, 0, 0, 0);
                }
            }
            ENDCG
        }
    }
} 