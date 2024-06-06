using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RecaNoMaho
{
    public class VolumetricLightRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class GlobalParams
        {
            [Header("体积光质量")]
            [Tooltip("Volumetic Light RT Scale")] [Range(0.01f, 2)] public float renderScale = 1f;
            [Tooltip("Ray Marching步进次数")][Range(0, 64)] public int steps = 8;

            [Header("全局参与介质材质")]
            [Tooltip("反照率Albedo")] public Color albedo = new Color(0.8f, 0.8f,0.8f);
            [Tooltip("消光系数Extinction")] public float extinction = 0.3f;
            [Tooltip("各向异性Phase g")] [Range(-1f, 1f)]
            public float phaseG = -0.5f;
            [Tooltip("自发光Emission")][ColorUsage(true, true)] public Color emission = Color.black;
            
            [Header("Jitter Sampling")]
            [Tooltip("每帧采样不同的BlueNoiseTexture做抖动采样，优化采样次数")]
            public List<Texture2D> blueNoiseTextures;

            [Header("云层模拟")] 
            [Tooltip("基本形状与细节")] public Texture3D cloudNoise3DTextureA;
            [Tooltip("边缘侵蚀")] public Texture3D cloudNoise3DTextureB;
            public Vector3 cloudScale = new Vector3(1f, 1f, 1f);
            [Tooltip("雾的Clip参数")] [Range(0f, 1f)] public float densityClip = 0f;
            [Tooltip("云层范围")] public Vector4 inCloudMin = new Vector4(-100, 0, -100, 0);
            public Vector4 inCloudMax = new Vector4(100, 100, 100, 0);
            [Tooltip("云流速度")] public Vector4 cloudFlowSpeed = new Vector4(1, 1, 1, 0);
            [Tooltip("云层覆盖图")] public Texture2D weatherDataTexture;
            
            public Vector4 GetScatteringExtinction()
            {
                float nonNegativeExtinction = Mathf.Max(0f, extinction);
                return new Vector4(albedo.r * nonNegativeExtinction, albedo.g * nonNegativeExtinction, albedo.b * nonNegativeExtinction, nonNegativeExtinction);
            }
            
            public Vector4 GetEmissionPhaseG()
            {
                return new Vector4(emission.r, emission.g, emission.b, phaseG);
            }
        }

        public GlobalParams globalParams = new GlobalParams();
        VolumetricLightRenderPass volumetricLightRenderPass;
        
        public override void Create()
        {
            volumetricLightRenderPass = new VolumetricLightRenderPass();
            volumetricLightRenderPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (volumetricLightRenderPass.Setup(ref renderingData, globalParams))
            {
                renderer.EnqueuePass(volumetricLightRenderPass);
            }
        }
    }
}



