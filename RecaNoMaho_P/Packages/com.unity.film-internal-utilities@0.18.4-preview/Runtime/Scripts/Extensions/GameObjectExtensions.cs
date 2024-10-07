using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Unity.FilmInternalUtilities {


/// <summary>
/// Extension methods for GameObject class.
/// </summary>
internal static class GameObjectExtensions {

    /// <summary>
    /// Destroy all children of a GameObject, while allowing assets to be destroyed.
    /// </summary>
    /// <param name="go">The GameObject which children will be destroyed.</param>
    public static void DestroyChildrenImmediate(this GameObject go) {
        Transform t = go.transform;
        DestroyChildrenImmediate(t);
    }

    /// <summary>
    /// Destroy all children of a Transform, while allowing assets to be destroyed.
    /// </summary>
    /// <param name="t">The Transform which children will be destroyed.</param>
    public static void DestroyChildrenImmediate(this Transform t) {                
        int childCount = t.childCount;        
        for (int i = childCount - 1; i >= 0; --i) {
            Object.DestroyImmediate(t.GetChild(i).gameObject, true);
        }        
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Returns the component of Type type. If one doesn't already exist on the GameObject it will be added.
    /// </summary>
    /// <typeparam name="T">The type of Component to return.</typeparam>
    /// <param name="gameObject">The GameObject this Component is attached to.</param>
    /// <returns>Component</returns>
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component {
        
        T ret = gameObject.GetComponent<T>();
        if (ret == null)
            ret = gameObject.AddComponent<T>();
        return ret;        
    }

   
//----------------------------------------------------------------------------------------------------------------------
#if UNITY_EDITOR
    /// <summary>
    /// Save a GameObject as a prefab.
    /// </summary>
    /// <param name="go">The GameObject to be saved.</param>
    /// <param name="prefabPath">The path of the prefab.</param>
    /// <param name="mode">The interaction mode. Defaults to InteractionMode.AutomatedAction.</param>
    /// <returns>The root GameObject of the saved Prefab Asset, if available.</returns>
    public static GameObject SaveAsPrefab(this GameObject go, string prefabPath, 
        InteractionMode mode = InteractionMode.AutomatedAction) 
    {
        return PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, mode);        
    }
    
    /// <summary>
    /// Returns true when a GameObject is instanced from a prefab. False otherwise.
    /// </summary>
    /// <param name="gameObject">The GameObject to be checked.</param>
    /// <returns>True when a GameObject is instanced from a prefab. False otherwise.</returns>
    public static bool IsPrefabInstance(this GameObject gameObject) {
        PrefabInstanceStatus prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
        return (prefabInstanceStatus != PrefabInstanceStatus.NotAPrefab);

    }


    /// <summary>
    /// Returns true if the GameObject is a prefab or an instanced prefab. False otherwise.
    /// </summary>
    /// <param name="gameObject">The gameObject to be checked.</param>
    /// <returns>True if the GameObject is a prefab or an instanced prefab. False otherwise.</returns>
    public static bool IsPrefab(this GameObject gameObject) {
        PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(gameObject);
        return (prefabAssetType != PrefabAssetType.NotAPrefab);
    }
    
#endif    
    
    
    
}

} //end namespace