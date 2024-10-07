using UnityEditor;
using UnityEngine;


namespace Unity.FilmInternalUtilities.Editor {

internal static class EditorWindowExtensions {
    internal static Vector2 GetWindowSize(this EditorWindow editorWindow) {
        Rect pos = editorWindow.position;
        return new Vector2(pos.width, pos.height);
    }
    
    internal static void Resize(this EditorWindow window, Vector2Int windowSize) {
        Rect pos = window.position;
        pos.width       = windowSize.x;
        pos.height      = windowSize.y;
        window.position = pos;
    }
    
}

} //end namespace

