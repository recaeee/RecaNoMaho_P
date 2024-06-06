#ifndef RECANOMAHO_CLOUD_SCAPES_INCLUDED
#define RECANOMAHO_CLOUD_SCAPES_INCLUDED

#include "./VolumetricLightUtils.hlsl"

//参考：https://www.jianshu.com/p/ae1d13bb0d86
//参考：https://github.com/erickTornero/realtime-volumetric-cloudscapes/blob/master/shaders/waterskyfrag.glsl

//Needed declarations
//TEXTURE3D(_CloudNoise3DTextureA);
//TEXTURE3D(_CloudNoise3DTextureB);
//float4 _CloudScale;
float _DensityClip;
float3 _InCloudMin;
float3 _InCloudMax;
float3 _CloudFlowSpeed;
TEXTURE2D(_WeatherDataTexture);
TEXTURE2D(_Noise2DTextureA);
TEXTURE2D(_Noise2DTextureB);
TEXTURE3D(_Noise3DTextureA);
TEXTURE3D(_Noise3DTextureB);

//从一个范围Remap到另一个范围的工具函数
float Remap(float originValue, float originMin, float originMax, float newMin, float newMax)
{
    return newMin + (((originValue - originMin) / (originMax - originMin)) * (newMax - newMin));
}

/**
 * \brief Gradient height function
 * \param height 相对高度，范围[0,1]
 * \param cloudType 云类型
 * \return 
 */
float GetGradientHeightFactor(float height, int cloudType)
{
    float timeWidthUp, startTimeUp, startTimeDown;
    //层云Stratus clouds
    if(cloudType == 0)
    {
        timeWidthUp = 0.08;
        startTimeUp = 0.08;
        startTimeDown = 0.3;
    }
    //积云Cumulus clouds
    else if(cloudType == 1)
    {
        timeWidthUp = 0.14;
        startTimeUp = 0.1;
        startTimeDown = 0.5;
    }
    //积雨云Cumulunimbusclouds
    else if(cloudType == 2)
    {
        timeWidthUp = 0.2;
        startTimeUp = 0.1;
        startTimeDown = 0.7;
    }

    float factor = 2.0 * PI / (2.0 * timeWidthUp);

    float densityGradient = 0.0;
    if(height < startTimeUp)
    {
        densityGradient = 0.0;
    }
    else if(height < startTimeUp + timeWidthUp)
    {
        densityGradient = 0.5 * sin(factor * height - PI / 2.0 - factor * startTimeUp) + 0.5;
    }
    else if(height < startTimeDown)
    {
        densityGradient = 1.0;
    }
    else if(height < startTimeDown + timeWidthUp)
    {
        densityGradient = 0.5 * sin(factor * height - PI / 2.0 - factor * (startTimeDown + timeWidthUp)) + 0.5;
    }
    else
    {
        densityGradient = 0.0;
    }

    return densityGradient;
}

//从density-height函数刻画云层在不同高度上的形状
float GetDensityHeightGradientForPoint(float3 positionWS, float3 weatherData, float heightFraction)
{
    float cloudTypeValue = weatherData.z;
    int cloudType = 1;
    if(cloudTypeValue < 0.1)
    {
        cloudType = 0;
    }
    else if(cloudTypeValue > 0.9)
    {
        cloudType = 2;
    }

    return GetGradientHeightFactor(heightFraction, cloudType);
}

float GetHeightFractionForPoint(float3 inPosition, float2 inCloudMinMax)
{
    float heightFraction = (inPosition.y - inCloudMinMax.x) / (inCloudMinMax.y - inCloudMinMax.x);
    return saturate(heightFraction);
}

float2 GetXZFractionForPoint(float3 inPosition, float4 inCloudMinMax)
{
    float2 xzFraction = 0;
    xzFraction.x = (inPosition.x - inCloudMinMax.x) / (inCloudMinMax.z - inCloudMinMax.x);
    xzFraction.y = (inPosition.z - inCloudMinMax.y) / (inCloudMinMax.w - inCloudMinMax.y);
    return xzFraction;
}


//暂时用算法生成Curl Noise
float Random(float2 st) {
    return frac(sin(dot(st.xy,
        float2(12.9898, 78.233)))*
        43758.5453123);
}

float2 ComputeCurl(float2 st)
{
    float x = st.x; float y = st.y;
    float h = 0.0001;
    float n, n1, n2, a, b;

    n = Random(float2(x, y));
    n1 = Random(float2(x, y - h));
    n2 = Random(float2(x - h, y));
    a = (n - n1) / h;
    b = (n - n2) / h;

    return normalize(float2(a, -b));
}

float SampleCloudDensity(float3 positionWS)
{
    float heightFraction = GetHeightFractionForPoint(positionWS, float2(_InCloudMin.y, _InCloudMax.y));
    //归一化xz用来计算Coverage
    float2 xzFraction = GetXZFractionForPoint(positionWS, float4(_InCloudMin.xz, _InCloudMax.xz));
    float3 uvw = positionWS * _CloudScale.xyz;
    uvw += float3(_Time.xxx * _CloudFlowSpeed);
    //采样Perlin-worley and Worley noises
    float4 lowFrequencyNoises = SAMPLE_TEXTURE3D(_CloudNoise3DTextureA, sampler_LinearRepeat, uvw).rgba;
    //gba通道再次FBM处理，用来刻画细节
    float lowFrequencyFbm = 0.625f * lowFrequencyNoises.g + 0.25f * lowFrequencyNoises.b + 0.125f * lowFrequencyNoises.a;
    //使用lowFrequencyFbm对Perlin-Worley进行膨胀，塑造云层基本形状
    float baseCloud = Remap(lowFrequencyNoises.r, -(1.0 - lowFrequencyFbm), 1.0, 0.0, 1.0);
    
    //塑造云层在不同高度下的密度
    //在我的舞台场景下并不需要严格按照真实云层的高度-密度梯度去塑造云在不同高度下的形状，但这里保留这种写法，可以让该方法更通用。
    //weatherData:r->Coverage g->Precipitation b->CloudType
    float3 weatherData = SAMPLE_TEXTURE2D(_WeatherDataTexture, sampler_LinearRepeat, xzFraction).rgb;
    //暂时先不使用Cloud Type分布
    weatherData.z = 0;
    //给heightFraction增加点扰动，增加起伏效果（正好利用Worley Noise）
    heightFraction += (2 * weatherData.r - 1) * 0.03;
    //再手动Clip下
    weatherData.r += 0.25;
    
    float densityHeightGradient = GetDensityHeightGradientForPoint(positionWS, weatherData, heightFraction);
    baseCloud *= densityHeightGradient;
    
    //使用云层覆盖率的贴图来Clip
    float cloudCoverage = clamp(weatherData.r, 0, 0.999);
    float baseCloudWithCoverage = Remap(baseCloud, cloudCoverage, 1.0, 0.0, 1.0);
    //To ensure that density increases with coverage in an aesthetically pleasing way, we multiply this result by the cloud coverage attribute.
    baseCloudWithCoverage *= cloudCoverage;

    float finalCloud = baseCloudWithCoverage;

    if(baseCloudWithCoverage > 0.0)
    {
        //使用高频FBM噪声对云的边缘增加细节
        //TODO:Curl替换成离线的噪声图
        float2 curlNoise = ComputeCurl(uvw);
        //云的底部增加湍流效果
        uvw.xz += curlNoise.xy * (1.0 - heightFraction);
        float3 highFrequencyNoises = SAMPLE_TEXTURE3D(_CloudNoise3DTextureB, sampler_LinearRepeat, uvw).rgb;
        //构建FBM
        float highFrequencyFbm = 0.625f * highFrequencyNoises.r + 0.25f * highFrequencyNoises.g + 0.125f * highFrequencyNoises.b;
        float highFreqNoiseModifier = lerp(highFrequencyFbm, 1.0 - highFrequencyFbm, saturate(heightFraction * 10));
        //侵蚀（收缩）云的形状产生细节
        finalCloud = Remap(baseCloudWithCoverage, highFreqNoiseModifier * 0.2, 1.0, 0.0, 1.0);
    }
    
    return saturate(finalCloud);
}

#endif