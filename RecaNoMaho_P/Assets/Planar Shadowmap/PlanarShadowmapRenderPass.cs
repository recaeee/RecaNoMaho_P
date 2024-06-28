using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace RecaNoMaho
{
    public class PlanarShadowmapRenderPass : ScriptableRenderPass
    {
        static class ShaderConstants
        {
            public static readonly int _PlanarShadowmapTex = Shader.PropertyToID("_PlanarShadowmapTex");
            public static readonly int _PlanarHeight = Shader.PropertyToID("_PlanarHeight");
            public static readonly int _GlobalMaxShadowHeight = Shader.PropertyToID("_GlobalMaxShadowHeight");
            public static readonly int _PlanarShadowmapShadowBias = Shader.PropertyToID("_PlanarShadowmapShadowBias");
            public static readonly int _PlanarShadowmapVP = Shader.PropertyToID("_PlanarShadowmapVP");
        }

        private PlanarShadowmapRenderFeature.GlobalParams globalParams;
        private RenderTextureDescriptor planarShadowmapDesc;
        private RenderTexture planarShadowmapTex;
        private List<ShaderTagId> shaderTagIds;
        private FilteringSettings filteringSettings;
        private RenderStateBlock renderStateBlock;

        private Dictionary<int, RenderTextureDescriptor> cachedPlanarShadowmapDescs;
        private Dictionary<int, RenderTexture> cachedPlanarShadowmapTexs;
        private Dictionary<int, Vector3> cachedCameraPositions;
        private Dictionary<int, Quaternion> cachedCameraRotations;
        private Dictionary<int, Vector2> cachedFovAspects;

        public PlanarShadowmapRenderPass(LayerMask layerMask)
        {
            profilingSampler = new ProfilingSampler("PlanarShadowmap");
            shaderTagIds = new List<ShaderTagId>() { new ShaderTagId("PlanarShadowmap")};
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            //Override DepthState
            renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
            renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);

            cachedCameraPositions = new Dictionary<int, Vector3>();
            cachedCameraRotations = new Dictionary<int, Quaternion>();
            cachedFovAspects = new Dictionary<int, Vector2>();
            cachedPlanarShadowmapDescs = new Dictionary<int, RenderTextureDescriptor>();
            cachedPlanarShadowmapTexs = new Dictionary<int, RenderTexture>();
        }
        
        public bool Setup(ref RenderingData renderingData, PlanarShadowmapRenderFeature.GlobalParams globalParams)
        {
            if (!renderingData.shadowData.supportsMainLightShadows || !PlanarShadowmapRenderFeature.actuallyExecute)
            {
                Cleanup();
                return false;
            }
            
            this.globalParams = globalParams;
            planarShadowmapDesc = renderingData.cameraData.cameraTargetDescriptor;
            planarShadowmapDesc.colorFormat = RenderTextureFormat.Shadowmap;
            planarShadowmapDesc.graphicsFormat = GraphicsFormat.None;
            planarShadowmapDesc.depthStencilFormat = GraphicsFormat.D16_UNorm;
            planarShadowmapDesc.autoGenerateMips = false;
            planarShadowmapDesc.useMipMap = false;
            planarShadowmapDesc.msaaSamples = 1;
            planarShadowmapDesc.width = (int)(planarShadowmapDesc.width * globalParams.renderScale);
            planarShadowmapDesc.height = (int)(planarShadowmapDesc.height * globalParams.renderScale);
            //PCF采样需要设置shadowSamplingMode
            planarShadowmapDesc.shadowSamplingMode = ShadowSamplingMode.CompareDepths;
            
            int hash = renderingData.cameraData.camera.GetHashCode();

            if (globalParams.shadowCache)
            {
                RenderTextureDescriptor cachedPlanarShadowmapDesc = planarShadowmapDesc;
                cachedPlanarShadowmapDesc.width = (int)(renderingData.cameraData.cameraTargetDescriptor.width *
                                                        globalParams.cachedPlanarShadowmapRenderScale);
                cachedPlanarShadowmapDesc.height = (int)(renderingData.cameraData.cameraTargetDescriptor.height *
                                                         globalParams.cachedPlanarShadowmapRenderScale);
                cachedPlanarShadowmapDescs[hash] = cachedPlanarShadowmapDesc;
            }
            
            SetupPlanarShadowmapTexture(hash);

            return true;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //注意ProfilingScope目前不能使用named CommandBuffers。
            //Currently there's an issue which results in mismatched markers.
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Camera camera = renderingData.cameraData.camera;
                int hash = camera.GetHashCode();
                //SetupCameraProperties一定要放在SetRenderTarget之前，否则会指向一张未知的RT，应该是在函数内部实现的。这里可能会有额外的SetRenderTarget消耗，实际上我们只需要VP矩阵，可以再优化下。
                context.SetupCameraProperties(camera, false);
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                cmd.SetGlobalMatrix(ShaderConstants._PlanarShadowmapVP, projectionMatrix * camera.worldToCameraMatrix);
                //调整渲染范围
                SetupCamera(cmd, camera);

                if (globalParams.shadowCache && IsCachedPlanarShadowmapDirty(camera))
                {
                    //Render CachedPlanarShadowmapTex "Only when cache is dirty"
                    RendererListDesc cachedRendererListDesc =
                        new RendererListDesc(shaderTagIds.ToArray(), renderingData.cullResults, camera);
                    cachedRendererListDesc.layerMask = globalParams.cachedShadowRendererLayers;
                    cachedRendererListDesc.sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    cachedRendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                    RendererList cachedRendererList = context.CreateRendererList(cachedRendererListDesc);

                    if (cachedRendererList.isValid)
                    {
                        cmd.SetRenderTarget(cachedPlanarShadowmapTexs[hash], RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store);
                        cmd.ClearRenderTarget(true, false, clearColor);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        cmd.DrawRendererList(cachedRendererList);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                    else
                    {
                        Debug.LogError("CachedRendererList is invalid!");
                    }
                }
                
                cmd.SetRenderTarget(planarShadowmapTex, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                if (globalParams.shadowCache)
                {
                    // cmd.ClearRenderTarget(true, false, clearColor);
                    //Shadow Cache情况下，可以使用全屏Blit代替Clear
                    // cmd.Blit(cachedPlanarShadowmapTex, planarShadowmapTex);
                    CommonUtil.BlitCopyDepth(cmd, cachedPlanarShadowmapTexs[hash], planarShadowmapTex);
                }
                else
                {
                    cmd.ClearRenderTarget(true, false, clearColor);
                }
                

                cmd.SetGlobalFloat(ShaderConstants._PlanarHeight, globalParams.planarHeight);
                cmd.SetGlobalFloat(ShaderConstants._GlobalMaxShadowHeight, globalParams.globalMaxShadowHeight);
                cmd.SetGlobalVector(ShaderConstants._PlanarShadowmapShadowBias, globalParams.planarShadowmapShadowBias);
                SetupKeywords(cmd, globalParams.sampleMode);
                
                //这里要先ExecuteCommandBuffer，确保SetRenderTarget在context.DrawRenderers之前执行
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (globalParams.shadowCache)
                {
                    //Render PlanarShadowmapTex
                    RendererListDesc realtimeShadowRendererListDesc =
                        new RendererListDesc(shaderTagIds.ToArray(), renderingData.cullResults, camera);
                    realtimeShadowRendererListDesc.layerMask = globalParams.realtimeShadowRendererLayers;
                    realtimeShadowRendererListDesc.sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    realtimeShadowRendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                    RendererList realtimeShadowRendererList = context.CreateRendererList(realtimeShadowRendererListDesc);
                    cmd.DrawRendererList(realtimeShadowRendererList);
                }
                else
                {
                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    DrawingSettings drawingSettings =
                        RenderingUtils.CreateDrawingSettings(shaderTagIds, ref renderingData, sortFlags);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
                        ref renderStateBlock);
                }
                

                cmd.SetGlobalTexture(ShaderConstants._PlanarShadowmapTex, planarShadowmapTex);
                //PlanarShadowmap 光照计算分支，需要在Shader里自己实现
            }
            //这里context.ExecuteCommandBuffer要放在Scope外面来确保FrameDebugger Nest正确DrawCall范围，属于不符合直觉的写法，但没啥办法，ProfilingScope的注入点官方写的不好。
            //参考https://forum.unity.com/threads/changing-name-of-custom-pass-in-frame-debugger.964112/
            //ProfilingScope probably adds a "start" command to the buffer on creation and "end" on disposal. 
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup()
        {
            if (planarShadowmapTex != null)
            {
                planarShadowmapTex.Release();
                planarShadowmapTex = null;
            }

            if (cachedPlanarShadowmapTexs.Count != 0)
            {
                foreach (KeyValuePair<int, RenderTexture> pair in cachedPlanarShadowmapTexs)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.Release();
                    }
                }
            }

            Shader.DisableKeyword("PLANAR_SHADOWMAP_LIT_PASS");
            Shader.DisableKeyword("PLANAR_SHADOWMAP_LIT_PASS_PCF2X2");
        }

        private void SetupPlanarShadowmapTexture(int hash)
        {
            if (planarShadowmapTex != null && (planarShadowmapTex.width != planarShadowmapDesc.width ||
                                                   planarShadowmapTex.height != planarShadowmapDesc.height))
            {
                planarShadowmapTex.Release();
                planarShadowmapTex = null;
            }

            if (planarShadowmapTex == null)
            {
                planarShadowmapTex = new RenderTexture(planarShadowmapDesc);
                planarShadowmapTex.name = "_PlanarShadowmapTex";
                planarShadowmapTex.wrapMode = TextureWrapMode.Clamp;
                //OpenGLES上使用Linear采样有问题。
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
                {
                    planarShadowmapTex.filterMode = FilterMode.Point;
                }
                else
                {
                    planarShadowmapTex.filterMode = FilterMode.Bilinear;
                }
            }

            if (globalParams.shadowCache)
            {
                cachedPlanarShadowmapTexs.TryGetValue(hash, out RenderTexture cachedPlanarShadowmapTex);
                cachedPlanarShadowmapDescs.TryGetValue(hash, out RenderTextureDescriptor cachedPlanarShadowmapDesc);
                if (cachedPlanarShadowmapTex != null &&
                    (cachedPlanarShadowmapTex.width != cachedPlanarShadowmapDesc.width ||
                     cachedPlanarShadowmapTex.height != cachedPlanarShadowmapDesc.height))
                {
                    cachedPlanarShadowmapTex.Release();
                    cachedPlanarShadowmapTex = null;
                }

                if (cachedPlanarShadowmapTex == null)
                {
                    cachedPlanarShadowmapTex = new RenderTexture(cachedPlanarShadowmapDesc);
                    cachedPlanarShadowmapTex.name = "_CachedPlanarShadowmapTex";
                    cachedPlanarShadowmapTex.wrapMode = TextureWrapMode.Clamp;
                    //OpenGLES上使用Linear采样有问题。
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
                    {
                        cachedPlanarShadowmapTex.filterMode = FilterMode.Point;
                    }
                    else
                    {
                        cachedPlanarShadowmapTex.filterMode = FilterMode.Bilinear;
                    }

                    cachedPlanarShadowmapTexs[hash] = cachedPlanarShadowmapTex;
                }
            }
        }
        
        private void SetupCamera(CommandBuffer cmd, Camera camera)
        {
            Matrix4x4 worldToCameraMatrix =  camera.worldToCameraMatrix * Matrix4x4.Translate(-globalParams.offset);
            
            cmd.SetViewProjectionMatrices(worldToCameraMatrix, camera.projectionMatrix);
            Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            cmd.SetGlobalMatrix(ShaderConstants._PlanarShadowmapVP, projectionMatrix * worldToCameraMatrix);
        }

        private Vector3 GetIntersectWithLineAndPlane(Vector3 point, Vector3 direct, Vector3 planeNormal, Vector3 planePoint)
        {
            float d = Vector3.Dot(planePoint - point, planeNormal) / Vector3.Dot(direct.normalized, planeNormal);
            return d * direct.normalized + point;
        }
        
        private void GetBottomCornerFromCameraPos(Camera camera, out Vector3 cornerA, out Vector3 cornerB)
        {
            Vector3 cameraPosition = camera.transform.position;
            Vector3 intersection = GetIntersectWithLineAndPlane(cameraPosition, camera.transform.forward, Vector3.up,
                new Vector3(0, globalParams.planarHeight, 0));
            float distanceAB = Vector3.Distance(cameraPosition, intersection);
            
            float angleA = camera.transform.rotation.eulerAngles.x * Mathf.Deg2Rad;
            float angleB = camera.fieldOfView / 2 * Mathf.Deg2Rad;
            
            float distanceNearB = distanceAB / (1 + (Mathf.Tan(angleB) / Mathf.Tan(angleA)));
            float halfHeight = distanceNearB * Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
            float halfWidth = halfHeight * camera.aspect;

            Vector3 cornerAInCameraSpace = new Vector3(-halfWidth, -halfHeight, -distanceNearB);
            Vector3 cornerBInCameraSpace = new Vector3(halfWidth, -halfHeight, -distanceNearB);


            Matrix4x4 cameraToWorldMatrix = camera.cameraToWorldMatrix;

            cornerA = cameraToWorldMatrix.MultiplyPoint(cornerAInCameraSpace);
            cornerB = cameraToWorldMatrix.MultiplyPoint(cornerBInCameraSpace);
        }
        
        private Vector3 GetCameraPosFromBottomCorners(Camera camera, Vector3 cornerA, Vector3 cornerB)
        {
            Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
            Vector3 cornerAInCameraSpace = worldToCameraMatrix * cornerA;
            Vector3 cornerBInCameraSpace = worldToCameraMatrix * cornerB;

            float halfWidth = Mathf.Abs(cornerAInCameraSpace.x - cornerBInCameraSpace.x) / 2;
            float halfHeight = halfWidth / camera.aspect;
            float distanceNearB = halfHeight / Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
            
            float angleA = camera.transform.rotation.eulerAngles.x * Mathf.Deg2Rad;
            float angleB = camera.fieldOfView / 2 * Mathf.Deg2Rad;

            float distanceAB = distanceNearB * (1 + (Mathf.Tan(angleB) / Mathf.Tan(angleA)));
            Vector3 intersection = new Vector3();
            intersection.y = globalParams.planarHeight;
            intersection.x = (cornerA.x + cornerB.x) / 2f;
            intersection.z = cornerA.z + distanceNearB * Mathf.Tan(angleB) / Mathf.Cos(angleA);

            return intersection - camera.transform.forward * distanceAB;
        }

        private void SetupKeywords(CommandBuffer cmd, PlanarShadowmapRenderFeature.GlobalParams.SampleMode sampleMode)
        {
            if (sampleMode == PlanarShadowmapRenderFeature.GlobalParams.SampleMode.pointFilter)
            {
                cmd.EnableShaderKeyword("PLANAR_SHADOWMAP_LIT_PASS");
                cmd.DisableShaderKeyword("PLANAR_SHADOWMAP_LIT_PASS_PCF2X2");
            }
            else if (sampleMode == PlanarShadowmapRenderFeature.GlobalParams.SampleMode.pcf2X2)
            {
                cmd.DisableShaderKeyword("PLANAR_SHADOWMAP_LIT_PASS");
                cmd.EnableShaderKeyword("PLANAR_SHADOWMAP_LIT_PASS_PCF2X2");
            }
        }

        private bool IsCachedPlanarShadowmapDirty(Camera camera)
        {
            bool isDirty = false;
            //Dirty conditions:
            //1:The parameters of the camera change.Ex:The camera moves over a certain distance.The camera rotates over a certain angle.
            Transform transform = camera.transform;
            int hash = camera.GetHashCode();
            if (cachedCameraPositions.TryGetValue(hash, out Vector3 cachedCameraPosition))
            {
                if (transform.position != cachedCameraPosition)
                {
                    isDirty = true;
                    cachedCameraPositions[hash] = transform.position;
                }
            }
            else
            {
                isDirty = true;
                cachedCameraPositions[hash] = transform.position;
            }

            if (cachedCameraRotations.TryGetValue(hash, out Quaternion cachedCameraRotation))
            {
                if (transform.rotation != cachedCameraRotation)
                {
                    isDirty = true;
                    cachedCameraRotations[hash] = transform.rotation;
                }
            }
            else
            {
                isDirty = true;
                cachedCameraRotations[hash] = transform.rotation;
            }
            
            if (cachedFovAspects.TryGetValue(hash, out Vector2 cachedFovAspect))
            {
                if (!Mathf.Approximately(camera.fieldOfView, cachedFovAspect.x) || !Mathf.Approximately(camera.aspect, cachedFovAspect.y))
                {
                    isDirty = true;
                    cachedFovAspects[hash] = new Vector2(camera.fieldOfView, camera.aspect);
                }
            }
            else
            {
                isDirty = true;
                cachedFovAspects[hash] = new Vector2(camera.fieldOfView, camera.aspect);
            }
            
            //2:New static objects come into the frustum.(Set by gameplay logic.We should always know if there are new objects in current frame.)
            if (PlanarShadowmapRenderFeature.CachedShadowRendererChanged)
            {
                isDirty = true;
                //Reset
                PlanarShadowmapRenderFeature.CachedShadowRendererChanged = false;
            }

            return isDirty;
        }
    }
}