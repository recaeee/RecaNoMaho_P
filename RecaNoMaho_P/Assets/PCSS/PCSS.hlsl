#ifndef RECANOMAHO_PCSS_INCLUDED
#define RECANOMAHO_PCSS_INCLUDED

// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
// #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "PCSSParams.hlsl"

float BlockerSearchRadius(float receiverDepth, float depth2RadialScale, float maxSampleZDistance, float minFilterRadius)
{
    #if UNITY_REVERSED_Z
        return max(min(1.0 - receiverDepth, maxSampleZDistance) * depth2RadialScale, minFilterRadius);
    #else
        return max(min(receiverDepth, maxSampleZDistance) * depth2RadialScale, minFilterRadius);
    #endif
}

//FibonacciSpiralDisk随机采样，Sample点更集中在中心，更适合给FindBlocker用
float2 ComputeFibonacciSpiralDiskSampleClumped(const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent,
                                out float sampleDistNorm)
{
    sampleDistNorm = (float)sampleIndex * sampleCountInverse;

    sampleDistNorm = PositivePow(sampleDistNorm, clumpExponent);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}

//使用FibonacciSpiralDisk做随机采样，它是一个Uniform的数组，元素只代表偏移的方向，而偏移的距离通过sampleIndex和sampleCount确定
float2 ComputeFibonacciSpiralDiskSampleUniform(const in int sampleIndex, const in float sampleCountInverse, const in float sampleBias,
                                out float sampleDistNorm)
{
    //MAD指令
    sampleDistNorm = (float)sampleIndex * sampleCountInverse + sampleBias;

    sampleDistNorm = sqrt(sampleDistNorm);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}

float2 ComputeFindBlockerSampleOffset(const in float filterRadius, const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent,
                                const in float2 sampleJitter, const in float2 shadowmapInAtlasScale,
                                out float sampleDistNorm)
{
    float2 offset = ComputeFibonacciSpiralDiskSampleClumped(sampleIndex, sampleCountInverse, clumpExponent, sampleDistNorm);
    //增加Temporal Jitter
    offset = float2(offset.x * sampleJitter.y + offset.y * sampleJitter.x,
                        offset.x * -sampleJitter.x + offset.y * sampleJitter.y);
    //应用SearchRadius，SearchRadius已经经过depth2Radial转换，即基于角直径，不再考虑TexelSize
    offset *= filterRadius;
    //应用shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5，角直径是基于单个Tile的，因此要考虑Atlas上的缩放
    offset *= shadowmapInAtlasScale;

    return offset;
}

float2 ComputePcfSampleOffset(const in float filterSize, const in float samplingFilterSize, const in int sampleIndex, const in float sampleCountInverse,
    const in float sampleCountBias, const in float2 sampleJitter, const in float2 shadowmapInAtlasScale, const in float radial2DepthScale,
    float maxPcssOffset, out float zOffset)
{
    #if UNITY_REVERSED_Z
    #define Z_OFFSET_DIRECTION 1
    #else
    #define Z_OFFSET_DIRECTION (-1)
    #endif
    
    float sampleDistNorm;
    float2 offset = ComputeFibonacciSpiralDiskSampleUniform(sampleIndex, sampleCountInverse, sampleCountBias, sampleDistNorm);
    //增加Temporal Jitter
    offset = float2(offset.x * sampleJitter.y + offset.y * sampleJitter.x,
                    offset.x * -sampleJitter.x + offset.y * sampleJitter.y);
    //应用Penumbra评估得到的FilterSize
    offset *= samplingFilterSize;
    //应用shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
    offset *= shadowmapInAtlasScale;

    zOffset = min(filterSize * sampleDistNorm * radial2DepthScale, maxPcssOffset) * Z_OFFSET_DIRECTION;
    
    return offset;
}

float2 ComputePcfSampleJitter(float2 pixCoord, uint frameIndex)
{
    float sampleJitterAngle = InterleavedGradientNoise(pixCoord, frameIndex) * 2.0 * PI;
    return float2(sin(sampleJitterAngle), cos(sampleJitterAngle));
}

float FindBlocker(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float2 shadowCoord, float receiverDepth, float searchRadius,
    float2 minCoord, float2 maxCoord, int sampleCount, float clumpExponent,
    float2 shadowmapInAtlasScale, float2 sampleJitter, float minFilterRadius, float minFilterRadial2DepthScale,
    float radial2DepthScale)
{
#if UNITY_REVERSED_Z
    #define Z_OFFSET_DIRECTION 1
#else
    #define Z_OFFSET_DIRECTION (-1)
#endif
    float depthSum = 0.0;
    float depthCount = 0.0;
    float sampleCountInverse = rcp((float)sampleCount);

    for(int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; ++i)
    {
        float sampleDistNorm;
        float2 offset = ComputeFindBlockerSampleOffset(searchRadius, i, sampleCountInverse, clumpExponent, sampleJitter, shadowmapInAtlasScale, sampleDistNorm);
        float2 sampleCoord = shadowCoord + offset;
        float sampleDepth = SAMPLE_TEXTURE2D_LOD(ShadowMap, sampler_ShadowMap, sampleCoord, 0.0).r;

        //对阴影接受物的Z做Cone Base偏移，这对于消除自遮挡很重要
        float radialOffset = searchRadius * sampleDistNorm;
        float zOffset = radialOffset * (radialOffset < minFilterRadius ? minFilterRadial2DepthScale : radial2DepthScale);
        float receiverDepthWithOffset = receiverDepth + (Z_OFFSET_DIRECTION) * zOffset;

        if(!(any(sampleCoord < minCoord) || any(sampleCoord > maxCoord)) &&
#if UNITY_REVERSED_Z
            (sampleDepth > receiverDepthWithOffset)
#else
            (sampleDepth < receiverDepthWithOffset)
#endif
            )
        {
            depthSum += sampleDepth;
            depthCount += 1.0;
        }
    }

    return depthCount > FLT_EPS ? (depthSum / depthCount) : 0;
}

float EstimatePenumbra(float receiverDepth, float avgBlockerDepth, float depth2RadialScale, float maxSampleZDistance,
                        float minFilterRadius, out float filterSize, out float blockerDistance)
{
    if(avgBlockerDepth < Eps_float())
    {
        return 0;
    }
    else
    {
        blockerDistance = min(abs(avgBlockerDepth - receiverDepth) * 0.9, maxSampleZDistance);
        filterSize = blockerDistance * depth2RadialScale;
        return max(filterSize, minFilterRadius);
    }
}

float PCF(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float samplingFilterSize, int sampleCount, 
                                float2 shadowmapInAtlasScale, float2 sampleJitter, float2 minCoord, float2 maxCoord,
                                float radial2DepthScale, float filterSize, float maxPcssOffset)
{
    float shadowAttenuationSum = 0.0;
    float sampleSum = 0.0;
    
    float sampleCountInverse = rcp((float)sampleCount);
    float sampleCountBias = 0.5 * sampleCountInverse;
    
    for(int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; ++i)
    {
        float zOffset;
        float2 offset = ComputePcfSampleOffset(filterSize, samplingFilterSize, i, sampleCountInverse,
            sampleCountBias, sampleJitter, shadowmapInAtlasScale, radial2DepthScale,
            maxPcssOffset, zOffset);
        //对阴影接受物的Z做Cone Base偏移，这对于消除自遮挡很重要
        float3 sampleCoord = (shadowCoord.xyz + float3(offset, zOffset));
        
        //只有sampleCoord不超出Tile时执行采样
        if(!(any(sampleCoord < minCoord) || any(sampleCoord > maxCoord)))
        {
            shadowAttenuationSum += SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, sampleCoord.xyz);
            sampleSum += 1.0;
        }
    }

    return shadowAttenuationSum / sampleSum;
}

//For forward shading
float ForwardPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float4 shadowMapSize)
{
    
    int cascadeIndex = (int)shadowCoord.w;
    float receiverDepth = shadowCoord.z;
    //当前片元所处Tile的边界
    //shadowmapInAtlasOffset=当前Tile在ShadowAtlas上的起始点，范围[0,1]
    float2 shadowmapInAtlasOffset = _CascadeOffsetScales[cascadeIndex].xy;
    //shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
    float2 shadowmapInAtlasScale = _CascadeOffsetScales[cascadeIndex].zw;
    float2 minCoord = shadowmapInAtlasOffset;
    float2 maxCoord = shadowmapInAtlasOffset + shadowmapInAtlasScale;
    float2 sampleJitter = float2(1.0, 1.0);//在这里偷懒，前向不jitter了

    //单个Tile的Texel大小(1/2048)
    float texelSize = shadowMapSize.x / shadowmapInAtlasScale.x;
        
    float depth2RadialScale = _DirLightPcssParams0[cascadeIndex].x;
    float radial2DepthScale = _DirLightPcssParams0[cascadeIndex].y;
    float maxBlokcerDistance = _DirLightPcssParams0[cascadeIndex].z;
    float maxSamplingDistance = _DirLightPcssParams0[cascadeIndex].w;
    float minFilterRadius = texelSize * _DirLightPcssParams1[cascadeIndex].x;
    float minFilterRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].y;
    float blockerRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].z;
    float blockerClumpSampleExponent = _DirLightPcssParams1[cascadeIndex].w;
    float maxPcssOffset = maxSamplingDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
    float maxSampleZDistance = maxBlokcerDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
    
    //计算遮挡物平均深度值
    float blockerSearchRadius = BlockerSearchRadius(receiverDepth, depth2RadialScale, maxSamplingDistance, minFilterRadius);
    float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius,
        minCoord, maxCoord, _FindBlockerSampleCount, blockerClumpSampleExponent,
        shadowmapInAtlasScale, sampleJitter, minFilterRadius, minFilterRadial2DepthScale,
        blockerRadial2DepthScale);
    //评估PCF范围
    float filterSize, blockerDistance;
    float samplingFilterSize = EstimatePenumbra(receiverDepth, avgBlockerDepth, depth2RadialScale, maxSampleZDistance,
        minFilterRadius, filterSize, blockerDistance);
    maxPcssOffset = min(maxPcssOffset, blockerDistance * 0.25f);
    //PCF采样
    return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingFilterSize, _PcfSampleCount,
        shadowmapInAtlasScale, sampleJitter, minCoord, maxCoord,
        minFilterRadial2DepthScale, filterSize, maxPcssOffset);
}

//For deferred shading
float DeferredPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float4 shadowMapSize, float2 screenCoord)
{
    float penumbraMask = SAMPLE_TEXTURE2D(_PenumbraMaskTex, sampler_LinearClamp, screenCoord).r;
    if(penumbraMask > Eps_float())
    {
        int cascadeIndex = (int)shadowCoord.w;
        float receiverDepth = shadowCoord.z;
        //当前片元所处Tile的边界
        //shadowmapInAtlasOffset=当前Tile在ShadowAtlas上的起始点，范围[0,1]
        float2 shadowmapInAtlasOffset = _CascadeOffsetScales[cascadeIndex].xy;
        //shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
        float2 shadowmapInAtlasScale = _CascadeOffsetScales[cascadeIndex].zw;
        float2 minCoord = shadowmapInAtlasOffset;
        float2 maxCoord = shadowmapInAtlasOffset + shadowmapInAtlasScale;

        //单个Tile的Texel大小(1/2048)
        float texelSize = shadowMapSize.x / shadowmapInAtlasScale.x;
        
        float depth2RadialScale = _DirLightPcssParams0[cascadeIndex].x;
        float radial2DepthScale = _DirLightPcssParams0[cascadeIndex].y;
        float maxBlokcerDistance = _DirLightPcssParams0[cascadeIndex].z;
        float maxSamplingDistance = _DirLightPcssParams0[cascadeIndex].w;
        float minFilterRadius = texelSize * _DirLightPcssParams1[cascadeIndex].x;
        float minFilterRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].y;
        float blockerRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].z;
        float blockerClumpSampleExponent = _DirLightPcssParams1[cascadeIndex].w;
        float maxPcssOffset = maxSamplingDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
        float maxSampleZDistance = maxBlokcerDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
        
        //计算Temporal Jitter
        float2 sampleJitter = ComputePcfSampleJitter(screenCoord, (uint)_PcssTemporalFilter);
        
        //计算遮挡物平均深度值
        float blockerSearchRadius = BlockerSearchRadius(receiverDepth, depth2RadialScale, maxSamplingDistance, minFilterRadius);
        float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius,
            minCoord, maxCoord, _FindBlockerSampleCount, blockerClumpSampleExponent,
            shadowmapInAtlasScale, sampleJitter, minFilterRadius, minFilterRadial2DepthScale,
            blockerRadial2DepthScale);
        //评估PCF范围
        float filterSize, blockerDistance;
        float samplingFilterSize = EstimatePenumbra(receiverDepth, avgBlockerDepth, depth2RadialScale, maxSampleZDistance,
            minFilterRadius, filterSize, blockerDistance);
        maxPcssOffset = min(maxPcssOffset, blockerDistance * 0.25f);
        //PCF采样
        return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingFilterSize, _PcfSampleCount,
            shadowmapInAtlasScale, sampleJitter, minCoord, maxCoord,
            minFilterRadial2DepthScale, filterSize, maxPcssOffset);
    }
    else
    {
        return SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord);
    }
}

#endif