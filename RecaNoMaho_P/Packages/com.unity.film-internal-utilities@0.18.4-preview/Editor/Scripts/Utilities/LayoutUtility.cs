using System.IO;
using System.Reflection;
using Type = System.Type;

namespace Unity.FilmInternalUtilities.Editor {

internal static class LayoutUtility {

//---------------------------------------------------------------------------------------------------------------------

	// path: relative to the project dir
	public static void SaveLayout(string path) {
		string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
        SAVE_WINDOW_LAYOUT_METHOD.Invoke(null, new object[] { fullPath });
	}

//---------------------------------------------------------------------------------------------------------------------
	// path: relative to the project dir
	public static void LoadLayout(string path) {
		string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
		LOAD_WINDOW_LAYOUT_METHOD.Invoke(null, new object[] { fullPath , true });
	}

//---------------------------------------------------------------------------------------------------------------------

	private static Type WINDOW_LAYOUT_TYPE = Type.GetType("UnityEditor.WindowLayout,UnityEditor");

#if UNITY_2023_1_OR_NEWER
	internal static MethodInfo LOAD_WINDOW_LAYOUT_METHOD = WINDOW_LAYOUT_TYPE.GetMethod("TryLoadWindowLayout", 
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(bool) }, null);
#else
	internal static MethodInfo LOAD_WINDOW_LAYOUT_METHOD = WINDOW_LAYOUT_TYPE.GetMethod("LoadWindowLayout", 
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(bool) }, null);
#endif
	
	internal static MethodInfo SAVE_WINDOW_LAYOUT_METHOD = WINDOW_LAYOUT_TYPE.GetMethod("SaveWindowLayout", 
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);

}

} //end namespace