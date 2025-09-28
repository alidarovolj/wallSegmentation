Shader "Unlit/SurfacePaintShader"
{
    Properties
    {
        // Эта текстура будет нашим "холстом" для рисования.
        // Она будет уникальной для каждой поверхности через MaterialPropertyBlock.
        _PaintMap ("Paint Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // Стандартный альфа-блендинг
            ZWrite Off // Не пишем в Z-буфер, т.к. поверхность уже существует
            Cull Off // Отключаем отсечение, т.к. меши могут быть видны с обеих сторон

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

            sampler2D _PaintMap;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Просто передаем UV координаты меша во фрагментный шейдер
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Сэмплируем нашу текстуру-холст по UV координатам меша
                fixed4 paintColor = tex2D(_PaintMap, i.uv);
                
                // Финальный цвет - это то, что нарисовано на холсте.
                // Альфа-канал определяет, насколько сильно мы закрашиваем.
                // Если на холсте ничего нет (альфа = 0), пиксель будет полностью прозрачным.
                return paintColor;
            }
            ENDCG
        }
    }
} 