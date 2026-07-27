Shader "TryAR/URP/Colored Depth Point Cloud CPU"
{
    Properties
    {
        _CameraTexture ("Camera Texture", 2D) = "black" {}
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
            TEXTURE2D(_CameraTexture);
            SAMPLER(sampler_CameraTexture);

            CBUFFER_START(UnityPerMaterial)
            float _PointAlpha;
            float _PointSize;
            float4 _DepthRange;
            float4x4 _InverseLocalReprojection;
            float4x4 _ColorCameraWorldToLocal;
            float4 _ColorFocalLength;
            float4 _ColorPrincipalPoint;
            float4 _ColorSensorCropRect;
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
                float2 cameraUv : TEXCOORD0;
                float pointSize : PSIZE;
                float valid : TEXCOORD1;
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

            bool TryReconstructLocalPosition(float2 uv, uint vertexID, out float3 localPosition)
            {
                localPosition = 0.0;

                float linearDepth = _LinearDepthBuffer[vertexID];
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

            bool TryProjectCameraUv(float3 worldPosition, out float2 cameraUv)
            {
                cameraUv = 0.0;
                float3 cameraPosition = mul(_ColorCameraWorldToLocal, float4(worldPosition, 1.0)).xyz;
                if (!IsFiniteFloat3(cameraPosition) || cameraPosition.z <= 1e-5)
                {
                    return false;
                }

                float2 sensorPoint = (cameraPosition.xy / cameraPosition.z) * _ColorFocalLength.xy
                    + _ColorPrincipalPoint.xy;
                cameraUv = (sensorPoint - _ColorSensorCropRect.xy) / _ColorSensorCropRect.zw;

                return all(cameraUv >= 0.0) && all(cameraUv <= 1.0);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 localPosition;
                bool isValid = TryReconstructLocalPosition(input.uv, input.vertexID, localPosition);

                float2 cameraUv = 0.0;
                if (isValid)
                {
                    float3 worldPosition = TransformObjectToWorld(localPosition);
                    isValid = TryProjectCameraUv(worldPosition, cameraUv);
                }

                output.cameraUv = cameraUv;
                output.valid = isValid ? 1.0 : 0.0;
                output.pointSize = isValid ? _PointSize : 0.0;
                output.positionCS = isValid
                    ? TransformObjectToHClip(localPosition)
                    : float4(0.0, 0.0, 0.0, 1.0);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.valid - 0.5);
                half4 cameraColor = SAMPLE_TEXTURE2D(_CameraTexture, sampler_CameraTexture, input.cameraUv);
                cameraColor.a *= _PointAlpha;
                return cameraColor;
            }
            ENDHLSL
        }
    }
}
