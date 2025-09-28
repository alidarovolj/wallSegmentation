Shader "Custom/WallPaintShader"
{
    Properties
    {
        _PaintColor ("Paint Color", Color) = (1,1,1,1)
        _SegmentationTex ("Segmentation Texture (ID map)", 2D) = "white" {}
        _TargetClassID ("Target Class ID", Float) = 0
        _NumClasses ("Number of Classes", Float) = 21
        _DebugMode ("Debug Mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _SegmentationTex;
            float4 _SegmentationTex_ST;
            fixed4 _PaintColor;
            float _TargetClassID;
            float _NumClasses;
            float _DebugMode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _SegmentationTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_DebugMode > 0.5)
                {
                    return fixed4(1, 0, 0, 0.5);
                }

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float segmentValue = tex2D(_SegmentationTex, screenUV).r;

                int classID = (int)round(segmentValue * (_NumClasses - 1));

                if (classID == _TargetClassID)
                {
                    UNITY_APPLY_FOG(i.fogCoord, _PaintColor);
                    return _PaintColor;
                }
                else
                {
                    discard;
                    return fixed4(0,0,0,0);
                }
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
} 