Shader "TryAR/URP/Depth Point Cloud CPU"
{
    Properties
    {
        _PointAlpha ("Point Alpha", Range(0, 1)) = 0.9
        _PointSize ("Point Size", Float) = 2.0
        _DepthRange ("Depth Range", Vector) = (0.1, 5.0, 0.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float> _LinearDepthBuffer;
            float4 _EnvironmentDepthZBufferParams;

            CBUFFER_START(UnityPerMaterial)
            float _PointAlpha;
            float _PointSize;
            float4 _DepthRange;
            float4x4 _InverseLocalReprojection;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float pointSize : PSIZE;
                float valid : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            bool IsFiniteValue(float value)
            {
                return value == value && abs(value) < 1e8;
            }

            bool IsFiniteFloat3(float3 value)
            {
                return IsFiniteValue(value.x) && IsFiniteValue(value.y) && IsFiniteValue(value.z);
            }

            float3 HsvToRgb(float3 hsv)
            {
                float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + k.xyz) * 6.0 - k.www);
                return hsv.z * lerp(k.xxx, saturate(p - k.xxx), hsv.y);
            }

            bool TryReconstructLocalPosition(float2 uv, uint vertexID, out float3 localPosition, out float linearDepth)
            {
                localPosition = 0.0;
                linearDepth = 0.0;

                linearDepth = _LinearDepthBuffer[vertexID];
                if (linearDepth < _DepthRange.x || linearDepth > _DepthRange.y || !IsFiniteValue(linearDepth))
                {
                    return false;
                }

                float ndcDepth = (_EnvironmentDepthZBufferParams.x / linearDepth) - _EnvironmentDepthZBufferParams.y;
                if (!IsFiniteValue(ndcDepth))
                {
                    return false;
                }

                float4 clipPosition = float4(uv * 2.0 - 1.0, ndcDepth, 1.0);
                float4 localPositionH = mul(_InverseLocalReprojection, clipPosition);
                if (abs(localPositionH.w) < 1e-5)
                {
                    return false;
                }

                localPosition = localPositionH.xyz / localPositionH.w;
                return IsFiniteFloat3(localPosition);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 localPosition;
                float linearDepth;
                bool isValid = TryReconstructLocalPosition(input.uv, input.vertexID, localPosition, linearDepth);

                float depth01 = saturate((linearDepth - _DepthRange.x) / max(_DepthRange.y - _DepthRange.x, 1e-5));
                float hue = lerp(0.1, 0.9, depth01);
                float3 depthColor = HsvToRgb(float3(hue, 1.0, 1.0));

                output.valid = isValid ? 1.0 : 0.0;
                output.color = isValid ? half4(depthColor, _PointAlpha) : half4(0.0, 0.0, 0.0, 0.0);
                output.pointSize = isValid ? _PointSize : 0.0;
                output.positionCS = isValid ? TransformObjectToHClip(localPosition) : float4(0.0, 0.0, 0.0, 1.0);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.valid - 0.5);
                return input.color;
            }
            ENDHLSL
        }
    }
}
