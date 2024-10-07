using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;


namespace Unity.FilmInternalUtilities.Tests {

internal class RenderTextureExtensionsTests {

    [Test]
    public void WriteRenderTextureToFile() {

        RenderTexture rt = new RenderTexture(64, 64, 0, GraphicsFormat.R8G8B8A8_UNorm);
        rt.ClearAll();

        string fileName = "FilmInternalUtilitiesTestRT";

        VerifyWriteRenderTextureToFile(rt, fileName + ".png", TextureFormat.RGBA32, isPNG: true, isLinear: false);
        VerifyWriteRenderTextureToFile(rt, fileName + ".png", TextureFormat.RGBA32, isPNG: true, isLinear: true);

        VerifyWriteRenderTextureToFile(rt, fileName + ".exr", TextureFormat.RGBAFloat, isPNG: false, isLinear: false);
        VerifyWriteRenderTextureToFile(rt, fileName + ".exr", TextureFormat.RGBAHalf, isPNG: false, isLinear: true);
        
        
        Object.DestroyImmediate(rt);
        
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    private void VerifyWriteRenderTextureToFile(RenderTexture rt, string outputPath, 
        TextureFormat texFormat, bool isPNG, bool isLinear) 
    {
        bool fileWritten = false;

        if (File.Exists(outputPath))
            File.Delete(outputPath);
        Assert.IsFalse(File.Exists(outputPath));

        try {

            fileWritten = rt.WriteToFile(outputPath, texFormat, isPNG, isLinear);
            if (fileWritten) {
                Assert.IsTrue(File.Exists(outputPath));
            }

            //Cleanup
            File.Delete(outputPath);

        }
        catch (Exception e) {
            Debug.LogError(e.ToString());
        }
        
        Assert.IsFalse(File.Exists(outputPath));
        Assert.IsTrue(fileWritten);
        
    }

}
 

        
        
} //end namespace
