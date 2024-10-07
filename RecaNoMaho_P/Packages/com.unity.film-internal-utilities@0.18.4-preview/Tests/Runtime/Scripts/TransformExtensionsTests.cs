using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert; 

namespace Unity.FilmInternalUtilities.Tests {

internal class TransformExtensionsTests {

    [Test]
    public void FindOrCreateChildren() {
        GameObject parent  =  new GameObject("Parent");
        Transform  parentT = parent.transform;
        Transform  child0  = FindOrCreateChildAndVerify(parentT,"Child0");
        
        Transform sameChild = FindOrCreateChildAndVerify(parentT,"Child0");
        Assert.AreEqual(child0, sameChild);

        Transform child1 = FindOrCreateChildAndVerify(parentT,"Child1");
        Assert.AreNotEqual(child0, child1);
    }
//----------------------------------------------------------------------------------------------------------------------    

    [Test]
    public void SetParentOfTransforms() {
        GameObject parent  =  new GameObject("Parent");
        Transform  parentT = parent.transform;

        List<Transform> children = new List<Transform>();
        for (int i = 0; i < 10; ++i) {
            GameObject child =  new GameObject($"Child-{i}");
            children.Add(child.transform);
        }
        
        children.SetParent(parentT);

        foreach (Transform t in children) {
            Assert.IsNotNull(t);
            Assert.AreEqual(parentT, t.parent);
        }
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    static Transform FindOrCreateChildAndVerify(Transform parent, string childName) {
        Transform child = parent.FindOrCreateChild(childName);
        Assert.IsNotNull(child);
        Assert.AreEqual(parent, child.parent);
        return child;
    }

    [Test]
    public void CreateDescendantsAndFindThem() {
        HashSet<Transform> createdObjs = new HashSet<Transform>();
        GameObject         ggParent0   = new GameObject("GreatGrandparent 0");
        GameObject         ggParent1   = new GameObject("GreatGrandparent 1");
        
        List<Transform> gParents = CreateChildren(ggParent0.transform, "Grandparent", 5, (t) => { AddUnique(createdObjs, t); });
        
        //parents
        List<Transform> p0 = CreateChildren(gParents[0].transform, "p0 ", 1,(t)=> {AddUnique(createdObjs, t);});
        List<Transform> p1 = CreateChildren(gParents[1].transform, "p1 ", 3,(t)=> {AddUnique(createdObjs, t);});
        List<Transform> p2 = CreateChildren(gParents[2].transform, "p2 ", 2,(t)=> {AddUnique(createdObjs, t);});
        
        List<Transform> us = CreateChildren(p1[1].transform, "us ", 8,(t)=> {AddUnique(createdObjs, t);});
        
        //Children
        CreateChildren(us[0].transform, "children 0", 1,(t)=> {AddUnique(createdObjs, t);});
        CreateChildren(us[1].transform, "children 1", 1,(t)=> {AddUnique(createdObjs, t);});
        
        foreach (Transform descendant in ggParent0.transform.FindAllDescendants()) {
            Assert.IsTrue(createdObjs.Contains(descendant));
            createdObjs.Remove(descendant);
        }
        
        NUnit.Framework.Assert.Zero(createdObjs.Count);

    }
    
//----------------------------------------------------------------------------------------------------------------------

    static List<Transform> CreateChildren(Transform parent, string childNamePrefix, int numChildren, 
        Action<Transform> onCreate = null) 
    {

        List<Transform> ret = new List<Transform>();
        for (int i = 0; i < numChildren; ++i) {
            GameObject child = new GameObject(childNamePrefix + $" {i}");
            Transform  t     = child.transform;
            t.SetParent(parent);
            ret.Add(t);
            if (null != onCreate)
                onCreate(t);
        }

        return ret;
    }
    
    static void AddUnique(HashSet<Transform> collection, Transform t) {
        Assert.IsFalse(collection.Contains(t));
        collection.Add(t);
    }

}
 

        
        
} //end namespace
