#ifndef RECANOMAHO_PLANAR_SHADOWMAP_LIT_PASS_INCLUDED
#define RECANOMAHO_PLANAR_SHADOWMAP_LIT_PASS_INCLUDED

// Needed definitions:
// _PlanarHeight
// _GlobalMaxShadowHeight
// _PlanarShadowmapTex
// _PlanarShadowmapShadowBias

#ifndef PLANAR_SHADOWMAP_PARAMS
#define PLANAR_SHADOWMAP_PARAMS
float _PlanarHeight;
float _GlobalMaxShadowHeight;
float2 _PlanarShadowmapShadowBias;
float4x4 _PlanarShadowmapVP;
TEXTURE2D(_PlanarShadowmapTex)SAMPLER_CMP(sampler_PlanarShadowmapTex);
float4 _PlanarShadowmapTex_TexelSize;
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

#include "./PlanarShadowmapUtil.hlsl"

float4 _BaseColor;

struct PlanarShadowmapLitPassAttributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PlanarShadowmapLitPassVaryings
{
    float4 positionCS : SV_POSITION;
    float4 planarShadowCoord : TEXCOORD0;
    float shadowHeight : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

PlanarShadowmapLitPassVaryings planarShadowmapLitPassVert(PlanarShadowmapLitPassAttributes input)
{
    PlanarShadowmapLitPassVaryings output = (PlanarShadowmapLitPassVaryings)0;
            
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    float3 positionWS = TransformObjectToWorld(positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.planarShadowCoord = RemapWorldPositionToPlanarShadowmapCoord(positionWS);
    output.shadowHeight = saturate(RemapWorldSpaceHeightToShadowHeight(positionWS.y));
            
    return output;
}

float4 planarShadowmapLitPassFrag(PlanarShadowmapLitPassVaryings input) : SV_Target
{
    float2 shadowCoord = input.planarShadowCoord.xy / input.planarShadowCoord.w;
    return _BaseColor * MainLightRealtimePlanarShadow(input.shadowHeight + _PlanarShadowmapShadowBias.x, shadowCoord);
}

#endif