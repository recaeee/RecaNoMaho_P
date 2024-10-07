using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.FilmInternalUtilities.EditorTests {

internal class SceneComponentsTest {

    [UnityTest]
    public IEnumerator EnsureClearOnSceneClosed() {

        DestroyAllLights(); //Clear all lights first
                
        const int NUM_LIGHTS = 3;
        for (int i = 0; i < NUM_LIGHTS; ++i) {
            new GameObject().AddComponent<Light>();
            SceneComponents<Light>.GetInstance().Update();
            yield return null;
            
            Assert.AreEqual(i + 1 , SceneComponents<Light>.GetInstance().GetCachedComponents().Count);
        }

        yield return null;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return null;
        
        Assert.AreEqual(0 , SceneComponents<Light>.GetInstance().GetCachedComponents().Count);

    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    [Test]
    public void CompareUpdateMethods() {
        DestroyAllLights(); //Clear all lights first
        
        SceneComponents<Light>.GetInstance().Update();
        Assert.AreEqual(0 , SceneComponents<Light>.GetInstance().GetCachedComponents().Count);
        
        const int NUM_LIGHTS = 3;
        for (int i = 0; i < NUM_LIGHTS; ++i) {
            new GameObject().AddComponent<Light>();
            SceneComponents<Light>.GetInstance().Update();
            Assert.AreEqual(i , SceneComponents<Light>.GetInstance().GetCachedComponents().Count);
            
            SceneComponents<Light>.GetInstance().ForceUpdate();
            Assert.AreEqual(i + 1 , SceneComponents<Light>.GetInstance().GetCachedComponents().Count);
        }
    }

//--------------------------------------------------------------------------------------------------------------------------------------------------------------    
    
    void DestroyAllLights() {
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            Object.DestroyImmediate(l.gameObject);
        }
        
    }
    
}

} //end namespace
