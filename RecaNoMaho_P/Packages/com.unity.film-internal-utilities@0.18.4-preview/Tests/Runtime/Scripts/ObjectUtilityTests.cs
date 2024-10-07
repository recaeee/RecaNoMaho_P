using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace Unity.FilmInternalUtilities.Tests {

internal class ObjectUtilityTests {

    [Test]   
    public void CreatePrimitiveAndFindComponents() {
            
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        int instanceID = go.GetInstanceID();

        Assert.IsNotNull(FindComponentWithGameObjectID(ObjectUtility.FindSceneComponents<MeshFilter>(), instanceID));        
        Assert.IsNotNull(FindComponentWithGameObjectID(ObjectUtility.FindSceneComponents<MeshRenderer>(), instanceID));        
        Assert.IsNotNull(FindComponentWithGameObjectID(ObjectUtility.FindSceneComponents<SphereCollider>(), instanceID));        
        
        Object.DestroyImmediate(go);
    }

//----------------------------------------------------------------------------------------------------------------------
    
    [Test]   
    public void ConvertArray() {
        Object[] objs = new Object[] {
            new GameObject(),
            new GameObject(),
            new GameObject()            
        };

        GameObject[] gameObjects = ObjectUtility.ConvertArray<GameObject>(objs);
        foreach (var gameObj in gameObjects) {
            Assert.IsNotNull(gameObj);
            Object.DestroyImmediate(gameObj);
        }
    }

//----------------------------------------------------------------------------------------------------------------------
    
    [UnityTest]   
    public IEnumerator Destroy() {
        GameObject go = new GameObject();       
        Assert.IsNotNull(go);
        ObjectUtility.Destroy(go);
        yield return null;
        UnityEngine.Assertions.Assert.IsNull(go);
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    private static T FindComponentWithGameObjectID<T>(IEnumerable<T> components, int goID) where T : UnityEngine.Component {
        T ret = null;
        Assert.IsNotNull(components);
        var enumerator = components.GetEnumerator();
        while (enumerator.MoveNext() && null == ret) {
            T curComponent = enumerator.Current;
            Assert.IsNotNull(curComponent);
            if (curComponent.gameObject.GetInstanceID() == goID) {
                ret = curComponent;
            }            
        }
        enumerator.Dispose();
        return ret;
        
    }
    
}
 
        
} //end namespace

