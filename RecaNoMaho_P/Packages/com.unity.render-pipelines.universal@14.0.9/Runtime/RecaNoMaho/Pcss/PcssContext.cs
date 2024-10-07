using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal
{
    public static class PcssContext
    {
        public static Matrix4x4[] deviceProjectionMatrixs = new Matrix4x4[4];
        public static Vector4[] deviceProjectionVectors = new Vector4[4];

        public static void UpdateDeviceProjectionMatrixs(ref ShadowSliceData[] shadowSliceDatas)
        {
            //deviceProjection will potentially inverse-Z
            for (int i = 0; i < shadowSliceDatas.Length && i < 4; ++i)
            {
                deviceProjectionMatrixs[i] = GL.GetGPUProjectionMatrix(shadowSliceDatas[i].projectionMatrix, false);
                deviceProjectionVectors[i] = new Vector4(deviceProjectionMatrixs[i].m00, deviceProjectionMatrixs[i].m11,
                    deviceProjectionMatrixs[i].m22, deviceProjectionMatrixs[i].m23);
            }
        }
    }
}