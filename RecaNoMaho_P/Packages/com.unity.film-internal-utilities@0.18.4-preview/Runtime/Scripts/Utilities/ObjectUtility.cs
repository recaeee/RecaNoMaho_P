using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FilmInternalUtilities {
internal static class ObjectUtility {

    internal static IEnumerable<T> FindSceneComponents<T>(bool includeInactive = true) where T: UnityEngine.Component {
        FindObjectsInactive findObjectsInactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude; 
        foreach (T comp in Object.FindObjectsByType<T>(findObjectsInactive, FindObjectsSortMode.None)) {
            yield return comp;
        }
    }

//--------------------------------------------------------------------------------------------------------------------------------------------------------------       

    internal static T[] ConvertArray<T>(Object[] objs) where T :  UnityEngine.Object{
        int numObjects = objs.Length;
        T[] ret = new T[numObjects];
        for (int i = 0; i < numObjects; i++) {
            ret[i] = objs[i] as T;
        }
        return ret;
    }

//--------------------------------------------------------------------------------------------------------------------------------------------------------------       
    
    [Obsolete("Replaced by Destroy<>(obj, forceImmediate, undo)")]
    internal static void Destroy(Object obj, bool forceImmediate = false) {
        Destroy<Object>(ref obj, forceImmediate, withUndo: true);
    }

    internal static void Destroy<T>(T obj, bool forceImmediate = false, bool withUndo = true) where T: Object {
        Destroy<T>(ref obj, forceImmediate, withUndo);
    }
    
    internal static void Destroy<T>(ref T obj, bool forceImmediate = false, bool withUndo = true) where T: Object {

        //Handle differences between editor/runtime when destroying immediately
#if UNITY_EDITOR
        if (!Application.isPlaying || forceImmediate) {
            if (withUndo)
                Undo.DestroyObjectImmediate(obj);
            else
                Object.DestroyImmediate(obj);
        }
#else
        if (forceImmediate) {
            Object.DestroyImmediate(obj);
        }
#endif
        else {
            Object.Destroy(obj);
        }

        obj = null;
    }
    
    internal static void DestroyImmediate<T>(ref T obj) where T : Object {
        Object.DestroyImmediate(obj);
        obj = null;
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------       
    /// <summary>
    /// Create a GameObject with a Component
    /// </summary>
    /// <param name="goName">The name of the GameObject</param>
    /// <typeparam name="T">The type of the Component</typeparam>
    /// <returns>The newly created GameObject</returns>
    [Obsolete] 
    internal static T CreateGameObjectWithComponent<T>(string goName) where T: Component {
        GameObject go        = new GameObject(goName);
        T          component = go.AddComponent<T>();
        return component;        
    }
}

} //end namespace
