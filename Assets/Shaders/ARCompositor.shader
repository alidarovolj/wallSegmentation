Shader "Custom/ARCompositor"
{
    Properties
    {
        [HideInInspector] _MainTex("Texture", 2D) = "white" {} // Hidden but required by ARCameraBackground
        _PaintColor("Paint Color", Color) = (1, 0, 0, 1)
        _SegmentationTex("Segmentation Texture (ID map)", 2D) = "black" {}
        _TargetClassID("Target Class ID", Float) = -1
        _NumClasses("Number of Classes", Float) = 21
        [Toggle] _PaintEnabled("Paint Enabled?", Float) = 0 // Our main "on/off" switch
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }

        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SegmentationTex);
            SAMPLER(sampler_SegmentationTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _PaintColor;
                float _TargetClassID;
                float _NumClasses;
                float _PaintEnabled; // Add the switch here
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 cameraColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // If painting is disabled OR no class is targeted, just show the camera feed.
                if (_PaintEnabled < 0.5 || _TargetClassID < 0)
                {
                    return cameraColor;
                }

                half segmentValue = SAMPLE_TEXTURE2D(_SegmentationTex, sampler_SegmentationTex, IN.uv).r;
                int classID = (int)round(segmentValue * _NumClasses);

                if (classID == (int)_TargetClassID)
                {
                    // Blend the paint color with the camera feed for a more integrated look.
                    return lerp(cameraColor, _PaintColor, 0.7h);
                }

                return cameraColor;
            }
            ENDHLSL
        }
    }
} 