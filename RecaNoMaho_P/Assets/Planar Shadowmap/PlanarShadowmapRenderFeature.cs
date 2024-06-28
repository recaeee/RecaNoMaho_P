using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RecaNoMaho
{
    public class PlanarShadowmapRenderFeature : ScriptableRendererFeature
    {
        //静态变量供外部接口方便设置是否实际执行该Pass（前提是Pass为Active）。注意actuallyExecute为false时，并不会释放PlanarShadowmap的RT内存。
        public static bool actuallyExecute = true;
        //静态变量供外部接口方便设置CachedShadowRenderer是否变化，需要外部逻辑严谨控制
        public static bool CachedShadowRendererChanged = false;
        
        [Serializable]
        public class GlobalParams
        {
            public enum SampleMode
            {
                pointFilter,
                pcf2X2
            }

            public LayerMask layerMask;
            [Header("地面高度")]
            public float planarHeight = 0;
            [Header("最大有效阴影高度")]
            public float globalMaxShadowHeight = 10;
            [Header("Shadow Bias")]
            public Vector4 planarShadowmapShadowBias = new Vector4(0.01f, 0.0f, 0, 0);
            [Header("RT Scale")]
            [Range(0.001f, 2f)] public float renderScale = 1f;
            [Header("渲染范围修正")]
            public Vector3 offset = new Vector3(0f, 0f, 0f);
            [Header("采样模式")] 
            public SampleMode sampleMode = SampleMode.pointFilter;
            [Header("Shadow Cache")] 
            public bool shadowCache = false;
            [Header("静态阴影贴图RT Scale")] 
            [Range(0.001f, 2f)] public float cachedPlanarShadowmapRenderScale = 1f;
            [Header("LayerMasks")]
            public LayerMask cachedShadowRendererLayers;
            public LayerMask realtimeShadowRendererLayers;
        }

        public GlobalParams globalParams = new GlobalParams();
        
        private PlanarShadowmapRenderPass planarShadowmapRenderPass;
        public override void Create()
        {
            if (planarShadowmapRenderPass != null)
            {
                planarShadowmapRenderPass.Cleanup();
            }
            planarShadowmapRenderPass = new PlanarShadowmapRenderPass(globalParams.layerMask);
            planarShadowmapRenderPass.renderPassEvent = RenderPassEvent.BeforeRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (planarShadowmapRenderPass.Setup(ref renderingData, globalParams))
            {
                renderer.EnqueuePass(planarShadowmapRenderPass);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (planarShadowmapRenderPass != null)
            {
                planarShadowmapRenderPass.Cleanup();
            }
            base.Dispose(disposing);
        }
        
        //完全关闭该Pass所有逻辑，清理内存。
        public void SetActiveWrapped(bool active)
        {
            SetActive(active);
            actuallyExecute = active;
            if (!active)
            {
                planarShadowmapRenderPass.Cleanup();
            }
        }
        
        //该Pass始终处于开启状态，但不会Execute，RT内存常驻。
        public static void SetActuallyExecute(bool value)
        {
            actuallyExecute = value;
            //改UniversalRendererData会触发UniversalRenderer的重建，不要使用这种方法。
        }
    }
}