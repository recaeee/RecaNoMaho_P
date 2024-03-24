using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace RecaNoMaho
{
    public static class CommonUtil
    {
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
            cmd.DrawMesh(TriangleMesh, Matrix4x4.identity, CommonUtilMat, 0, 0);
        }
    }
}

