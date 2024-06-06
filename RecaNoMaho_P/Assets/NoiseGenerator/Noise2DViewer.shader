Shader "RecaNoMaho/Noises/Noise2DViewer"
{
    Properties
    {
        _BaseMap ("_BaseMap", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);float4 _BaseMap_ST;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionOS = input.positionOS.xyz;
            VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
            output.positionCS = vertexInput.positionCS;
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

            return output;
        }

        float4 frag(Varyings input) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(input);

            float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rrrr;

            return color;
        }
        ENDHLSL

        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}