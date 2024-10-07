using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FilmInternalUtilities {

internal static class GameObjectUtility {

    [CanBeNull]
    internal static Transform FindFirstRoot(string objectName) {

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject go in roots) {
            if (go.name != objectName) 
                continue;

            return go.transform;
        }
        return null;
    }

//----------------------------------------------------------------------------------------------------------------------    
    

    // * Returns null if not found
    // * Name Delimiter is '/'
    // * Empty names are ignored
    [CanBeNull]
    internal static Transform FindByPath(Transform parent, string path) {
        string[] names = path.Split('/');
        if (names.Length <= 0)
            return null;

        //if parent is null, search from root 
        Transform t = parent;
        int       tokenStartIdx = 0;
        if (null == t) {
            t = FindFirstRoot(names[0]);
            if (null == t)
                return null;

            tokenStartIdx = 1;
        }

        //loop
        int nameLength = names.Length;
        for (int i = tokenStartIdx; i < nameLength; ++i) {
            string nameToken = names[i];
            if (string.IsNullOrEmpty(nameToken))
                continue;

            t = t.Find(nameToken);
            if (null == t)
                return null;
        }

        return t;        
    }
       
    // * Name Delimiter is '/'
    // * Empty names are ignored
    internal static Transform FindOrCreateByPath(Transform parent, string path, bool worldPositionStays = true) {
        string[] names = path.Split('/');
        if (names.Length <= 0)
            return null;

        //if parent is null, search from root 
        Transform t             = parent;
        int       tokenStartIdx = 0;
        if (null == t) {
            string rootGameObjectName = names[0];
            t = FindFirstRoot(rootGameObjectName);
            if (null == t) {
                GameObject go = new GameObject(rootGameObjectName);
                t = go.GetComponent<Transform>();                
            }
            tokenStartIdx = 1;
        }

        //loop
        int nameLength = names.Length;
        for (int i = tokenStartIdx; i < nameLength; ++i) {
            string nameToken = names[i];
            if (string.IsNullOrEmpty(nameToken))
                continue;

            t = t.FindOrCreateChild(nameToken, worldPositionStays);
            if (null == t)
                return null;
        }

        return t;        
    }
    
    
}

} //end namespace