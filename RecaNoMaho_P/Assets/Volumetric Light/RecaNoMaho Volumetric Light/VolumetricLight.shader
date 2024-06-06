Shader "Hidden/RecaNoMaho/VolumetricLight"
{
    Properties
    {
        
    }
    
    HLSLINCLUDE

    #define _ADDITIONAL_LIGHT_SHADOWS

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

    TEXTURE3D(_CloudNoise3DTextureA);
    TEXTURE3D(_CloudNoise3DTextureB);
    float4 _CloudScale;
    #include "./CloudScapes.hlsl"
    #include "./VolumetricLightUtils.hlsl"

    // TEXTURE2D(_CameraColorTexture);
    TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
    TEXTURE2D(_BlueNoiseTexture);

    

    int _BoundaryPlanesCount;
    float4 _BoundaryPlanes[6]; // Ax + By + Cz + D = 0; _BoundaryPlane[i] = (A, B, C, D); normal = (A, B, C)
    int _Steps;
    
    float4 _LightPosition;
    float4 _LightDirection;
    float4 _LightColor;
    float _LightCosHalfAngle;
    int  _ApplyShadow;
    int _ShadowLightIndex;

    float4 _ScatteringExtinction;
    float4 _EmissionPhaseG;
    
    float4 _RenderExtent;
    float4 _BlueNoiseTexture_TexelSize;

    float4 _CameraPackedInfo;
    
    float _ShadowIntensity;

    #define TAN_FOV_VERTICAL _CameraPackedInfo.x
    #define TAN_FOV_HORIZONTAL _CameraPackedInfo.y

    #define Scattering _ScatteringExtinction.xyz
    #define Extinction _ScatteringExtinction.w

    #define Emission _EmissionPhaseG.xyz
    #define PhaseG _EmissionPhaseG.w

    #define DensityScale _DensityScaleAndFlowSpeed.xyz
    #define DensityFlowSpeed _DensityScaleAndFlowSpeed.w

    #define DensityIntensity _DensityIntensityAndHeightAttenuation.x
    #define DensityHeightAttenuation _DensityIntensityAndHeightAttenuation.y

    #define SampleAccumulatedCloudDensityStepCount 6
    #define SampleAccumulatedCloudDensityDistance 6

    struct Attributes
    {
        float4 positionOS   : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS   : SV_POSITION;
        float3 positionWS   : TEXCOORD0;
        float3 screenUV     : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    
    //返回射线起点到平面的距离，projection为射线到平面的投影
    float intersectPlane(float4 plane, float3 origin, float3 dir, out float projection)
    {
        projection = dot(dir, plane.xyz);
        return -dot(float4(origin.xyz, 1), plane) / projection;
    }

    //计算视线与RayMarching包围盒交点near & far、经过的光学深度（光线经过的距离）depth
    float computeIntersect(float3 viewRay, out float nearIntersect, out float farIntersect)
    {
        nearIntersect = _ProjectionParams.y; // near Plane
        farIntersect = _ProjectionParams.z; // far Plane

        for(int i = 0; i < _BoundaryPlanesCount; i++)
        {
            float projection;
            float depth = intersectPlane(_BoundaryPlanes[i], _WorldSpaceCameraPos, viewRay, projection);
            //如果当前plane是front face则更新nearIntersect
            // TODO: 优化判断
            if(projection < 0)
            {
                nearIntersect = max(nearIntersect, depth);
            }
            else if (projection > 0)
            {
                farIntersect = min(farIntersect, depth);
            }
        }

        return farIntersect - nearIntersect;
    }

    //计算viewRay原点（摄像机）到实际（物体）片元的距离
    float computeDepthFromCameraToRealFragment(float2 screenUV, float depth)
    {
        //比较笨但易于理解的求法
        //Linear Eye Depth为View空间下的深度值
        float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
        float realFragmentX = linearDepth * TAN_FOV_HORIZONTAL * abs(2 * screenUV.x - 1);
        float realFragmentY = linearDepth * TAN_FOV_VERTICAL * abs(2 * screenUV.y - 1);
        return sqrt(realFragmentX * realFragmentX + realFragmentY * realFragmentY + linearDepth * linearDepth);
        //TODO:更快的算法
        // float2 p = (screenUV * 2 - 1) * float2(TAN_FOV_HORIZONTAL, TAN_FOV_VERTICAL);
        // float3 ray = float3(p.xy, 1);
        // return linearDepth * length(ray);
    }

    //噪声
    float SampleNoise(float3 positionWS)
    {
        float noise = 0;

        float2 noise2DUV = float2(positionWS.xz);
        noise2DUV += _Time.y;
        noise += SAMPLE_TEXTURE2D(_Noise2DTextureA, sampler_LinearRepeat, noise2DUV) * 0.55;

        float3 noise3DUV = positionWS;
        noise3DUV += _Time.y;
        noise += SAMPLE_TEXTURE2D(_Noise3DTextureA, sampler_LinearRepeat, noise3DUV) * 0.25;
    }

    //介质中x处的消光系数（吸收和外散射事件），k为风格化系数
    float extinctionAt(float3 positionWS, float density)
    {
        return Extinction * density;
        //也可以采样3D Texture代表非均匀介质
        //也可以基于解析函数实时计算
    }

    //参与介质的密度越大，其中的粒子就越多，更多的粒子就会导致更多的散射
    float3 scatteringAt(float3 positionWS, float density)
    {
        return Scattering * density;
    }

    float phaseGAt(float3 positionWS, float density)
    {
        return PhaseG;
    }

    float3 emissionAt(float3 positionWS, float density)
    {
        return Emission;
    }
    
    float SpotLightRealtimeShadow(int lightIndex, float3 positionWS)
    {
        ShadowSamplingData shadowSamplingData = GetAdditionalLightShadowSamplingData(lightIndex);

        half4 shadowParams = GetAdditionalLightShadowParams(lightIndex);

        int shadowSliceIndex = shadowParams.w;
        if ( shadowSliceIndex < 0)
            return 1.0;

        float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[shadowSliceIndex], float4(positionWS, 1.0));

        return SampleShadowmap(TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_LinearClampCompare), shadowCoord, shadowSamplingData, shadowParams, true);
    }

    //返回1表示没有阴影，0表示完全在阴影中
    float shadowAt(float3 positionWS)
    {
        if(_ApplyShadow == 0)
        {
            return 1;
        }
        return SpotLightRealtimeShadow(_ShadowLightIndex, positionWS);
    }

    //PhaseFunction，给定入射光线方向，根据概率分布，计算指定方向上的散射光线RGB，体积积分总是为1
    float3 Phase(float3 lightDir, float3 viewDir, float3 positionWS, float density)
    {
        // 采用Henyey-Greenstein phase function(即HG Phase)模拟米氏散射，即介质中微粒与入射光线波长的相对大小接近相等，模拟丁达尔效应时的情况
        float phaseG = phaseGAt(positionWS, density);
        return ( 1 - phaseG * phaseG) / ( 4 * PI * pow(1 + phaseG * phaseG- 2 * phaseG * dot(viewDir, lightDir) , 1.5));
    }

    //为了实现云的自阴影，需要从步进点向光源方向二次步进累积，计算positionWS接收到来自光源的irradiance，虽然费，但是效果好。
    float SampleAccumulatedCloudIrradiance(float3 positionWS, float3 lightDir, float3 viewDir)
    {
        float stepSize = SampleAccumulatedCloudDensityDistance / SampleAccumulatedCloudDensityStepCount;
        float cloudIrradianceMultiDepth = 0;
        for(int i = 0; i < SampleAccumulatedCloudDensityStepCount; i++)
        {
            float3 pos = positionWS + lightDir * stepSize * i;
            cloudIrradianceMultiDepth += extinctionAt(positionWS, SampleCloudDensity(pos)) * stepSize;
        }

        //只有当视线和光源方向一致时我们才使用BeerPowder
        //另外这里其实没有做到能量守恒，代码并不严谨
        if(dot(_LightDirection.xyz, viewDir) > 0)
        {
            return BeerPowder(cloudIrradianceMultiDepth, SampleAccumulatedCloudDensityDistance);
        }
        else
        {
            return BeerLambert(cloudIrradianceMultiDepth, SampleAccumulatedCloudDensityDistance);
        }
    }

    //可见性函数v，代表从光源位置到达采样点positionWS的比例
    float GetLightVisible(float3 positionWS, float3 lightDir, float3 viewDir)
    {
        //聚光灯衰减项
        float spotAttenuation = step(_LightCosHalfAngle, dot(lightDir, _LightDirection.xyz));
        //shadowmap阴影
        float shadowmapAttenuation = shadowAt(positionWS);
        //考虑体积阴影项，从光源到采样点的透光率Transmittance
        float transmittance = SampleAccumulatedCloudIrradiance(positionWS, lightDir, viewDir);
        //不考虑体积阴影项（即使是聚光灯，我们暂时也先不考虑距离衰减）
        // float transmittance = BeerLambert(extinctionAt(positionWS, 1), SampleAccumulatedCloudDensityDistance);

        return spotAttenuation * shadowmapAttenuation * transmittance;
    }

    //返回介质中x处接收到的光线（RGB）
    float3 scatteredLight(float3 positionWS, float3 viewDir, float density)
    {
        //_LightPosition.w = 0时，为方向光，此时_LightPosition.xyx为方向光dir
        //_LightPosition.w = 1时，为SpotLight
        float3 lightDir = normalize(_LightPosition.xyz - positionWS * _LightPosition.w);
        //考虑Phase Function、可见性函数v、光源强度
        float3 radiance = PI * Phase(-lightDir, -viewDir, positionWS, density) * GetLightVisible(positionWS, lightDir, viewDir) * _LightColor.rgb;
        //考虑自发光、风格化_ShadowIntensity
        radiance += emissionAt(positionWS, density) * step(_LightCosHalfAngle, dot(lightDir, _LightDirection.xyz));
        // radiance = radiance + emissionAt(positionWS, density) * spotAttenuation - (1 - shadowAt(positionWS)) * _ShadowIntensity;

        return radiance;
    }

    //Ray-marching
    float4 scattering(float3 ray, float near, float far)
    {
        float3 totalRadiance = 0;
        float totalTransmittance = 1.0;
        float stepSize = (far - near) / _Steps;
        // [UNITY_LOOP]
        for(int i = 0; i < _Steps; i++)
        {
            float3 pos = _WorldSpaceCameraPos + ray * (near + stepSize * i);
            float density = SampleCloudDensity(pos)  * step(_LightCosHalfAngle, dot(normalize(_LightPosition.xyz - pos * _LightPosition.w), _LightDirection.xyz));
            //dx的消光系数
            float transmittance = BeerLambert(extinctionAt(pos, density), stepSize);
            
            //参考SIGGRAPH2015 Frosbite PB and unified volumetrics中对积分式进行了一定求解。
            //Scattering评估成本太高，可以认为dx内为定值，即常数。
            //认为dx内Transmittance不是定值，因此将Transmittance在0~D上的积分求解，让Transmittance随dx连续变化，使计算结果更加接近正确值。
            float3 scattering =  scatteringAt(pos, density) * scatteredLight(pos, ray, density) * (1 - transmittance) / max(extinctionAt(pos, density), 0.00001f);
            totalRadiance += scattering * totalTransmittance;
            
            //如果按照RTR4的积分式积分，此时对于stepSize这段距离，认为其中的totalTransmittance是一个定值，这是不合理的，因此不直接使用积分式累积。
            // totalRadiance +=  totalTransmittance * scatteredLight(pos, ray, density) * scatteringAt(pos, density) * stepSize;
            
            totalTransmittance *= transmittance;
        }
        
        return float4(totalRadiance, totalTransmittance);
    }

    Varyings volumetricLightVert(Attributes input)
    {
        Varyings output = (Varyings)0;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 positionOS = input.positionOS.xyz;
        VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
        output.positionCS = vertexInput.positionCS;
        output.positionWS = vertexInput.positionWS;
        output.screenUV = output.positionCS.xyw;
        #if UNITY_UV_STARTS_AT_TOP
        output.screenUV.xy = output.screenUV.xy * float2(0.5, -0.5) + 0.5 * output.screenUV.z;
        #else
        output.screenUV.xy = output.screenUV.xy * 0.5 + 0.5 * output.screenUV.z;
        #endif

        return output;
    }

    float4 volumetricLightFrag(Varyings input) : SV_TARGET
    {
        UNITY_SETUP_INSTANCE_ID(input);

        float2 screen_uv = (input.screenUV.xy / input.screenUV.z);
        float3 viewRay = normalize(input.positionWS - _WorldSpaceCameraPos);
        float cameraDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screen_uv);

        float nearIntersect, farIntersect, lightDepthIntersect;
        lightDepthIntersect = computeIntersect(viewRay, nearIntersect, farIntersect);

        //计算viewRay与RayMarching空间的近交点是否可见，如果近交点都不可见，那么就丢弃
        float3 nearIntersectWS = _WorldSpaceCameraPos + viewRay * nearIntersect;
        float4 nearIntersectCS = TransformWorldToHClip(nearIntersectWS);
        nearIntersectCS /= nearIntersectCS.w;
        clip(nearIntersectCS.z - cameraDepth);

        //farIntersect clamp到viewRay与物体相交的点
        farIntersect = min(farIntersect, computeDepthFromCameraToRealFragment(screen_uv, cameraDepth));
        
        //Jitter sampling以优化采样次数，这里是对采样的near~far做抖动，不是在视平面方向抖动
        float2 jitterUV = screen_uv * _RenderExtent.xy * _BlueNoiseTexture_TexelSize.xy;
        float offset = SAMPLE_TEXTURE2D(_BlueNoiseTexture, sampler_PointRepeat, jitterUV).a;
        offset *= (farIntersect - nearIntersect) / _Steps;
        //注意这里是-=offset，如果让farIntersect增大了，在偏正对光源的情况下，会让本来无光区域接收到光。
        nearIntersect -= offset;
        farIntersect -= offset;

        //Ray Marching!!
        float4 color = scattering(viewRay, nearIntersect, farIntersect);

        return color;
    }
    ENDHLSL
    
    SubShader
    {
        Pass 
        {
            Name "Volumetric Light Spot" // Pass 0
            ZTest Off
            ZWrite Off
            Cull Front
            Blend One SrcAlpha
            
            HLSLPROGRAM

            #pragma vertex volumetricLightVert
            #pragma fragment volumetricLightFrag
            
            ENDHLSL
        }
    }
}
