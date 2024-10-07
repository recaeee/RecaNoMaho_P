using NUnit.Framework;
using UnityEngine;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class ObjectEditorUtilityTests {
                
    [Test]
    public void Destroy() {       
        GameObject go = new GameObject();       
        Assert.IsNotNull(go);
        ObjectUtility.Destroy(go);
        UnityEngine.Assertions.Assert.IsNull(go);
    }

//----------------------------------------------------------------------------------------------------------------------

    
}
}