Shader "TryAR/URP/Depth Point Cloud"
{
    Properties
    {
        _PointColor ("Point Color", Color) = (0.15, 0.85, 1.0, 0.9)
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

            TEXTURE2D_ARRAY_FLOAT(_EnvironmentDepthTexture);
            SAMPLER(sampler_EnvironmentDepthTexture);
            float4 _EnvironmentDepthZBufferParams;

            CBUFFER_START(UnityPerMaterial)
            float4 _PointColor;
            float _PointSize;
            float4 _DepthRange;
            float4x4 _InverseLocalReprojection;
            float _DepthEyeIndex;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
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

            bool TryReconstructLocalPosition(float2 uv, out float3 localPosition)
            {
                localPosition = 0.0;

                float rawDepth = SAMPLE_TEXTURE2D_ARRAY_LOD(
                    _EnvironmentDepthTexture,
                    sampler_EnvironmentDepthTexture,
                    uv,
                    _DepthEyeIndex,
                    0).r;

                if (rawDepth <= 0.0)
                {
                    return false;
                }

                float ndcDepth = rawDepth * 2.0 - 1.0;
                float linearDepth = (1.0 / (ndcDepth + _EnvironmentDepthZBufferParams.y)) * _EnvironmentDepthZBufferParams.x;

                if (linearDepth < _DepthRange.x || linearDepth > _DepthRange.y || !IsFiniteValue(linearDepth))
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
                bool isValid = TryReconstructLocalPosition(input.uv, localPosition);

                output.valid = isValid ? 1.0 : 0.0;
                output.color = _PointColor;
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
