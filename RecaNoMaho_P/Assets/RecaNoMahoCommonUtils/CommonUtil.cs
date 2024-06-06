using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;


namespace RecaNoMaho
{
    public static class CommonUtil
    {
        enum CommonUtilPass
        {
            BlitAdd = 0,
            BlitBlendOneSrcAlpha = 1,
            CopyDepth = 2
        }
        
        static class ShaderConstants
        {
            public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        }
        
        private static Shader commonUtilShader;

        private static Shader CommonUtilShader
        {
            get
            {
                if (commonUtilShader == null)
                {
                    commonUtilShader = Shader.Find("Hidden/RecaNoMaho/CommonUtil");
                }

                return commonUtilShader;
            }
        }

        private static Material commonUtilMat;

        private static Material CommonUtilMat
        {
            get
            {
                if (commonUtilMat == null)
                {
                    commonUtilMat = CoreUtils.CreateEngineMaterial(CommonUtilShader);
                }

                return commonUtilMat;
            }
        }

        private static Mesh triangleMesh;

        private static Mesh TriangleMesh
        {
            get
            {
                if (triangleMesh == null)
                {
                    float nearClipZ = SystemInfo.usesReversedZBuffer ? 1 : -1;
                    triangleMesh = new Mesh();
                    triangleMesh.vertices = GetFullScreenTriangleVertexPosition(nearClipZ);
                    triangleMesh.uv = GetFullScreenTriangleTexCoord();
                    triangleMesh.triangles = new int[3] { 0, 1, 2 };
                }

                return triangleMesh;
            }
        }

        private static UniversalRenderPipelineAsset universalRenderPipelineAsset;

        private static UniversalRenderPipelineAsset UniversalRenderPipelineAsset
        {
            get
            {
                if (universalRenderPipelineAsset == null)
                {
                    universalRenderPipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                }

                return universalRenderPipelineAsset;
            }
        }

        private static UniversalRenderer universalRenderer;

        private static UniversalRenderer UniversalRenderer
        {
            get
            {
                if (universalRenderer == null)
                {
                    universalRenderer = UniversalRenderPipelineAsset.scriptableRenderer as UniversalRenderer;
                }

                return universalRenderer;
            }
        }

        private static Dictionary<string, ScriptableRendererFeature> cachedScriptableRendererFeatures =
            new Dictionary<string, ScriptableRendererFeature>();

        // Should match Common.hlsl
        static Vector3[] GetFullScreenTriangleVertexPosition(float z /*= UNITY_NEAR_CLIP_VALUE*/)
        {
            var r = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                Vector2 uv = new Vector2((i << 1) & 2, i & 2);
                r[i] = new Vector3(uv.x * 2.0f - 1.0f, uv.y * 2.0f - 1.0f, z);
            }
            return r;
        }
        
        // Should match Common.hlsl
        static Vector2[] GetFullScreenTriangleTexCoord()
        {
            var r = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                if (SystemInfo.graphicsUVStartsAtTop)
                    r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
                else
                    r[i] = new Vector2((i << 1) & 2, i & 2);
            }
            return r;
        }

        public static void BlitAdd(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            CommonUtilMat.SetVector(ShaderConstants._BlitScaleBias, new Vector4(1, 1, 0, 0));
            cmd.SetGlobalTexture(ShaderConstants._BlitTexture, source);
            cmd.SetRenderTarget(destination, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            DrawTriangle(cmd, CommonUtilMat, (int)CommonUtilPass.BlitAdd);
        }
        
        public static void BlitBlendOneSrcAlpha(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            CommonUtilMat.SetVector(ShaderConstants._BlitScaleBias, new Vector4(1, 1, 0, 0));
            cmd.SetGlobalTexture(ShaderConstants._BlitTexture, source);
            cmd.SetRenderTarget(destination, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            DrawTriangle(cmd, CommonUtilMat, (int)CommonUtilPass.BlitBlendOneSrcAlpha);
        }
        
        public static void BlitCopyDepth(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            CommonUtilMat.SetVector(ShaderConstants._BlitScaleBias, new Vector4(1, 1, 0, 0));
            cmd.SetGlobalTexture(ShaderConstants._BlitTexture, source);
            cmd.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            DrawTriangle(cmd, CommonUtilMat, (int)CommonUtilPass.CopyDepth);
        }

        private static void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
        {
            if (SystemInfo.graphicsShaderLevel < 30)
            {
                cmd.DrawMesh(TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
            }
            else
            {
                //When the command buffer executes, this will do a draw call on the GPU, without any vertex or index buffers. This is mainly useful on Shader Model 4.5 level hardware where shaders can read arbitrary data from ComputeBuffer buffers.
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1);
            }
        }

        public static UniversalRenderPipelineAsset GetUniversalRenderPipelineAsset()
        {
            return UniversalRenderPipelineAsset;
        }

        public static UniversalRenderer GetUniversalRenderer()
        {
            return UniversalRenderer;
        }

        public static ScriptableRendererFeature GetScriptableRenderFeature(string featureName)
        {
            //How..
            return null;
        }
    }
}

