Shader "Unlit/Normal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        LOD 100
        Cull Back

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Pragma required to support stereo rendering in VR
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_MULTIVIEW_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
                // Define properties for instancing here (as needed)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Add instance ID as input
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Maintain instance ID
                UNITY_VERTEX_OUTPUT_STEREO // Required for stereo support
            };

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Setup instance and stereo information
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // Setup instance information
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 color;
                color.rgb = input.worldNormal * 0.5h + 0.5h;
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}