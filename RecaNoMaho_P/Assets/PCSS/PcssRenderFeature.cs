using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RecaNoMaho
{
    public class PcssRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class GlobalParams
        {
            public bool active = true;
            [Range(4, 64)]public int findBlockerSampleCount = 16;
            [Range(4, 64)]public int pcfSampleCount = 16;
            
            
            public PcssLightParams pcssLightParams = new PcssLightParams();
            public PenumbraMaskParams penumbraMaskParams = new PenumbraMaskParams();
        }

        [Serializable]
        public class PcssLightParams
        {
            public float dirLightAngularDiameter = 1.5f;
            public float dirLightPcssBlockerSearchAngularDiameter = 12;
            public float dirLightPcssMinFilterMaxAngularDiameter = 10;
            public float dirLightPcssMaxPenumbraSize = 0.56f;
            public float dirLightPcssMaxSamplingDistance = 0.5f;
            public float dirLightPcssMinFilterSizeTexels = 1.5f;
            public float dirLightPcssBlockerSamplingClumpExponent = 2f;
        }

        [Serializable]
        public class PenumbraMaskParams
        {
            [Range(1, 32)] public int penumbraMaskScale = 4;
            public Shader penumbraMaskShader;
        }
        
        public GlobalParams globalParams = new GlobalParams();
        
        private PcssRenderPass pcssRenderPass;
        public override void Create()
        {
            if (pcssRenderPass != null)
            {
                pcssRenderPass.Cleanup();
            }

            pcssRenderPass = new PcssRenderPass();
            pcssRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pcssRenderPass.Setup(ref renderingData, globalParams))
            {
                renderer.EnqueuePass(pcssRenderPass);
            }
        }
    }
}