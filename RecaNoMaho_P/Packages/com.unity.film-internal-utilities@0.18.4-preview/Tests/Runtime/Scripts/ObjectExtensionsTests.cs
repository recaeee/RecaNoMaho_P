using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;


namespace Unity.FilmInternalUtilities.Tests {

internal class ObjectExtensionsTests {

    [Test]
    public void CheckNullRef() {
        CreateAndDestroyObject(()=> new GameObject());
        CreateAndDestroyObject(()=> new Texture2D(100,100));
        CreateAndDestroyObject(()=> ScriptableObject.CreateInstance<IntScriptableObject>());

    }

//----------------------------------------------------------------------------------------------------------------------    
    void CreateAndDestroyObject<T>(Func<T> createFunc) where T : Object{
        T obj = null;
        Assert.IsTrue(obj.IsNullRef());
        
        obj = createFunc();
        Assert.IsFalse(obj.IsNullRef());

        Object.DestroyImmediate(obj);
        Assert.IsFalse(obj.IsNullRef());

        obj = null;
        Assert.IsTrue(obj.IsNullRef());
        
    }

}
 

        
        
} //end namespace
