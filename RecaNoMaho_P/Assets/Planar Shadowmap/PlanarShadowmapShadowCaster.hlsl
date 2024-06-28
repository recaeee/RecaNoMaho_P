#ifndef RECANOMAHO_PLANAR_SHADOWMAP_SHADOW_CASTER_INCLUDED
#define RECANOMAHO_PLANAR_SHADOWMAP_SHADOW_CASTER_INCLUDED

#define NORMAL_BIAS

// Needed definitions:
// _PlanarHeight
// _GlobalMaxShadowHeight

#ifndef PLANAR_SHADOWMAP_PARAMS
#define PLANAR_SHADOWMAP_PARAMS
float _PlanarHeight;
float _GlobalMaxShadowHeight;
float2 _PlanarShadowmapShadowBias;
float4x4 _PlanarShadowmapVP;
TEXTURE2D(_PlanarShadowmapTex);SAMPLER_CMP(sampler_PlanarShadowmapTex);
float4 _PlanarShadowmapTex_TexelSize;
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

#include "./PlanarShadowmapUtil.hlsl"

struct PlanarShadowmapShadowCasterAttributes
{
    float4 positionOS : POSITION;
#if defined(NORMAL_BIAS)
    float3 normalOS : NORMAL;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PlanarShadowmapShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

PlanarShadowmapShadowCasterVaryings planarShadowmapShadowCasterVert(PlanarShadowmapShadowCasterAttributes input)
{
    PlanarShadowmapShadowCasterVaryings output = (PlanarShadowmapShadowCasterVaryings)0;
            
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 dirLightDir = normalize(GetMainLight().direction);
#if defined(NORMAL_BIAS)
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float invNdotL = 1.0 - saturate(dot(dirLightDir, normalWS));
    float scale = invNdotL * _PlanarShadowmapShadowBias.y;
    positionWS -= lerp(normalWS * scale.xxx, 0, RemapWorldSpaceHeightToShadowHeight(positionWS.y));
#endif

    //对于WorldSpace，存储摄像机视野内XZ平面上在阴影中的最大高度maxShadowHeight，坐标映射到HClipSpace。
    //求世界空间下投影坐标planarShadowmapPositionWS
    float3 planarShadowmapPositionWS = float3(0, 0, 0);
    planarShadowmapPositionWS.xz = positionWS.xz - dirLightDir.xz * max(0, positionWS.y - _PlanarHeight) / dirLightDir.y;
    planarShadowmapPositionWS.y = _PlanarHeight;
            
    //在阴影中的最大高度maxShadowHeight，归一化到[0,1]
    float maxShadowHeight = RemapWorldSpaceHeightToShadowHeight(positionWS.y);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    maxShadowHeight = 1 - maxShadowHeight;
#endif

    //坐标映射到HClipSpace
    output.positionCS = TransformWorldToHClip(planarShadowmapPositionWS);
    //不能使用以下写法，手动/w会产生问题
    // output.positionCS = float4(output.positionCS.xy / output.positionCS.w, maxShadowHeight, 1);
    //正确写法如下
    output.positionCS.z = maxShadowHeight * output.positionCS.w;
            
    return output;
}

float4 planarShadowmapShadowCasterFrag(PlanarShadowmapShadowCasterVaryings input) : SV_Target
{
    return 1;
}

#endif