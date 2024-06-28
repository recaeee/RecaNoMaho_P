//主要思路参考：https://zhuanlan.zhihu.com/p/31504088
Shader "RecaNoMaho/PlanarShadow"
{
    Properties
    {
        //继承Lit.shader的Properties
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
                
        _PlanarHeight ("_PlanarHeight", float) = 0
        _PlanarShadowColor("_PlanarShadowColor", color) = (0, 0, 0, 1)
        _PlanarShadowAttenuation("_PlanarShadowAttenuation", float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
        #include "Packages/com.unity.render-pipelines.universal@14.0.9/ShaderLibrary/RealtimeLights.hlsl"

        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float _PlanarHeight;
        float4 _PlanarShadowColor;
        float _PlanarShadowAttenuation;
        CBUFFER_END

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
            float4 planarShadowColor : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings planarShadowVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionOS = input.positionOS.xyz;
            VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
            float3 positionWS = vertexInput.positionWS;
            float3 dirLightDir = normalize(GetMainLight().direction);

            //求世界空间下投影坐标planarShadowPositionWS
            float3 planarShadowPositionWS = float3(0, 0, 0);
            planarShadowPositionWS.y = min(positionWS.y, _PlanarHeight);//y值低于地面时，y值不置为_PlanarHeight
            planarShadowPositionWS.xz = positionWS.xz - dirLightDir.xz * max(0, positionWS.y - _PlanarHeight) / dirLightDir.y;
            
            output.positionCS = TransformWorldToHClip(planarShadowPositionWS);
            
            //平面阴影衰减计算：根据 (0,0,0)在投影后的坐标(即平面阴影中心点) 和 当前顶点 之间的距离计算衰减量
            //当多个物体阴影重叠时，该衰减会穿帮
            float3 planarShadowCenterPositionWS = float3(unity_ObjectToWorld[0].w, _PlanarHeight, unity_ObjectToWorld[2].w);//直接取M矩阵值，避免多余的矩阵计算
            float planarShadowAttenuation = saturate(1 - saturate(distance(planarShadowPositionWS, planarShadowCenterPositionWS)) * _PlanarShadowAttenuation);
            output.planarShadowColor = float4(_PlanarShadowColor.rgb, _PlanarShadowColor.a * planarShadowAttenuation);
            
            return output;
        }

        float4 planarShadowFrag(Varyings input) : SV_Target
        {
            return input.planarShadowColor;
        }
        
        ENDHLSL
        
        UsePass "Universal Render Pipeline/Lit/ForwardLit"
        
        Pass
        {
            Name "Planar Shadow"

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1 , 0
            
            //使用Stencil保证阴影不会重叠
            Stencil
            {
                Ref 0
                Comp equal
                Pass IncrWrap 
                Fail Keep
                ZFail Keep    
            }
            

            HLSLPROGRAM
            #pragma vertex planarShadowVert
            #pragma fragment planarShadowFrag
            ENDHLSL
        }
    }
}
