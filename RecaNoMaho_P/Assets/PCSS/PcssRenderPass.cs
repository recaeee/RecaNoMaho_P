using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RecaNoMaho
{
    public class PcssRenderPass : ScriptableRenderPass
    {
        static class ShaderConstants
        {
            public static readonly int _DirLightPcssParams0 = Shader.PropertyToID("_DirLightPcssParams0");
            public static readonly int _DirLightPcssParams1 = Shader.PropertyToID("_DirLightPcssParams1");
            public static readonly int _DirLightPcssProjs = Shader.PropertyToID("_DirLightPcssProjs");
            public static readonly int _ShadowTileTexelSize = Shader.PropertyToID("_ShadowTileTexelSize");

            public static readonly int _ColorAttachmentTexelSize = Shader.PropertyToID("_ColorAttachmentTexelSize");
            public static readonly int _PenumbraMaskTexelSize = Shader.PropertyToID("_PenumbraMaskTexelSize");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _PenumbraMaskTex = Shader.PropertyToID("_PenumbraMaskTex");

            public static readonly int _FindBlockerSampleCount = Shader.PropertyToID("_FindBlockerSampleCount");
            public static readonly int _PcfSampleCount = Shader.PropertyToID("_PcfSampleCount");
            public static readonly int _PcssTemporalFilter = Shader.PropertyToID("_PcssTemporalFilter");
        }

        private struct PcssCascadeData
        {
            public Vector4 dirLightPcssParams0;
            public Vector4 dirLightPcssParams1;
        }

        private static readonly int shadowCascadeCount = 4;
        
        private PcssRenderFeature.GlobalParams globalParams;
        private string shaderKeyword = "RECANOMAHO_PCSS";
        private RenderTextureDescriptor penumbraMaskDesc;
        private RenderTexture penumbraMaskTex;
        private RenderTexture penumbraMaskBlurTempTex;
        private Material penumbraMaskMat;
        private int colorAttachmentWidth, colorAttachmentHeight;
        private int frameIndex;

        private PcssCascadeData[] pcssCascadeDatas;
        
        public PcssRenderPass()
        {
            profilingSampler = new ProfilingSampler("PCSS");
            penumbraMaskDesc = new RenderTextureDescriptor();
            pcssCascadeDatas = new PcssCascadeData[shadowCascadeCount];
            frameIndex = 0;
        }
        public bool Setup(ref RenderingData renderingData, PcssRenderFeature.GlobalParams globalParams)
        {
            this.globalParams = globalParams;
            SetupPenumbraMask(renderingData.cameraData.cameraTargetDescriptor);

            return true;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                if (globalParams.active)
                {
                    cmd.EnableShaderKeyword(shaderKeyword);
                    PackDirLightParams(cmd);

                    int shadowTileResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                        renderingData.shadowData.mainLightShadowmapWidth,
                        renderingData.shadowData.mainLightShadowmapHeight,
                        renderingData.shadowData.mainLightShadowCascadesCount);
                    cmd.SetGlobalFloat(ShaderConstants._ShadowTileTexelSize, 1f / (float)shadowTileResolution);
                    cmd.SetGlobalFloat(ShaderConstants._FindBlockerSampleCount, globalParams.findBlockerSampleCount);
                    cmd.SetGlobalFloat(ShaderConstants._PcfSampleCount, globalParams.pcfSampleCount);

                    frameIndex = frameIndex >= 1024 ? 0 : frameIndex + 1;
                    cmd.SetGlobalFloat(ShaderConstants._PcssTemporalFilter, frameIndex);
                    
                    RenderPenumbraMask(cmd);
                }
                else
                {
                    cmd.DisableShaderKeyword(shaderKeyword);
                }
                
                
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup()
        {
            if (penumbraMaskTex != null)
            {
                penumbraMaskTex.Release();
                penumbraMaskTex = null;
            }
            
            if (penumbraMaskBlurTempTex != null)
            {
                penumbraMaskBlurTempTex.Release();
                penumbraMaskBlurTempTex = null;
            }

            if (penumbraMaskMat != null)
            {
                GameObject.DestroyImmediate(penumbraMaskMat);
            }
        }

        private void SetupPenumbraMask(RenderTextureDescriptor cameraTargetDesc)
        {
            penumbraMaskDesc = cameraTargetDesc;
            penumbraMaskDesc.colorFormat = RenderTextureFormat.R8;
            penumbraMaskDesc.graphicsFormat = GraphicsFormat.R8_UNorm;
            penumbraMaskDesc.depthStencilFormat = GraphicsFormat.None;
            penumbraMaskDesc.autoGenerateMips = false;
            penumbraMaskDesc.useMipMap = false;
            penumbraMaskDesc.msaaSamples = 1;
            penumbraMaskDesc.width = (int)(cameraTargetDesc.width / globalParams.penumbraMaskParams.penumbraMaskScale);
            penumbraMaskDesc.height = (int)(cameraTargetDesc.height / globalParams.penumbraMaskParams.penumbraMaskScale);
            colorAttachmentWidth = cameraTargetDesc.width;
            colorAttachmentHeight = cameraTargetDesc.height;

            if (penumbraMaskTex != null && (penumbraMaskTex.width != penumbraMaskDesc.width ||
                                            penumbraMaskTex.height != penumbraMaskDesc.height))
            {
                penumbraMaskTex.Release();
                penumbraMaskTex = null;
                penumbraMaskBlurTempTex.Release();
                penumbraMaskBlurTempTex = null;
            }

            if (penumbraMaskTex == null)
            {
                penumbraMaskTex = new RenderTexture(penumbraMaskDesc);
                penumbraMaskTex.name = "_PenumbraMaskTex";
                penumbraMaskTex.wrapMode = TextureWrapMode.Clamp;
                penumbraMaskTex.filterMode = FilterMode.Bilinear;
                
                penumbraMaskBlurTempTex = new RenderTexture(penumbraMaskDesc);
                penumbraMaskBlurTempTex.name = "_PenumbraMaskBlurTempTex";
                penumbraMaskBlurTempTex.wrapMode = TextureWrapMode.Clamp;
                penumbraMaskBlurTempTex.filterMode = FilterMode.Bilinear;
            }

            if (penumbraMaskMat == null)
            {
                penumbraMaskMat = new Material(globalParams.penumbraMaskParams.penumbraMaskShader);
            }
        }

        private void RenderPenumbraMask(CommandBuffer cmd)
        {
            cmd.SetRenderTarget(penumbraMaskTex, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.SetGlobalVector(ShaderConstants._ColorAttachmentTexelSize,
                new Vector4(1f / colorAttachmentWidth, 1f / colorAttachmentHeight, colorAttachmentWidth,
                    colorAttachmentHeight));
            cmd.SetGlobalVector(ShaderConstants._PenumbraMaskTexelSize, new Vector4(1f / penumbraMaskDesc.width, 1f / penumbraMaskDesc.height, penumbraMaskDesc.width, penumbraMaskDesc.height));
            cmd.SetGlobalVector(ShaderConstants._BlitScaleBias, new Vector4(1, 1, 0, 0));
            CommonUtil.DrawFullScreenTriangle(cmd, penumbraMaskMat, 0);

            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskTex);
            cmd.SetRenderTarget(penumbraMaskBlurTempTex, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store);
            CommonUtil.DrawFullScreenTriangle(cmd, penumbraMaskMat, 1);
            
            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskBlurTempTex);
            cmd.SetRenderTarget(penumbraMaskTex, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store);
            CommonUtil.DrawFullScreenTriangle(cmd, penumbraMaskMat, 2);
            
            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskTex);
        }
        
        private void PackDirLightParams(CommandBuffer cmd)
        {
            float lightAngularDiameter = globalParams.pcssLightParams.dirLightAngularDiameter;
            float dirlightDepth2Radius =
                Mathf.Tan(0.5f * Mathf.Deg2Rad * lightAngularDiameter);
            //确保PCF最小的角直径覆盖Blocker Search的角直径？意味着PCF范围一定比Blocker Seacher范围大？
            float minFilterAngularDiameter =
                Mathf.Max(globalParams.pcssLightParams.dirLightPcssBlockerSearchAngularDiameter,
                    globalParams.pcssLightParams.dirLightPcssMinFilterMaxAngularDiameter);
            float halfMinFilterAngularDiameterTangent =
                Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(minFilterAngularDiameter, lightAngularDiameter));
            
            float halfBlockerSearchAngularDiameterTangent = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(globalParams.pcssLightParams.dirLightPcssBlockerSearchAngularDiameter, lightAngularDiameter));

            
            for (int i = 0; i < shadowCascadeCount; ++i)
            {
                float shadowmapDepth2RadialScale = Mathf.Abs(PcssContext.deviceProjectionMatrixs[i].m00 /
                                                             PcssContext.deviceProjectionMatrixs[i].m22);
                //depth2RadialScale
                pcssCascadeDatas[i].dirLightPcssParams0.x = dirlightDepth2Radius * shadowmapDepth2RadialScale;
                //radial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams0.y = 1.0f / pcssCascadeDatas[i].dirLightPcssParams0.x;
                //maxBlockerDistance
                pcssCascadeDatas[i].dirLightPcssParams0.z = globalParams.pcssLightParams.dirLightPcssMaxPenumbraSize /
                                                            (2.0f * halfMinFilterAngularDiameterTangent);
                //maxSamplingDistance
                pcssCascadeDatas[i].dirLightPcssParams0.w =
                    globalParams.pcssLightParams.dirLightPcssMaxSamplingDistance;
                
                //minFilterRadius(in texels)
                pcssCascadeDatas[i].dirLightPcssParams1.x =
                    globalParams.pcssLightParams.dirLightPcssMinFilterSizeTexels;
                //minFilterRadial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams1.y =
                    1.0f / (halfMinFilterAngularDiameterTangent * shadowmapDepth2RadialScale);
                //blockerRadial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams1.z =
                    1.0f / (halfBlockerSearchAngularDiameterTangent * shadowmapDepth2RadialScale);
                //blockerClumpSampleExponent
                pcssCascadeDatas[i].dirLightPcssParams1.w =
                    0.5f * globalParams.pcssLightParams.dirLightPcssBlockerSamplingClumpExponent;

            }
            
            Vector4[] dirLightPcssParams0 = new Vector4[shadowCascadeCount];
            Vector4[] dirLightPcssParams1 = new Vector4[shadowCascadeCount];
            for (int i = 0; i < shadowCascadeCount; ++i)
            {
                dirLightPcssParams0[i] = pcssCascadeDatas[i].dirLightPcssParams0;
                dirLightPcssParams1[i] = pcssCascadeDatas[i].dirLightPcssParams1;
            }
            
            cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssParams0, dirLightPcssParams0);
            cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssParams1, dirLightPcssParams1);
            cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssProjs, PcssContext.deviceProjectionVectors);
        }
    }
}