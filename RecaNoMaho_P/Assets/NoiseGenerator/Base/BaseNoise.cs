using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class BaseNoise : MonoBehaviour {
    public string saveToDiskPath = "Assets/CommonRes/Noise";

    [Space]
    [Space]
    [Space]
    public int resolution = 256;
    [Range(1, 32)] public float frequency = 4;
    public bool is3D = false;
    public bool isTilable = true;
    [Range(1, 100)] public float randomSeed = 1;
    public bool autoReseed = true;
    public Vector3 evolution = Vector3.zero;
    public TextureImporterCompression compression = TextureImporterCompression.Uncompressed;

    [Space]
    [Space]
    [Space]
    [Range(0, 8)] public int fbmIteration = 0;

    [Space]
    [Space]
    [Space]
    public bool remapTo01 = true;
    public bool invert;
    public bool changeContrast;
    [Range(0, 5)] public float contrast = 1;
    
    [Space]
    [Space]
    [Space]
    public ComputeShader cs_core;
    public ComputeShader cs_postProcess;

    [Space]
    [Space]
    [Space]
    public Material viewMaterial2D;
    public Material viewMaterial3D;

    protected ComputeBuffer tempComputeBuffer;
    protected RenderTexture tempRenderTexture2D;
    protected RenderTexture tempRenderTexture3D;

    private void OnDisable() {
        ReleaseTempResources();
    }

    public virtual void Generate() {
        ReleaseTempResources();

        if (autoReseed) {
            randomSeed = Random.Range(1f, 100f);
        }

        int resolutionZ = 1;
        if (is3D) {
            resolutionZ = resolution;
        }

        tempComputeBuffer = new ComputeBuffer(resolution * resolution * resolutionZ, 16);

        if (is3D) {
            tempRenderTexture2D = new RenderTexture(4, 4, 0, RenderTextureFormat.R8);
            tempRenderTexture2D.enableRandomWrite = true;

            tempRenderTexture3D = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.R8);
            tempRenderTexture3D.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            tempRenderTexture3D.volumeDepth = resolution;
            tempRenderTexture3D.wrapMode = TextureWrapMode.Repeat;
            tempRenderTexture3D.filterMode = FilterMode.Bilinear;
            tempRenderTexture3D.enableRandomWrite = true;
        }
        else {
            tempRenderTexture2D = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.R8);
            tempRenderTexture2D.wrapMode = TextureWrapMode.Repeat;
            tempRenderTexture2D.filterMode = FilterMode.Bilinear;
            tempRenderTexture2D.enableRandomWrite = true;

            tempRenderTexture3D = new RenderTexture(4, 4, 0, RenderTextureFormat.R8);
            tempRenderTexture3D.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            tempRenderTexture3D.enableRandomWrite = true;
        }

        int kernel = cs_core.FindKernel("Main");
        cs_core.SetBuffer(kernel, "_Colors", tempComputeBuffer);
        cs_core.SetTexture(kernel, "_Texture2D", tempRenderTexture2D);
        cs_core.SetTexture(kernel, "_Texture3D", tempRenderTexture3D);

        cs_core.SetInt("_Resolution", resolution);
        cs_core.SetFloat("_Frequency", frequency);
        cs_core.SetBool("_Is3D", is3D);
        cs_core.SetBool("_IsTilable", isTilable);
        cs_core.SetFloat("_RandomSeed", randomSeed);
        cs_core.SetVector("_Evolution", evolution);
        cs_core.SetInt("_FBMIteration", fbmIteration);

        int dispatchX = Mathf.CeilToInt(resolution / 16f);
        int dispatchY = Mathf.CeilToInt(resolution / 16f);

        cs_core.Dispatch(kernel, dispatchX, dispatchY, resolutionZ);

        if (ShouldPostProcess()) {
            cs_postProcess.SetBuffer(kernel, "_Colors", tempComputeBuffer);
            cs_postProcess.SetTexture(kernel, "_Texture2D", tempRenderTexture2D);
            cs_postProcess.SetTexture(kernel, "_Texture3D", tempRenderTexture3D);

            cs_postProcess.SetInt("_Resolution", resolution);
            cs_postProcess.SetBool("_Is3D", is3D);
            cs_postProcess.SetBool("_RemapTo01", remapTo01);
            cs_postProcess.SetBool("_Invert", invert);
            cs_postProcess.SetBool("_ChangeContrast", changeContrast);

            if (remapTo01) {
                Color[] colors = new Color[tempComputeBuffer.count];
                tempComputeBuffer.GetData(colors);
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                for (int i = 0; i < colors.Length; i++) {
                    min = Mathf.Min(min, colors[i].r);
                    max = Mathf.Max(max, colors[i].r);
                }

                cs_postProcess.SetFloat("_MinValue", min);
                cs_postProcess.SetFloat("_MaxValue", max);
            }

            if (changeContrast) {
                cs_postProcess.SetFloat("_Contrast", contrast);
            }

            cs_postProcess.Dispatch(kernel, dispatchX, dispatchY, resolutionZ);
        }

        if (is3D) {
            viewMaterial3D.SetTexture("_BaseMap", tempRenderTexture3D);
        }
        else {
            viewMaterial2D.SetTexture("_BaseMap", tempRenderTexture2D);
        }
    }

    public void SaveToDisk() {
        if (is3D) {
            Texture3D texture = new Texture3D(resolution, resolution, resolution, TextureFormat.R8, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] colors = new Color[tempComputeBuffer.count];
            tempComputeBuffer.GetData(colors);
            texture.SetPixels(colors);
            texture.Apply();

            string path = saveToDiskPath + ".asset";

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();

            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.Refresh();

            viewMaterial3D.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(path));
        }
        else {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.R8, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color[] colors = new Color[tempComputeBuffer.count];
            tempComputeBuffer.GetData(colors);
            texture.SetPixels(colors);
            texture.Apply();

            string path = saveToDiskPath + ".png";

            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.Refresh();

            viewMaterial2D.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(path));

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.SingleChannel;
            importer.mipmapEnabled = false;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }
    }

    protected void ReleaseTempResources() {
        if (tempRenderTexture2D != null)
            tempRenderTexture2D.Release();

        if (tempRenderTexture3D != null)
            tempRenderTexture3D.Release();

        if (tempComputeBuffer != null)
            tempComputeBuffer.Release();
    }

    protected bool ShouldPostProcess() {
        if (remapTo01) {
            return true;
        }

        if (invert) {
            return true;
        }

        if (changeContrast) {
            return true;
        }

        return false;
    }
}