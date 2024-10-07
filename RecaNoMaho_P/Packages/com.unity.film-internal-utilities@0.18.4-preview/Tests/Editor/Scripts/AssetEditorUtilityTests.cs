using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class AssetEditorUtilityTests {
                
    [Test]
    public void CreateAndDeleteScriptableObjectInDataPath() {
        const string TEST_FILE_NAME = "AssetEditorUtilityTest.asset";
       
        IntScriptableObject asset = ScriptableObject.CreateInstance<IntScriptableObject>();
        string path = AssetEditorUtility.NormalizePath(Path.Combine(Application.dataPath, TEST_FILE_NAME));
        AssetEditorUtility.OverwriteAsset(asset,path);        
        Assert.IsTrue(File.Exists(path));

        AssetEditorUtility.DeleteAssetsOrFiles(Application.dataPath, TEST_FILE_NAME);
        Assert.IsFalse(File.Exists(path));                
    }

//----------------------------------------------------------------------------------------------------------------------

    [Test]
    public void CreateAndDeleteTextInTempCachePath() {
        const string TEST_FILE_NAME = "AssetEditorUtilityTest.txt";
        const string TEXT           = "This is a test from AnimeToolbox";
        
        string path = Path.Combine(Application.temporaryCachePath, TEST_FILE_NAME);
        File.WriteAllText(path, TEXT);
        Assert.IsTrue(File.Exists(path));

        AssetEditorUtility.DeleteAssetsOrFiles(Application.temporaryCachePath, TEST_FILE_NAME);
        Assert.IsFalse(File.Exists(path));                
    }


//----------------------------------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator CreateAndLoadAsset() {
        string materialName = "TestRunnerMaterial";

        Material createdMat = new Material(Shader.Find("Standard")) {
            name = materialName
        };
        string path = AssetEditorUtility.CreateSceneAsset(createdMat,"mat");
        
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);

        string[] guids = AssetDatabase.FindAssets($"t:material {materialName}");
        Assert.IsNotNull(guids);
        Assert.Greater(guids.Length,0);

        Material mat = AssetEditorUtility.LoadAssetByGUID<Material>(guids[0]);
        Assert.IsNotNull(mat);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);        

        AssetDatabase.DeleteAsset(path);
    }

//----------------------------------------------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator CreateAndFindAsset() {

        const string TEST_ASSETS_ROOT = "Assets/TestRunner";
        const string MAT_FOLDER       = TEST_ASSETS_ROOT + "/Materials";
        const string MAT_NAME         = "TestRunnerMaterial";
        
        Directory.CreateDirectory(MAT_FOLDER);
        
        string path         = AssetDatabase.GenerateUniqueAssetPath($"{MAT_FOLDER}/{MAT_NAME}.mat");
        AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),path);        
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);

        HashSet<string> paths = AssetEditorUtility.FindAssetPaths("t:material", MAT_NAME);
        Assert.AreEqual(1, paths.Count);
        
        //exact name
        paths = AssetEditorUtility.FindAssetPaths("t:material", MAT_NAME.Substring(0,MAT_NAME.Length-3));
        Assert.AreEqual(0, paths.Count);
                
        paths = AssetEditorUtility.FindAssetPaths("t:material", MAT_NAME, new[]{"Assets"}, shouldSearchSubFolder:true);
        Assert.AreEqual(1, paths.Count);
        
        //exact folder
        paths = AssetEditorUtility.FindAssetPaths("t:material", MAT_NAME, new[]{"Assets"}, shouldSearchSubFolder:false);
        Assert.AreEqual(0, paths.Count);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.DeleteAsset(TEST_ASSETS_ROOT);        
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    [Test]
    [UnityPlatform(RuntimePlatform.WindowsEditor)]    
    public void NormalizeAssetPathOnWindows() {

        const string ASSET_FILE = "Foo.prefab";

        //Under Assets
        string unityAssetPath = Path.Combine(Application.dataPath, ASSET_FILE).Replace(Path.DirectorySeparatorChar,'/'); 
        VerifyNormalizedPath(unityAssetPath, $"Assets/{ASSET_FILE}");
        VerifyNormalizedPath($"Assets/{ASSET_FILE}", $"Assets/{ASSET_FILE}");                

        //Inside project, outside Assets
        string projectRoot = PathUtility.GetDirectoryName(Application.dataPath);
        VerifyNormalizedPath($"{ASSET_FILE}", $"{ASSET_FILE}");                
        VerifyNormalizedPath($"{projectRoot}/{ASSET_FILE}", $"{ASSET_FILE}");
        VerifyNormalizedPath($"{projectRoot}", "");
        
        //Outside project
        const string NON_UNITY_ASSET_PATH = @"C:/NonUnityProject/" + ASSET_FILE;
        VerifyNormalizedPath(NON_UNITY_ASSET_PATH, NON_UNITY_ASSET_PATH);

        
    }

//----------------------------------------------------------------------------------------------------------------------    

    [Test]
    public void VerifyAssetAndNonAssetPaths() {

        const string ASSET_FILE = "Foo.prefab";

        //Under Assets
        string unityAssetPath = Path.Combine(Application.dataPath, ASSET_FILE).Replace(Path.DirectorySeparatorChar,'/'); 
        VerifyPathIsAssetPath(unityAssetPath, true);
        VerifyPathIsAssetPath($"Assets/{ASSET_FILE}", true);

        //Under Packages
        VerifyPathIsAssetPath($"{FilmInternalUtilitiesEditorConstants.PACKAGE_PATH}/{ASSET_FILE}", true);

        //Under Library
        VerifyPathIsAssetPath($"Library/PackageCache/{FilmInternalUtilitiesEditorConstants.PACKAGE_NAME}/{ASSET_FILE}", true);
        
        //Inside project, outside Assets
        string projectRoot = PathUtility.GetDirectoryName(Application.dataPath);
        VerifyPathIsAssetPath($"{ASSET_FILE}", false);                
        VerifyPathIsAssetPath($"{projectRoot}/{ASSET_FILE}", false);
        VerifyPathIsAssetPath($"{projectRoot}", false);
        
        //Outside project
        const string NON_UNITY_ASSET_PATH = @"C:/NonUnityProject/" + ASSET_FILE;
        VerifyPathIsAssetPath(NON_UNITY_ASSET_PATH, false);
        
        //Empty strings
        VerifyPathIsAssetPath(null, false);
        VerifyPathIsAssetPath("", false);
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    void VerifyNormalizedPath(string input, string expected) {
        string normalizedPath = AssetEditorUtility.NormalizePath(input);
        Assert.AreEqual(expected, normalizedPath);        
    }
    
    void VerifyPathIsAssetPath(string path, bool expectedResult) {
        bool isAssetPath = AssetEditorUtility.IsPathNormalized(path);
        Assert.AreEqual(expectedResult, isAssetPath);        
    }
    
}

} //end namespace
