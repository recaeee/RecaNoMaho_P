using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.FilmInternalUtilities.Tests {
internal class GameObjectUtilityTests {

    [UnitySetUp]
    public IEnumerator Setup() {
        m_transforms["d"]  = GameObjectUtility.FindOrCreateByPath(null, "a/b/c0/d");
        m_transforms["c0"] = GameObjectUtility.FindOrCreateByPath(null, "a/b/c0");
        m_transforms["c1"] = GameObjectUtility.FindOrCreateByPath(null, "a/b/c1");
        m_transforms["b"]  = GameObjectUtility.FindOrCreateByPath(null, "a/b");
        m_transforms["a"]  = GameObjectUtility.FindOrCreateByPath(null, "a");
        
        yield return null;
    }
    
    [Test]
    public void CheckSetup() {
        Transform a  = VerifySingleRootGameObjectExists("a");
        Transform b  = VerifySingleChildExists(a,"b");
        Transform c0 = VerifySingleChildExists(b,"c0");
        Transform c1 = VerifySingleChildExists(b,"c1");
        Transform d  = VerifySingleChildExists(c0,"d");
        
        Assert.AreEqual(a,  m_transforms["a"]);
        Assert.AreEqual(b,  m_transforms["b"]);
        Assert.AreEqual(c0, m_transforms["c0"]);
        Assert.AreEqual(c1, m_transforms["c1"]);
        Assert.AreEqual(d,  m_transforms["d"]);
    }

//----------------------------------------------------------------------------------------------------------------------        
    
    [Test]
    public void FindGameObjectsByPath() {

        Transform a  = GameObjectUtility.FindByPath(null, "a");
        Transform b  = GameObjectUtility.FindByPath(a, "b");
        Transform c0 = GameObjectUtility.FindByPath(b, "c0");
        Transform c1 = GameObjectUtility.FindByPath(b, "c1");
        Transform d  = GameObjectUtility.FindByPath(null, "a/b/c0/d");
        
        Assert.AreEqual(a,  m_transforms["a"]);
        Assert.AreEqual(b,  m_transforms["b"]);
        Assert.AreEqual(c0, m_transforms["c0"]);
        Assert.AreEqual(c1, m_transforms["c1"]);
        Assert.AreEqual(d,  m_transforms["d"]);
    }


//----------------------------------------------------------------------------------------------------------------------        
    
    [Test]
    public void TryCreateDuplicateChildren() {

        Transform parent      = m_transforms["b"];
        Transform duplicateC0 = GameObjectUtility.FindOrCreateByPath(parent, "c0");
        Transform duplicateC1 = GameObjectUtility.FindOrCreateByPath(parent, "c1");
                
        Assert.AreEqual(duplicateC0, m_transforms["c0"]);
        Assert.AreEqual(duplicateC1, m_transforms["c1"]);
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    //Find root GameObjects with a certain name
    private static Transform VerifySingleRootGameObjectExists(string objectName) {

        Transform ret = null;
        int found = 0;
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject go in roots) {
            if (go.name != objectName) 
                continue;

            ++found;
            ret = go.transform;
        }
        
        Assert.AreEqual(1,found);
        Assert.IsNotNull(ret);
        return ret;
    }

    //Verify that only one child exists with a certain name
    private static Transform VerifySingleChildExists(Transform parent, string childName) {
        Assert.IsNotNull(parent);

        Transform ret   = null;
        int       found = 0;

        int childCount = parent.childCount;
        for (int i = 0; i < childCount; ++i) {
            Transform curChild = parent.GetChild(i);
            if (curChild.name != childName)
                continue;

            ++found;
            ret = curChild.transform;
        }
        
        Assert.AreEqual(1,found);
        Assert.IsNotNull(ret);
        return ret;
    }

//----------------------------------------------------------------------------------------------------------------------
    
    private Dictionary<string, Transform> m_transforms = new Dictionary<string, Transform>();


}

} //end namespace
