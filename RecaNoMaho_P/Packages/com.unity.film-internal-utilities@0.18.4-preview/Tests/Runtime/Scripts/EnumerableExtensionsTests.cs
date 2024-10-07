using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;


namespace Unity.FilmInternalUtilities.Tests {

internal class EnumerableExtensionsTests {

    [Test]
    public void FindIndexInList() {
        List<int> list        = new List<int>() { -100, 0 , 1 , 2, 3};
        int       numElements = list.Count;
        
        for (int i = 0; i < numElements; ++i) {
            Assert.AreEqual(i, list.FindIndex(list[i]));
        }
        Assert.AreEqual(-1, list.FindIndex(100)); 
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    [Test]
    public void FindElementAtInList() {

        List<int> list        = new List<int>() { -100, 0 , 1 , 2, 3};
        int       numElements = list.Count;

        for (int i = 0; i < numElements; ++i) {
            Assert.IsTrue(list.FindElementAt(i, out int element));
            Assert.AreEqual(list[i], element);
        }

        Assert.IsFalse(list.FindElementAt(100, out _));
    }

}
 

        
        
} //end namespace
