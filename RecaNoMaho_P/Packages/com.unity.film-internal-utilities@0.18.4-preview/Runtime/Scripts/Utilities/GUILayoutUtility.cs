using UnityEngine;

namespace Unity.FilmInternalUtilities {

internal static class GUILayoutUtility {

    internal static void SetWithLastRect(ref Rect rect) {
        Rect r = UnityEngine.GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.Repaint) {
            rect = r;
        }
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    internal static void ReserveRect(ref Rect rect, GUIContent content, GUIStyle guiStyle, params GUILayoutOption[] options) {
        Rect r = UnityEngine.GUILayoutUtility.GetRect(content, guiStyle, options);
        if (Event.current.type == EventType.Repaint) {
            rect = r;
        }
    }
    
}

} //end namespace