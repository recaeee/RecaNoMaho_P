using System.Collections;

namespace Unity.FilmInternalUtilities {

internal static class YieldUtility {
                
    internal static IEnumerator WaitForFrames(int numFrames) {
        
        for (int i = 0; i < numFrames; ++i) {
            yield return null;
            
        }
    }
}

} //end namespace