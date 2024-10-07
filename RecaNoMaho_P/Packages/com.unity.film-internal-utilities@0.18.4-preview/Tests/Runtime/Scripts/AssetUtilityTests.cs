using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.FilmInternalUtilities.Tests {
internal class AssetUtilityTests {
    
//----------------------------------------------------------------------------------------------------------------------
    
    [Test]
    [UnityPlatform(RuntimePlatform.WindowsEditor)]    
    public void ConvertToAssetRelativePathsOnWindows() {

        const string ASSET_FILE = "Foo.prefab";

        //Under Assets
        VerifyAssetRelativePath($"Assets/{ASSET_FILE}", ASSET_FILE);                

        //Inside project, outside Assets
        string projectRoot = PathUtility.GetDirectoryName(Application.dataPath);
        VerifyAssetRelativePath($"{ASSET_FILE}", null);                
        VerifyAssetRelativePath($"{projectRoot}/{ASSET_FILE}", null);
        
        //Outside project
        const string NON_UNITY_ASSET_PATH = @"C:/NonUnityProject/" + ASSET_FILE;
        VerifyAssetRelativePath(NON_UNITY_ASSET_PATH, null);
    }

    [Test]
    public void ConvertToResourceRelativePaths() {

        VerifyResourcesRelativePath("Assets/Resources/foo", "foo");
        VerifyResourcesRelativePath("Assets/Resources/foo.asset", "foo");
        VerifyResourcesRelativePath("Assets/MyFolder/Resources/foo.asset", "foo");
        VerifyResourcesRelativePath("Assets/MyFolder/Resources/Stage0/foo.asset", "Stage0/foo");
        VerifyResourcesRelativePath("Assets/MyFolder/Resources/Stage0/Launch/foo.asset", "Stage0/Launch/foo");
        VerifyResourcesRelativePath("Assets/Resources/Resources/foo.asset", "Resources/foo");
        
        VerifyResourcesRelativePath("Resources/foo", null);
        VerifyResourcesRelativePath("foo", null);
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    [Test]
    public void VerifyAssetAndNonAssetPaths() {

        const string ASSET_FILE = "Foo.prefab";

        //Under Assets
        VerifyPathIsAssetPath($"Assets/{ASSET_FILE}", true);
        
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

    void VerifyAssetRelativePath(string input, string expected) {
        string relPath = AssetUtility.ToAssetRelativePath(input);
        Assert.AreEqual(expected, relPath);
    }
    
    void VerifyResourcesRelativePath(string input, string expected) {
        string resPath = AssetUtility.ToResourcesRelativePath(input);
        Assert.AreEqual(expected, resPath);
    }
    
    
    void VerifyPathIsAssetPath(string path, bool expectedResult) {
        bool isAssetPath = AssetUtility.IsAssetPath(path, out _);
        Assert.AreEqual(expectedResult, isAssetPath);        
    }
    
}

} //end namespace
