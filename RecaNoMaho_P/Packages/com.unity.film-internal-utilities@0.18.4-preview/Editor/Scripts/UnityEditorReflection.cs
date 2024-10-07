using System.Reflection;
using UnityEditor;


namespace Unity.FilmInternalUtilities.Editor {

internal static class UnityEditorReflection {
    
    internal static readonly MethodInfo SCROLLABLE_TEXT_AREA_METHOD 
        = typeof(EditorGUI).GetMethod("ScrollableTextAreaInternal", BindingFlags.Static | BindingFlags.NonPublic);

}

} //end namespace

