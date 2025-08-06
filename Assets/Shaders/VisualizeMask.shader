Shader "Unlit/VisualizeMask"
{
    Properties
    {
        _MaskTex ("Segmentation Mask (RFloat)", 2D) = "white" {}
        _SelectedClass ("Selected Class (-1 for all)", Int) = -1
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _PaintColor ("Paint Color", Color) = (1, 0, 0, 1)
        
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
            
            float3 getClassColor(int classId)
            {
                float hue = frac((float)classId * 0.61803398875f);
                float saturation = 0.9f;
                float value = 0.85f;
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

                // НЕ ДЕЛАЕМ НИКАКИХ ТРАНСФОРМАЦИЙ - всё уже сделано при конвертации камеры
                float2 uv = v.uv;
                
                // Используем UV координаты как есть - MirrorY уже применен в ConvertCpuImageToTexture
                // НИКАКИХ дополнительных инвертирований!
                
                o.uv = uv;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float class_index_float = tex2D(_MaskTex, i.uv).r;
                int class_index = (int)round(class_index_float);

                // Если выбран режим "скрыть все", делаем пиксель прозрачным
                if (_SelectedClass == -2)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // Если мы в режиме "показать один класс"
                if (_SelectedClass >= 0)
                {
                    // Если индекс пикселя совпадает с выбранным классом - красим
                    if (class_index == _SelectedClass)
                    {
                        return fixed4(_PaintColor.rgb, _Opacity);
                    }
                }
                
                // Если мы в режиме "показать все классы"
                if (_SelectedClass == -1)
                {
                    // Красим каждый класс своим цветом
                    return fixed4(getClassColor(class_index), _Opacity);
                }

                // Во всех остальных случаях (например, если мы в режиме "один класс",
                // но текущий пиксель не принадлежит ему) - делаем пиксель прозрачным.
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}