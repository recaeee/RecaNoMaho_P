using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RecaNoMaho
{
    public static class ResourcesUtil
    {
        private static ComputeShader packingTexturesCore;

        private static ComputeShader PackingTexturesCore
        {
            get
            {
                if (packingTexturesCore == null)
                {
                    string path = "Assets/RecaNoMahoCommonUtils/ComputeShaders/PackingTextures.compute";
                    packingTexturesCore = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                }

                return packingTexturesCore;
            }
        }
        
        public static void Save3DTextureToDisk(string savePath, ComputeBuffer computeBuffer, GraphicsFormat graphicsFormat, int resolutionX, int resolutionY, int resolutionZ)
        {
            if (String.IsNullOrEmpty(savePath))
            {
                Debug.LogError("savePath can't be null or empty.");
                return;
            }

            Texture3D texture = new Texture3D(resolutionX, resolutionY, resolutionZ, graphicsFormat,
                TextureCreationFlags.None);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] colors = new Color[computeBuffer.count];
            computeBuffer.GetData(colors);
            texture.SetPixels(colors);
            texture.Apply();

            string path = savePath + ".asset";

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();

            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.Refresh();
        }
        
        public static void Save2DTextureToDisk(string savePath, ComputeBuffer computeBuffer, GraphicsFormat graphicsFormat, int resolutionX, int resolutionY)
        {
            if (String.IsNullOrEmpty(savePath))
            {
                Debug.LogError("savePath can't be null or empty.");
                return;
            }

            Texture2D texture = new Texture2D(resolutionX, resolutionY, graphicsFormat, TextureCreationFlags.None);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] colors = new Color[computeBuffer.count];
            computeBuffer.GetData(colors);
            texture.SetPixels(colors);
            texture.Apply();

            string path = savePath + ".asset";

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();

            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.Refresh();
        }

        public static void Packing4SingleChannelTextureToRgbaTexture(string savePath, Texture RChannel,
            Texture GChannel, Texture BChannel, Texture AChannel)
        {
            if (!IsSameResolution(RChannel, GChannel, BChannel, AChannel))
            {
                Debug.LogError("Packing textures failed.");
                return;
            }
            bool is3D = RChannel.dimension == TextureDimension.Tex3D;
            int resolutionX = RChannel.width;
            int resolutionY = RChannel.height;
            int resolutionZ = is3D ? (RChannel as Texture3D)?.depth ?? 1 : 1;
            int kernel = PackingTexturesCore.FindKernel("PackingTexturesRGBA");

            ComputeBuffer computeBuffer = new ComputeBuffer(resolutionX * resolutionY * resolutionZ, 16);
            PackingTexturesCore.SetBuffer(kernel, "_Colors", computeBuffer);

            Texture3D empty3D = new Texture3D(1, 1, 1, TextureFormat.R8, false);
            Texture2D empty2D = new Texture2D(1, 1, TextureFormat.R8, false);
            if (is3D)
            {
                PackingTexturesCore.SetTexture(kernel, "_3DRChannel", RChannel);
                PackingTexturesCore.SetTexture(kernel, "_3DGChannel", GChannel);
                PackingTexturesCore.SetTexture(kernel, "_3DBChannel", BChannel);
                PackingTexturesCore.SetTexture(kernel, "_3DAChannel", AChannel);
                
                PackingTexturesCore.SetTexture(kernel, "_2DRChannel", empty2D);
                PackingTexturesCore.SetTexture(kernel, "_2DGChannel", empty2D);
                PackingTexturesCore.SetTexture(kernel, "_2DBChannel", empty2D);
                PackingTexturesCore.SetTexture(kernel, "_2DAChannel", empty2D);
            }
            else
            {
                PackingTexturesCore.SetTexture(kernel, "_2DRChannel", RChannel);
                PackingTexturesCore.SetTexture(kernel, "_2DGChannel", GChannel);
                PackingTexturesCore.SetTexture(kernel, "_2DBChannel", BChannel);
                PackingTexturesCore.SetTexture(kernel, "_2DAChannel", AChannel);
                
                PackingTexturesCore.SetTexture(kernel, "_3DRChannel", empty3D);
                PackingTexturesCore.SetTexture(kernel, "_3DGChannel", empty3D);
                PackingTexturesCore.SetTexture(kernel, "_3DBChannel", empty3D);
                PackingTexturesCore.SetTexture(kernel, "_3DAChannel", empty3D);
            }
            
            PackingTexturesCore.SetInt("_ResolutionX", resolutionX);
            PackingTexturesCore.SetInt("_ResolutionY", resolutionY);
            PackingTexturesCore.SetInt("_ResolutionZ", resolutionZ);
            PackingTexturesCore.SetBool("_Is3D", is3D);

            int dispatchX = Mathf.CeilToInt(resolutionX / 16f);
            int dispatchY = Mathf.CeilToInt(resolutionY / 16f);
            PackingTexturesCore.Dispatch(kernel, dispatchX, dispatchY, resolutionZ);

            if (is3D)
            {
                Save3DTextureToDisk(savePath, computeBuffer, GraphicsFormat.R8G8B8A8_UNorm, resolutionX, resolutionY,
                    resolutionZ);
            }
            else
            {
                Save2DTextureToDisk(savePath, computeBuffer, GraphicsFormat.R8G8B8A8_UNorm, resolutionX, resolutionY);
            }
            
            Object.DestroyImmediate(empty2D);
            Object.DestroyImmediate(empty3D);
        }
        
        public static void Packing3SingleChannelTextureToRgbTexture(string savePath, Texture RChannel,
            Texture GChannel, Texture BChannel)
        {
            if (!IsSameResolution(RChannel, GChannel, BChannel))
            {
                Debug.LogError("Packing textures failed.");
                return;
            }
            bool is3D = RChannel.dimension == TextureDimension.Tex3D;
            int resolutionX = RChannel.width;
            int resolutionY = RChannel.height;
            int resolutionZ = is3D ? (RChannel as Texture3D)?.depth ?? 1 : 1;
            int kernel = PackingTexturesCore.FindKernel("PackingTexturesRGB");

            ComputeBuffer computeBuffer = new ComputeBuffer(resolutionX * resolutionY * resolutionZ, 16);
            PackingTexturesCore.SetBuffer(kernel, "_Colors", computeBuffer);

            Texture3D empty3D = new Texture3D(1, 1, 1, TextureFormat.R8, false);
            Texture2D empty2D = new Texture2D(1, 1, TextureFormat.R8, false);
            if (is3D)
            {
                PackingTexturesCore.SetTexture(kernel, "_3DRChannel", RChannel);
                PackingTexturesCore.SetTexture(kernel, "_3DGChannel", GChannel);
                PackingTexturesCore.SetTexture(kernel, "_3DBChannel", BChannel);

                PackingTexturesCore.SetTexture(kernel, "_2DRChannel", empty2D);
                PackingTexturesCore.SetTexture(kernel, "_2DGChannel", empty2D);
                PackingTexturesCore.SetTexture(kernel, "_2DBChannel", empty2D);
            }
            else
            {
                PackingTexturesCore.SetTexture(kernel, "_2DRChannel", RChannel);
                PackingTexturesCore.SetTexture(kernel, "_2DGChannel", GChannel);
                PackingTexturesCore.SetTexture(kernel, "_2DBChannel", BChannel);

                PackingTexturesCore.SetTexture(kernel, "_3DRChannel", empty3D);
                PackingTexturesCore.SetTexture(kernel, "_3DGChannel", empty3D);
                PackingTexturesCore.SetTexture(kernel, "_3DBChannel", empty3D);
            }
            
            PackingTexturesCore.SetInt("_ResolutionX", resolutionX);
            PackingTexturesCore.SetInt("_ResolutionY", resolutionY);
            PackingTexturesCore.SetInt("_ResolutionZ", resolutionZ);
            PackingTexturesCore.SetBool("_Is3D", is3D);

            int dispatchX = Mathf.CeilToInt(resolutionX / 16f);
            int dispatchY = Mathf.CeilToInt(resolutionY / 16f);
            PackingTexturesCore.Dispatch(kernel, dispatchX, dispatchY, resolutionZ);

            if (is3D)
            {
                Save3DTextureToDisk(savePath, computeBuffer, GraphicsFormat.R8G8B8A8_UNorm, resolutionX, resolutionY,
                    resolutionZ);
            }
            else
            {
                Save2DTextureToDisk(savePath, computeBuffer, GraphicsFormat.R8G8B8A8_UNorm, resolutionX, resolutionY);
            }
            
            Object.DestroyImmediate(empty2D);
            Object.DestroyImmediate(empty3D);
        }

        private static bool IsSameResolution(Texture textureA, Texture textureB, Texture textureC = null, Texture textureD = null)
        {
            Texture[] textures = { textureA, textureB, textureC, textureD };
            bool[] isValidTexutres = { textureA != null, textureB != null, textureC != null, textureD != null };

            if (isValidTexutres[0] == false || isValidTexutres[1] == false)
            {
                Debug.LogError("TextureA or TextureB is invalid.");
                return false;
            }
            
            bool[] is3DTextures = new bool[4];
            for(int i = 0;i < 4; i++)
            {
                if (isValidTexutres[i])
                {
                    is3DTextures[i] = textures[i] is Texture3D;
                }
            }

            bool isSameDimension = true;
            for (int i = 1; i < 4; i++)
            {
                if (isValidTexutres[i])
                {
                    isSameDimension = isSameDimension && is3DTextures[i] == is3DTextures[0];
                }
            }

            if (!isSameDimension)
            {
                return false;
            }

            Vector3Int[] resolutions = { Vector3Int.zero, Vector3Int.zero, Vector3Int.zero, Vector3Int.zero };
            for (int i = 0; i < 4; i++)
            {
                if (isValidTexutres[i])
                {
                    resolutions[i] = new Vector3Int(textures[i].width, textures[i].height, (textures[i] as Texture3D)?.depth ?? 1);
                }
            }

            bool isSameResolution = true;
            for (int i = 0; i < 4; i++)
            {
                if (isValidTexutres[i])
                {
                    isSameResolution = isSameResolution && resolutions[i] == resolutions[0];
                }
            }
            
            return isSameResolution;
        }
    }
}