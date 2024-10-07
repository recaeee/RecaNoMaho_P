using System;
using System.Collections;
using UnityEditor;
using UnityEditor.Timeline;

namespace Unity.FilmInternalUtilities.Editor {

internal static class EditorTestsUtility {
                
    [Obsolete("Replaced by YieldEditorUtility.WaitForFramesAndIncrementUndo()")]
    internal static IEnumerator WaitForFrames(int numFrames) {
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(numFrames);
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------    
    
    internal static void UndoAndRefreshTimelineEditor(RefreshReason refreshReason = RefreshReason.ContentsModified) {
        Undo.PerformUndo(); 
        TimelineEditor.Refresh(refreshReason);
    }
    
}

} //end namespace