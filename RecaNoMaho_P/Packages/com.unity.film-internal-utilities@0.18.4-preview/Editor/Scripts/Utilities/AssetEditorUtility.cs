using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Unity.FilmInternalUtilities.Editor {

/// <summary>
/// A utility class for executing operations related to Unity assets in the editor.
/// </summary>
internal static class AssetEditorUtility {

    /// <summary>
    /// Pings (highlights) an asset by its path in the Project window.
    /// The path can be absolute, or relative to the Unity project folder.
    /// </summary>
    /// <param name="path">The asset path.</param>
    /// <returns>True if the asset is found. False otherwise.</returns>
    public static bool PingAssetByPath(string path) {
        Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizePath(path));
        if (asset == null) 
            return false;
        
        EditorGUIUtility.PingObject(asset);
        return true;        
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Creates an asset in a given path from an Object.
    /// This will overwrite the existing asset if it exists.
    /// </summary>
    /// <param name="asset">The object to be created as an asset.</param>
    /// <param name="path">The path of the asset, relative to the Unity project folder.</param>
    public static void OverwriteAsset(Object asset, string path) {
        if (File.Exists(path)) {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(asset, path);
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    public static T LoadAssetByGUID<T>(string guid) where T:UnityEngine.Object {
        string path  = AssetDatabase.GUIDToAssetPath(guid);
        T      asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset;
    }        
    
    //return a set of paths
    //exactAssetName: the exact asset name without extention
    /// <summary>
    /// return a collection of asset paths that match the requirements in the AssetDatabase
    /// </summary>
    /// <param name="filterPrefix">a filter similar to the filter parameter in AssetDatabase.FindAssets()</param>
    /// <param name="exactAssetName">the exact asset name without extension</param>
    /// <param name="searchInFolders">the folders where the search will start.</param>
    /// <param name="shouldSearchSubFolder">should search sub-folders or not</param>
    /// <returns></returns>
    public static HashSet<string> FindAssetPaths(string filterPrefix, string exactAssetName=null, 
        string[] searchInFolders = null, bool shouldSearchSubFolder = true) 
    {
                
        string[] guids = AssetDatabase.FindAssets($"{filterPrefix} {exactAssetName}", searchInFolders);
        
        HashSet<string> foundAssetPaths = new HashSet<string>();
        foreach (string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (null != exactAssetName && exactAssetName != Path.GetFileNameWithoutExtension(path))
                continue;

            if (shouldSearchSubFolder || null == searchInFolders) {
                foundAssetPaths.Add(path);
                continue;
            }

            //exact folder required
            string folder = PathUtility.GetDirectoryName(path,1);
            foreach (string searchedFolder in searchInFolders) {
                if (folder != searchedFolder) 
                    continue;
                
                foundAssetPaths.Add(path);
                break;
            }
        }
        return foundAssetPaths;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Create an asset file for the active scene, using the object name as the file name when applicable.
    /// If the active scene has been saved, then this asset will also be saved in the scene folder.
    /// Otherwise, the asset will be saved under the root "Assets" folder
    /// </summary>
    /// <param name="obj">The object to be saved to the asset file.</param>
    /// <param name="ext">The extension of the asset file.</param>
    /// <returns>The path to the asset</returns>
    public static string CreateSceneAsset(Object obj, string ext = "asset") {
        Assert.IsNotNull(obj);
        
        Scene  activeScene = SceneManager.GetActiveScene();
        string assetDir    = string.IsNullOrEmpty(activeScene.path) ? "Assets" : Path.GetDirectoryName(activeScene.path);

        Directory.CreateDirectory(assetDir);
        
        string assetName   = string.IsNullOrEmpty(obj.name) ? obj.GetType().Name : obj.name;
        string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetDir, assetName) + $".{ext}");
            
        AssetDatabase.CreateAsset(obj, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return path;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Delete assets/files in a given path with specified file patterns.
    /// This can delete files both inside and outside Unity project folder.
    /// </summary>
    /// <param name="path">The path which contain the assets to be deleted.</param>
    /// <param name="searchPattern">The pattern of the files. Ex: "*.prefab"</param>
    public static void DeleteAssetsOrFiles(string path, string searchPattern) {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        bool isUnityAsset = path.StartsWith(Application.dataPath);
        Action<string> onFileFound = null;
        if (isUnityAsset) {
            onFileFound = (string filePath) => {
                AssetDatabase.DeleteAsset(NormalizePath(filePath.Replace('\\','/')));
            };
        } else {
            onFileFound = (string filePath) => {
                File.Delete(filePath);
            };            
        }

        EnumerateFiles(path, searchPattern, onFileFound);        
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Normalize an absolute path under Unity project to make it relative to the Unity project folder.
    /// Paths that are outside Unity project will be unchanged.
    /// Will always return with directory separators using slash ('/'), but can handle both slash/backslash
    /// as the directory separator for input. 
    /// Ex: C:/TempUnityProject/Assets/Foo.prefab => Assets/Foo.prefab
    ///     C:/NonUnityProject/Foo.prefab => C:/NonUnityProject/Foo.prefab
    /// </summary>
    /// <param name="path">The path to be normalized.</param>
    /// <returns>The normalized path.</returns>
    public static string NormalizePath(string path) {
        if (string.IsNullOrEmpty(path))
            return null;

        string slashedPath = path.Replace('\\', '/');        
        string projectRoot = GetApplicationRootPath();

        if (!slashedPath.StartsWith(projectRoot)) 
            return slashedPath;
        
        string normalizedPath = slashedPath.Substring(projectRoot.Length);
        if (normalizedPath.Length > 0) {
            normalizedPath = normalizedPath.Substring(1); //1 for additional '/'           
        }

        return normalizedPath;
    }    
    
//----------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns whether the path points to a path under "Assets" folder
    /// </summary>
    public static bool IsPathNormalized(string path) {
        if (string.IsNullOrEmpty(path))
            return false;
            
        string   normalizedPath = NormalizePath(path);
        string[] dirs           = normalizedPath.Split('/');
        return (dirs.Length > 0 && (dirs[0] == "Assets" || dirs[0] == "Packages" || dirs[0] == "Library"));
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------

    internal static string GetApplicationRootPath() {
        if (null != m_appRootPath)
            return m_appRootPath;

        //Not using Application.dataPath because it may not be called in certain times, e.g: during serialization
        
        m_appRootPath = System.IO.Directory.GetCurrentDirectory().Replace('\\','/');
        return m_appRootPath;
    }
    
    private static void EnumerateFiles(string path, string searchPattern, Action<string> onFileFound) {
        DirectoryInfo di    = new DirectoryInfo(path);
        FileInfo[]    files = di.GetFiles(searchPattern);
        foreach (FileInfo fi in files) {
            onFileFound(fi.FullName);
        }
        
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string m_appRootPath = null;
    
}

} //end namespace