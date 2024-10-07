using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;

namespace Unity.FilmInternalUtilities.EditorTests {

internal class PackageVersionTests {
                
    [Test]
    public void ParseValidPackageVersions() {
        ParseAndVerifyVersion("1.0.2-preview"              , 1, 0, 2, PackageLifecycle.PREVIEW, null);
        ParseAndVerifyVersion("9.3.5-preview.1"            , 9, 3, 5, PackageLifecycle.PREVIEW, "1");
        ParseAndVerifyVersion("4.0.5-experimental.alpha.1" , 4, 0, 5, PackageLifecycle.EXPERIMENTAL, "alpha.1");
        ParseAndVerifyVersion("3.0.4-pre.10"               , 3, 0, 4, PackageLifecycle.PRERELEASE, "10");
        ParseAndVerifyVersion("7.0.2.final"                , 7, 0, 2, PackageLifecycle.RELEASED, "final");

        ParseAndVerifyVersion("x.0.x-preview" , null, 0, null, PackageLifecycle.PREVIEW, null);
        ParseAndVerifyVersion("3.x.10-pre.beta" , 3, null, 10, PackageLifecycle.PRERELEASE, "beta");
        
    }

    [Test]
    public void ParseInvalidPackageVersions() {
        Assert.IsFalse(PackageVersion.TryParse("aa.1.2-preview", out PackageVersion _));
        Assert.IsFalse(PackageVersion.TryParse("10.y.71", out PackageVersion _));
        Assert.IsFalse(PackageVersion.TryParse("4.5.z", out PackageVersion _));
        Assert.IsFalse(PackageVersion.TryParse("x.y.z", out PackageVersion _));
        
    }
    
//----------------------------------------------------------------------------------------------------------------------


    private void ParseAndVerifyVersion(string semanticVer, int? major, int? minor, int? patch, 
        PackageLifecycle lifecycle, string additionalMetadata) 
    {
        bool result = PackageVersion.TryParse(semanticVer, out PackageVersion packageVersion);
        UnityEngine.Assertions.Assert.IsTrue(result);
        
        Assert.AreEqual(major, packageVersion.GetMajor());            
        Assert.AreEqual(minor, packageVersion.GetMinor());
        Assert.AreEqual(patch, packageVersion.GetPatch());
        Assert.AreEqual(lifecycle, packageVersion.GetLifeCycle());
        Assert.AreEqual(additionalMetadata, packageVersion.GetMetadata());        
        Assert.AreEqual(semanticVer, packageVersion.ToString());
    }
    
}

} //end namespace