using UnityEngine;

namespace Unity.FilmInternalUtilities {


/// <summary>
/// Extension methods for Texture2D class.
/// </summary>
internal static class Texture2DExtensions {

    /// <summary>
    /// Set the pixels of a Texture2D with a certain color
    /// </summary>
    /// <param name="tex">The Texture2D to be modified.</param>
    /// <param name="color">The color to apply.</param>
    public static void SetPixelsWithColor(this Texture2D tex, Color color) {
        
        Color[] pixels =  tex.GetPixels(); 
        for(int i = 0; i < pixels.Length; ++i) {
            pixels[i] = color;
        }  
        tex.SetPixels( pixels );  
        tex.Apply();                
    }    
    
}

} //end namespace