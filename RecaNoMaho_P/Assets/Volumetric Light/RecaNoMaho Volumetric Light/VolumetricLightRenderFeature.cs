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
            [Tooltip("Volumetic Light RT Scale")] [Range(0.01f, 2)] public float renderScale = 1f;
            [Tooltip("Ray Marching步进次数")][Range(0, 64)] public int steps = 8;
            [Tooltip("体积光的可见距离(影响介质透射率)")][Range(0.01f, 50f)] public float visibilityDistance = 50;
            [Tooltip("散射光在顺光或逆光方向上的相对强度，取值范围[-1, 1]，1在逆光上最强")] [Range(-1f, 1f)] public float HGFactor;
            [Tooltip("每帧采样不同的BlueNoiseTexture做抖动采样，优化采样次数")]
            public List<Texture2D> blueNoiseTextures;
            public float GetExtinction()
            {
                return Mathf.Log(10f) / visibilityDistance;
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

        private void OnDestroy()
        {
            if (volumetricLightRenderPass != null)
            {
                volumetricLightRenderPass.Cleanup();
            }
        }

        private void OnDisable()
        {
            if (volumetricLightRenderPass != null)
            {
                volumetricLightRenderPass.Cleanup();
            }
        }

        private void OnValidate()
        {
            if (volumetricLightRenderPass != null)
            {
                volumetricLightRenderPass.Cleanup();
            }
        }
    }
}



