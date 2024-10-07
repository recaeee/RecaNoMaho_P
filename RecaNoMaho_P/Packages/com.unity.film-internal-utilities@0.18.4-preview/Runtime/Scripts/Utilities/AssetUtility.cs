using System;
using System.IO;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.FilmInternalUtilities {

/// <summary>
/// A utility class for executing operations related to Unity assets.
/// </summary>
internal static class AssetUtility {
    
    /// <summary>
    /// If the path starts with "Assets/" then returns the relative path under Assets.
    /// Otherwise, returns null.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <returns>The relative path.</returns>

    [CanBeNull]
    public static string ToAssetRelativePath(string path) {
        if (string.IsNullOrEmpty(path))
            return null;

        if (!IsAssetPath(path, out string slashedPath)) 
            return null;
        
        string normalizedPath = slashedPath.Substring(m_assetPathPrefix.Length);
        return normalizedPath;
    }

    /// <summary>
    /// If the path starts with "Assets/" then this function will find Resources folder under it,
    /// and return a path relative to the Resources folder.
    /// Otherwise, returns null.
    /// </summary>
    /// <param name="path">The source path.</param>
    /// <returns>The relative path.</returns>
    
    [CanBeNull]
    public static string ToResourcesRelativePath(string path) {
        if (!IsAssetPath(path, out string slashedPath)) 
            return null;

        const string RESOURCE_TOKEN = "/Resources/";

        int pos = path.IndexOf(RESOURCE_TOKEN, StringComparison.Ordinal);
        if (pos < 0)
            return null;
        
        pos += RESOURCE_TOKEN.Length;
        
        string relPath       = path.Substring(pos);
        string dir           = Path.GetDirectoryName(relPath);
        string fileNameNoExt = Path.GetFileNameWithoutExtension(relPath);


        return string.IsNullOrEmpty(dir) ? fileNameNoExt : $"{dir.Replace('\\','/')}/{fileNameNoExt}";
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Returns whether the path is under "Assets" folder
    /// </summary>        
    public static bool IsAssetPath(string path, out string convertedPath) {
        if (null == path) {
            convertedPath = null;
            return false;
        }
        
        Assert.IsNotNull(path);
        convertedPath = path.Replace('\\', '/');
        return convertedPath.StartsWith(m_assetPathPrefix);
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    private const  string m_assetPathPrefix = "Assets/";
        
}

} //end namespace