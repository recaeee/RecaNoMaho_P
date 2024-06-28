#ifndef RECANOMAHO_PLANAR_SHADOWMAP_UTIL_INCLUDED
#define RECANOMAHO_PLANAR_SHADOWMAP_UTIL_INCLUDED

// Needed definitions:
// _PlanarHeight
// _GlobalMaxShadowHeight
// _PlanarShadowmapTex

#ifndef PLANAR_SHADOWMAP_PARAMS
#define PLANAR_SHADOWMAP_PARAMS
float _PlanarHeight;
float _GlobalMaxShadowHeight;
float2 _PlanarShadowmapShadowBias;
float4x4 _PlanarShadowmapVP;
TEXTURE2D(_PlanarShadowmapTex);SAMPLER_CMP(sampler_PlanarShadowmapTex);SAMPLER(sampler_LinearClamp);
float4 _PlanarShadowmapTex_TexelSize;
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.deprecated.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

float4 RemapWorldPositionToPlanarShadowmapCoord(float3 positionWS)
{
    float3 dirLightDir = normalize(GetMainLight().direction);
    float3 planarShadowmapPositionWS = float3(0, 0, 0);
    planarShadowmapPositionWS.xz = positionWS.xz - dirLightDir.xz * max(0, positionWS.y - _PlanarHeight) / dirLightDir.y;
    planarShadowmapPositionWS.y =  _PlanarHeight;

    float4 planarShadowmapPositionCS = mul(_PlanarShadowmapVP, float4(planarShadowmapPositionWS, 1.0));

    float4 planarShadowmapCoord = ComputeScreenPos(planarShadowmapPositionCS);
    return planarShadowmapCoord;
}

float RemapWorldSpaceHeightToShadowHeight(float heightWS)
{
    return (heightWS - _PlanarHeight) / (_GlobalMaxShadowHeight - _PlanarHeight);
}

float MainLightRealtimePlanarShadow(float shadowHeight, float2 planarShadowmapCoord)
{
#if defined(PLANAR_SHADOWMAP_LIT_PASS_PCF2X2)
    if(shadowHeight < 1)
    {
        half4 attenuation4 = 0;
        attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));

        return dot(attenuation4, real(0.25));
    }
    else//高度大于等于有效阴影范围时回退到PointFilter，否则出错
    {
        float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        maxShadowHeight = 1 - maxShadowHeight;
#endif
        return 1 - (shadowHeight <= maxShadowHeight);
    }
#elif defined(PLANAR_SHADOWMAP_LIT_PASS)
    return SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.x, planarShadowmapCoord.y, shadowHeight)).r;
    float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    maxShadowHeight = 1 - maxShadowHeight;
#endif
    return 1 - (shadowHeight < maxShadowHeight);
#else
    return 1;
#endif
}
    


#endif