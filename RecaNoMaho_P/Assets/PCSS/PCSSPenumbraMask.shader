Shader "Hidden/RecaNoMaho/PCSSPenumbraMask"
{
    Properties
    {
        
    }
    
    
    HLSLINCLUDE
    
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    static const float2 offset[4] = {float2(-1, 1), float2(1, 1), float2(-1, -1), float2(1, -1)};
    
    float PcssPenumbraMaskFrag(Varyings input) : SV_TARGET
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float shadowAttenuation = 0;

        for(int i = 0; i < 4; ++i)
        {
            float2 coord = input.texcoord.xy + offset[i].xy * _ColorAttachmentTexelSize.xy;
            
            #if UNITY_REVERSED_Z
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, coord.xy).r;
            #else
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, coord.xy).r;
                deviceDepth = deviceDepth * 2.0 - 1.0;
            #endif
            
            float3 positionWS = ComputeWorldSpacePosition(coord.xy, deviceDepth, unity_MatrixInvVP);
            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);

            //注意考虑deviceDepth=0的情况下，我们认为shadowAttenuation为1。
            shadowAttenuation += 0.25f * lerp(1, SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, shadowCoord.xyz), step(Eps_float(), deviceDepth));
        }
        
        return shadowAttenuation < Eps_float() || shadowAttenuation > 1 - Eps_float() ? 0 : 1;
    }

    float PcssBlurHorizontalFrag(Varyings input) : SV_TARGET
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float texelSize = _PenumbraMaskTex_TexelSize.x * 2.0;
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

        // 9-tap gaussian blur on the downsampled source
        float m0 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(texelSize * 4.0, 0.0)).r;
        float m1 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(texelSize * 3.0, 0.0)).r;
        float m2 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(texelSize * 2.0, 0.0)).r;
        float m3 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(texelSize * 1.0, 0.0)).r;
        float m4 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv).r;
        float m5 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(texelSize * 1.0, 0.0)).r;
        float m6 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(texelSize * 2.0, 0.0)).r;
        float m7 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(texelSize * 3.0, 0.0)).r;
        float m8 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(texelSize * 4.0, 0.0)).r;

        float result =  m0 * 0.01621622 + m1 * 0.05405405 + m2 * 0.12162162 + m3 * 0.19459459
                        + m4 * 0.22702703
                        + m5 * 0.19459459 + m6 * 0.12162162 + m7 * 0.05405405 + m8 * 0.01621622;

        return result;
    }

    float PcssBlurVerticalFrag(Varyings input) : SV_TARGET
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float texelSize = _PenumbraMaskTex_TexelSize.y;
        float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

        // Optimized bilinear 5-tap gaussian on the same-sized source (9-tap equivalent)
        float m0 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(0.0, texelSize * 3.23076923)).r;
        float m1 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv - float2(0.0, texelSize * 1.38461538)).r;
        float m2 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv).r;
        float m3 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(0.0, texelSize * 1.38461538)).r;
        float m4 = SAMPLE_TEXTURE2D_X(_PenumbraMaskTex, sampler_LinearClamp, uv + float2(0.0, texelSize * 3.23076923)).r;

        float result =  m0 * 0.07027027 + m1 * 0.31621622
                        + m2 * 0.22702703
                        + m3 * 0.31621622 + m4 * 0.07027027;

        return result;
    }
    
    ENDHLSL
    
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "Pcss Penumbra Mask"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM

            #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex Vert
            #pragma fragment PcssPenumbraMaskFrag
            
            ENDHLSL
        }
        
        Pass
        {
            Name "Pcss Blur Horizontal"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment PcssBlurHorizontalFrag
            
            ENDHLSL
        }
        
        Pass
        {
            Name "Pcss Blur Vertical"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment PcssBlurVerticalFrag
            
            ENDHLSL
        }
    }
}