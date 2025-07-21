Shader "Unlit/SurfacePaintShader"
{
    Properties
    {
        _PaintColor ("Paint Color", Color) = (1, 0, 0, 0.5)
        _SegmentationTex ("Segmentation Texture", 2D) = "white" {}
        _CameraFeedTex ("Camera Feed Texture", 2D) = "white" {}
        _TargetClassID ("Target Class ID", Float) = 0
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
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            sampler2D _SegmentationTex;
            sampler2D _CameraFeedTex;
            float4 _PaintColor;
            float _TargetClassID;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert screen position to UV coordinates
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Sample segmentation texture to get the class ID
                // Note: We'll need to pass the segmentation data here.
                // This is a simplified example. The real implementation will be more complex.
                // For now, let's assume we have a way to read the class ID.
                float classID = _TargetClassID; // Placeholder

                // Sample the real-world color from the camera feed
                fixed4 cameraColor = tex2D(_CameraFeedTex, screenUV);
                
                // If the class ID at this pixel matches our target, paint it
                if (classID == _TargetClassID)
                {
                    // Blend the paint color with the camera color
                    // 'Multiply' blending mode often gives a nice, realistic tint
                    fixed3 blendedColor = cameraColor.rgb * _PaintColor.rgb;
                    return fixed4(blendedColor, _PaintColor.a);
                }
                else
                {
                    // If it's not the target class, make it fully transparent
                    return fixed4(0, 0, 0, 0);
                }
            }
            ENDCG
        }
    }
} 