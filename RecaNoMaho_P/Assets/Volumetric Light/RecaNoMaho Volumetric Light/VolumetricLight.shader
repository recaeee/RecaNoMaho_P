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

    TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
    TEXTURE2D(_BlueNoiseTexture);

    int _BoundaryPlanesCount;
    float4 _BoundaryPlanes[6]; // Ax + By + Cz + D = 0; _BoundaryPlane[i] = (A, B, C, D); normal = (A, B, C)
    int _Steps;

    float _DirLightDistance;
    float4 _LightPosition;
    float4 _LightDirection;
    float4 _LightColor;
    float _LightCosHalfAngle;
    int  _UseShadow;
    int _ShadowLightIndex;

    float _TransmittanceExtinction;
    float _IncomingLoss; // 光线到x处剩余的能量
    float _HGFactor; // _HGFactor影响散射光在顺光或逆光方向上的相对强度，取值范围[-1, 1]，1在逆光上最强。
    float _Absorption;
    float4 _RenderExtent;
    float4 _BlueNoiseTexture_TexelSize;

    float4 _CameraPackedInfo;

    float _BrightIntensity;
    float _DarkIntensity;

    #define TAN_FOV_VERTICAL _CameraPackedInfo.x
    #define TAN_FOV_HORIZONTAL _CameraPackedInfo.y

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

    //介质中x处的消光系数（吸收和外散射事件），k为风格化系数
    float extinctionAt(float3 pos, float k)
    {
        //假设介质均匀
        return k * _TransmittanceExtinction;
        //也可以采样3D Texture代表非均匀介质
        //也可以基于解析函数实时计算
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
        return SpotLightRealtimeShadow(_ShadowLightIndex, positionWS);
    }

    //返回介质中x处接收到的光线（RGB），以及x处到光源的方向
    float3 lightAt(float3 pos, out float3 lightDir)
    {
        //_LightPosition.w = 0时，为方向光，此时_LightPosition.xyx为方向光dir
        //_LightPosition.w = 1时，为SpotLight
        lightDir = normalize(_LightPosition.xyz - pos * _LightPosition.w);
        float lightDistance = lerp(_DirLightDistance, distance(_LightPosition.xyz, pos), _LightPosition.w);
        //从光源到介质中x处的透射率，这里假设介质中x处到光源为均匀介质
        float transmittance = lerp(1, exp(-lightDistance * extinctionAt(pos, _BrightIntensity)), _IncomingLoss);

        float3 lightColor = _LightColor.rgb;
        //考虑光源方向与片元到光源方向之间夹角的能量损失
        lightColor *= step(_LightCosHalfAngle, dot(lightDir, _LightDirection.xyz));
        //考虑阴影
        lightColor *= shadowAt(pos);
        //透射率造成的衰减
        lightColor *= transmittance;
        //散射系数=消光系数-吸收系数，但这里参数简化为比例，即散射系数=消光系数*(1 - _Absorption)
        lightColor *= extinctionAt(pos, _BrightIntensity) * (1 - _Absorption);
        //风格化亮部
        lightColor *= _BrightIntensity;

        return lightColor;
    }

    //PhaseFunction，给定入射光线方向，根据概率分布，计算指定方向上的散射光线RGB，体积积分总是为1
    float3 Phase(float3 lightDir, float3 viewDir)
    {
        // 采用Henyey-Greenstein phase function(即HG Phase)模拟米氏散射，即介质中微粒与入射光线波长的相对大小接近相等，模拟丁达尔效应时的情况
        return ( 1 - _HGFactor * _HGFactor) / ( 4 * PI * pow(1 + _HGFactor * _HGFactor- 2 * _HGFactor * dot(viewDir, lightDir) , 1.5));
    }

    float3 scattering(float3 ray, float near, float far, out float3 transmittance)
    {
        transmittance = 1;
        float3 totalLight = 0;
        float stepSize = (far - near) / _Steps;
        // [UNITY_LOOP]
        for(int i= 1; i <= _Steps; i++)
        {
            float3 pos = _WorldSpaceCameraPos + ray * (near + stepSize * i);
            //从介质中x处到视点的消光系数，采用累乘避免多次积分
            transmittance *= exp(-stepSize * extinctionAt(pos, _DarkIntensity));
            
            float3 lightDir;
            totalLight += transmittance * lightAt(pos, lightDir) * stepSize * Phase(lightDir, -ray);
        }
        return totalLight;
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
        float3 transmittance = 1;
        float3 color = 0;
        color = scattering(viewRay, nearIntersect, farIntersect, transmittance);
        
        return float4(color, 1);
    }
    ENDHLSL
    
    SubShader
    {
        UsePass "Hidden/Universal Render Pipeline/Blit/Blit"
        
        Pass 
        {
            Name "Volumetric Light Spot" // 1
            ZTest Off
            ZWrite Off
            Cull Off
            Blend One One
            
            HLSLPROGRAM

            #pragma vertex volumetricLightVert
            #pragma fragment volumetricLightFrag
            
            ENDHLSL
        }
    }
}
