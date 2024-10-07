using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.FilmInternalUtilities.Tests {

internal class MonoBehaviourSingletonTests {

    [Test]
    public void CreateInstance() {
        DummySingletonA firstSingleton  = DummySingletonA.GetOrCreateInstance();
        DummySingletonA secondSingleton = DummySingletonA.GetOrCreateInstance();
        Assert.IsNotNull(firstSingleton);        
        Assert.AreEqual(firstSingleton, secondSingleton);        
    }

    [Test]
    public void CreateDifferentSingletons() {
        MonoBehaviour singletonA = DummySingletonA.GetOrCreateInstance();
        MonoBehaviour singletonB = DummySingletonB.GetOrCreateInstance();
        Assert.IsNotNull(singletonA);        
        Assert.IsNotNull(singletonB);
        Assert.AreNotEqual(singletonA, singletonB);
    }

//----------------------------------------------------------------------------------------------------------------------    
    [UnityTest]
    public IEnumerator AddSingletonByAwake() {
        DummySingletonB singleton = new GameObject("TrueSingleton").AddComponent<DummySingletonB>();
        yield return null;
        
        DummySingletonB secondSingleton = DummySingletonB.GetOrCreateInstance();
        
        DummySingletonB thirdSingleton = new GameObject("FakeSingleton").AddComponent<DummySingletonB>();
        yield return null;
        
        Assert.IsNotNull(singleton);
        Assert.IsNotNull(secondSingleton);
        Assert.IsNull(thirdSingleton);
        Assert.AreEqual(singleton, secondSingleton);
    }

//----------------------------------------------------------------------------------------------------------------------    

    [UnityTest]
    public IEnumerator AutoDeleteDuplicateInstance() {
        DummySingletonA singleton     = DummySingletonA.GetOrCreateInstance();
        DummySingletonA fakeSingleton = new GameObject("FakeSingleton").AddComponent<DummySingletonA>();
        yield return null;
    
        Assert.IsNotNull(singleton);
        Assert.IsNull(fakeSingleton);            
    }

}


} //end namespace

