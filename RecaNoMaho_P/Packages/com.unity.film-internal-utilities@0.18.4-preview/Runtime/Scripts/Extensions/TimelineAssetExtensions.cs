
using UnityEngine.Timeline;

namespace Unity.FilmInternalUtilities {

internal static class TimelineAssetExtensions {

    internal static double GetFPS(this TimelineAsset.EditorSettings editorSettings) {
        
#if AT_USE_TIMELINE_GE_1_6_0                    
        return editorSettings.frameRate;
#else         
        return (double) editorSettings.fps;
#endif
    }
    
    
}

} //end namespace

