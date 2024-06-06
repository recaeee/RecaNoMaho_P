using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RecaNoMaho
{
    public class VolumetricLightRenderPass : ScriptableRenderPass
    {
        struct LightVolumeData
        {
            public LightVolumeRenderer lightVolumeRenderer;
            public int lightIndex;
            public int additionalLightIndex;
            public int volumeIndex;
        }

        static class ShaderConstants
        {
            //光源相关
            public static readonly int _LightPosition = Shader.PropertyToID("_LightPosition");
            public static readonly int _LightDirection = Shader.PropertyToID("_LightDirection");
            public static readonly int _LightColor = Shader.PropertyToID("_LightColor");
            public static readonly int _LightCosHalfAngle = Shader.PropertyToID("_LightCosHalfAngle");
            public static readonly int _ShadowLightIndex = Shader.PropertyToID("_ShadowLightIndex");
            public static readonly int _ApplyShadow = Shader.PropertyToID("_ApplyShadow");
            
            //Ray Marching相关
            public static readonly int _BoundaryPlanesCount = Shader.PropertyToID("_BoundaryPlanesCount");
            public static readonly int _BoundaryPlanes = Shader.PropertyToID("_BoundaryPlanes");
            public static readonly int _Steps = Shader.PropertyToID("_Steps");
            
            public static readonly int _ScatteringExtinction = Shader.PropertyToID("_ScatteringExtinction");
            public static readonly int _EmissionPhaseG = Shader.PropertyToID("_EmissionPhaseG");
                
            public static readonly int _BlueNoiseTexture = Shader.PropertyToID("_BlueNoiseTexture");
            public static readonly int _RenderExtent = Shader.PropertyToID("_RenderExtent");

            //云层塑造
            public static readonly int _CloudNoise3DTextureA = Shader.PropertyToID("_CloudNoise3DTextureA");
            public static readonly int _CloudNoise3DTextureB = Shader.PropertyToID("_CloudNoise3DTextureB");
            public static readonly int _CloudScale = Shader.PropertyToID("_CloudScale");
            public static readonly int _DensityClip = Shader.PropertyToID("_DensityClip");
            public static readonly int _InCloudMin = Shader.PropertyToID("_InCloudMin");
            public static readonly int _InCloudMax = Shader.PropertyToID("_InCloudMax");
            public static readonly int _CloudFlowSpeed = Shader.PropertyToID("_CloudFlowSpeed");
            public static readonly int _WeatherDataTexture = Shader.PropertyToID("_WeatherDataTexture");
            
            
            //相机相关
            public static readonly int _CameraPackedInfo = Shader.PropertyToID("_CameraPackedInfo");
            
            //风格化参数
            public static readonly int _ShadowIntensity = Shader.PropertyToID("_ShadowIntensity");
        }

        enum ShaderPass
        {
            VOLUMETRIC_LIGHT_SPOT = 0,
        }
        
        private Shader shader;
        private Material mat;
        
        private List<LightVolumeData> lightVolumeDatas;
        private VolumetricLightRenderFeature.GlobalParams globalParams;
        private RenderTextureDescriptor sourceDesc;
        private RenderTexture volumetricLightTex;
        private int frameIndex = 0;

        public VolumetricLightRenderPass()
        {
            profilingSampler = new ProfilingSampler("Volumetric Light");
            lightVolumeDatas = new List<LightVolumeData>();
            globalParams = new VolumetricLightRenderFeature.GlobalParams();
        }

        public bool Setup(ref RenderingData renderingData, VolumetricLightRenderFeature.GlobalParams globalParams)
        {
            if (!FetchMaterial())
            {
                return false;
            }

            if (!FetchLightVolumeDatas(ref renderingData))
            {
                return false;
            }
            
            this.globalParams = globalParams;

            sourceDesc = renderingData.cameraData.cameraTargetDescriptor;
            sourceDesc.width = (int)(sourceDesc.width * globalParams.renderScale);
            sourceDesc.height = (int)(sourceDesc.height * globalParams.renderScale);

            if (sourceDesc.width <= 0 || sourceDesc.height <= 0)
            {
                return false;
            }
            
            sourceDesc.depthBufferBits = 0;

            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                SetupVolumetricLightTexture();
            }

            return true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Volumetric Light");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Camera camera = renderingData.cameraData.camera;

                for (int i = 0; i < lightVolumeDatas.Count; i++)
                {
                    LightVolumeData lightVolumeData = lightVolumeDatas[i];
                    LightVolumeRenderer lightVolumeRenderer = lightVolumeData.lightVolumeRenderer;
                    if (!lightVolumeRenderer.enabled)
                    {
                        continue;
                    }

                    //光源相关参数
                    VisibleLight light = renderingData.cullResults.visibleLights[lightVolumeData.lightIndex];
                    cmd.SetGlobalInt(ShaderConstants._ShadowLightIndex, lightVolumeData.additionalLightIndex);
                    List<Vector4> boundaryPlanes = lightVolumeRenderer.GetVolumeBoundFaces(camera);
                    Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
                    UniversalRenderPipeline.InitializeLightConstants_Common(renderingData.lightData.visibleLights, lightVolumeData.lightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);

                    cmd.SetGlobalVector(ShaderConstants._LightPosition, lightPos);
                    cmd.SetGlobalVector(ShaderConstants._LightDirection, lightSpotDir);
                    cmd.SetGlobalVector(ShaderConstants._LightColor,
                        lightColor * lightVolumeData.lightVolumeRenderer.intensityMultiplier);
                    cmd.SetGlobalFloat(ShaderConstants._LightCosHalfAngle,
                        light.lightType == LightType.Spot ? Mathf.Cos(Mathf.Deg2Rad * light.spotAngle / 2) : -2);
                    cmd.SetGlobalInt(ShaderConstants._ApplyShadow, lightVolumeRenderer.applyShadow ? 1 : 0);
                    
                    //RayMarching相关参数
                    cmd.SetGlobalInt(ShaderConstants._BoundaryPlanesCount, boundaryPlanes.Count);
                    cmd.SetGlobalVectorArray(ShaderConstants._BoundaryPlanes, boundaryPlanes);
                    cmd.SetGlobalInt(ShaderConstants._Steps, lightVolumeRenderer.stepOverride ? lightVolumeRenderer.rayMarchingSteps : globalParams.steps);
                    
                    cmd.SetGlobalColor(ShaderConstants._ScatteringExtinction,
                        lightVolumeRenderer.mediaOverride ? lightVolumeRenderer.GetScatteringExtinction() : globalParams.GetScatteringExtinction());
                    cmd.SetGlobalColor(ShaderConstants._EmissionPhaseG, lightVolumeRenderer.mediaOverride ? lightVolumeRenderer.GetEmissionPhaseG() : globalParams.GetEmissionPhaseG());
                    
                    if (globalParams.blueNoiseTextures.Count != 0)
                    {
                        cmd.SetGlobalTexture(ShaderConstants._BlueNoiseTexture, globalParams.blueNoiseTextures[frameIndex % globalParams.blueNoiseTextures.Count]);
                        if (frameIndex > 1024)
                        {
                            frameIndex = 0;
                        }
                        else
                        {
                            frameIndex++;
                        }
                        
                        cmd.SetGlobalVector(ShaderConstants._RenderExtent,
                            new Vector4(sourceDesc.width, sourceDesc.height, 1f / sourceDesc.width,
                                1f / sourceDesc.height));
                    }

                    if (globalParams.cloudNoise3DTextureA != null && globalParams.cloudNoise3DTextureB != null && globalParams.weatherDataTexture != null)
                    {
                        cmd.SetGlobalTexture(ShaderConstants._CloudNoise3DTextureA, globalParams.cloudNoise3DTextureA);
                        cmd.SetGlobalTexture(ShaderConstants._CloudNoise3DTextureB, globalParams.cloudNoise3DTextureB);
                        cmd.SetGlobalVector(ShaderConstants._CloudScale, globalParams.cloudScale);
                        cmd.SetGlobalFloat(ShaderConstants._DensityClip, globalParams.densityClip);
                        cmd.SetGlobalVector(ShaderConstants._InCloudMin, globalParams.inCloudMin);
                        cmd.SetGlobalVector(ShaderConstants._InCloudMax, globalParams.inCloudMax);
                        cmd.SetGlobalVector(ShaderConstants._CloudFlowSpeed, globalParams.cloudFlowSpeed);
                        cmd.SetGlobalTexture(ShaderConstants._WeatherDataTexture, globalParams.weatherDataTexture);
                    }

                    //相机相关
                    float tanFov = Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
                    cmd.SetGlobalVector(ShaderConstants._CameraPackedInfo, new Vector4(tanFov, tanFov * camera.aspect, 0, 0));
                    
                    //风格化参数
                    cmd.SetGlobalFloat(ShaderConstants._ShadowIntensity, lightVolumeRenderer.shadowIntensity);

                    switch (light.lightType)
                    {
                        case LightType.Spot:
                            cmd.DrawMesh(lightVolumeRenderer.volumeMesh,
                                lightVolumeRenderer.transform.localToWorldMatrix,
                                mat, 0, (int)ShaderPass.VOLUMETRIC_LIGHT_SPOT);
                            break;
                        //暂时不支持方向光和点光源，可自行拓展
                        case LightType.Directional:
                            break;
                        case LightType.Point:
                            break;
                    }
                }
                
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup()
        {
            if (volumetricLightTex != null)
            {
                volumetricLightTex.Release();
                volumetricLightTex = null;
            }
        }

        private bool FetchMaterial()
        {
            if (shader == null)
            {
                shader = Shader.Find("Hidden/RecaNoMaho/VolumetricLight");
            }

            if (shader == null)
            {
                return false;
            }

            if (mat == null && shader != null)
            {
                mat = CoreUtils.CreateEngineMaterial(shader);
            }

            if (mat == null)
            {
                return false;
            }

            return true;
        }
        private bool FetchLightVolumeDatas(ref RenderingData renderingData)
        {
            lightVolumeDatas.Clear();
            int additionalLightIndex = -1;
            for (int i = 0; i < renderingData.cullResults.visibleLights.Length; i++)
            {
                VisibleLight visibleLight = renderingData.cullResults.visibleLights[i];
                if (visibleLight.light.TryGetComponent(out LightVolumeRenderer lightVolumeRenderer))
                {
                    lightVolumeDatas.Add(new LightVolumeData()
                    {
                        lightVolumeRenderer = lightVolumeRenderer,
                        lightIndex = i,
                        additionalLightIndex = i == renderingData.lightData.mainLightIndex
                            ? -1 : ++additionalLightIndex,
                        volumeIndex = lightVolumeDatas.Count
                    });
                }
            }

            return lightVolumeDatas.Count != 0;
        }
        
        private void SetupVolumetricLightTexture()
        {
            if (volumetricLightTex != null && (volumetricLightTex.width != sourceDesc.width ||
                                               volumetricLightTex.height != sourceDesc.height))
            {
                volumetricLightTex.Release();
                volumetricLightTex = null;
            }

            if (volumetricLightTex == null)
            {
                volumetricLightTex = new RenderTexture(sourceDesc);
                volumetricLightTex.name = "_VolumetricLightTex";
                volumetricLightTex.filterMode = FilterMode.Bilinear;
                volumetricLightTex.wrapMode = TextureWrapMode.Clamp;
            }
        }
    }
}


