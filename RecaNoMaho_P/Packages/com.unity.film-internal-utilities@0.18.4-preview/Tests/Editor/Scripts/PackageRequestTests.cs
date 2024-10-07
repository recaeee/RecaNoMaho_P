using System.Collections;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;
using UnityEditor.PackageManager;
using UnityEngine.TestTools;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class PackageRequestTests {


//----------------------------------------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator CheckPackageIsInstalled() {
        bool done         = false;
        bool packageFound = false;
        
        PackageRequestJobManager.CreateListRequest(/*offlineMode*/ true, /*includeIndirectIndependencies= */ true,
            /*onSuccess=*/ (packageCollectionReq) => {
                if (null == packageCollectionReq) {
                    done = true;
                    return;
                }
                foreach (PackageInfo packageInfo in packageCollectionReq.Result) {
                    if (packageInfo.name != FilmInternalUtilitiesEditorConstants.PACKAGE_NAME) {
                        continue;
                    }

                    packageFound = true;
                    done         = true;
                    return;

                }
                done = true;

            },
            /*onFail=*/ (packageCollectionReq) => {
                done = true;
            }
        );

        while (!done) {
            yield return null;
        }
        
        Assert.IsTrue(packageFound);
    }
}

} //end namespace
