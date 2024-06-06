Shader "Hidden/RecaNoMaho/CommonUtil"
{
        HLSLINCLUDE
        // Core.hlsl for XR dependencies
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        SAMPLER(sampler_BlitTexture);

        float4 FragmentBlitAdd(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;

            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

            #ifdef _LINEAR_TO_SRGB_CONVERSION
                col = LinearToSRGB(col);
            #endif

            #if defined(DEBUG_DISPLAY)
                half4 debugColor = 0;

                if(CanDebugOverrideOutputColor(col, uv, debugColor))
                {
                    return debugColor;
                }
            #endif

            return col;
        }

        float4 FragmentBlitBlend(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;

            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

            #ifdef _LINEAR_TO_SRGB_CONVERSION
                col = LinearToSRGB(col);
            #endif

            #if defined(DEBUG_DISPLAY)
                half4 debugColor = 0;

                if(CanDebugOverrideOutputColor(col, uv, debugColor))
                {
                    return debugColor;
                }
            #endif

            return col;
        }

        half FragmentCopyDepth(Varyings input) : SV_Depth
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;

            half depth = SAMPLE_DEPTH_TEXTURE(_BlitTexture, sampler_BlitTexture, uv);

            return depth;
        }
        ENDHLSL
    
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Blit Add"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlitAdd
            ENDHLSL
        }

        Pass
        {
            Name "Blit Blend One SrcAlpha"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One SrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentBlitBlend
            ENDHLSL
        }
        
        Pass
        {
            Name "Copy Depth"
            ZTest Always
            ZWrite On
            Cull Off
            Blend One Zero
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragmentCopyDepth
            ENDHLSL
        }
    }
}