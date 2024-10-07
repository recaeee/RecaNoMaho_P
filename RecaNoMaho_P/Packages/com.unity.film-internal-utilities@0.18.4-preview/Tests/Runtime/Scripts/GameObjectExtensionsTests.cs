using NUnit.Framework;
using UnityEngine;


namespace Unity.FilmInternalUtilities.Tests {

internal class GameObjectExtensionsTests {


    [Test]
    public void GetOrAddComponent() {
        GameObject go     =  new GameObject();
        
        Light light1 = go.GetOrAddComponent<Light>();
        Light light2 = go.GetOrAddComponent<Light>();
        
        Assert.IsNotNull(light1);
        Assert.AreEqual(light1, light2);
    }

}
 

        
        
} //end namespace
