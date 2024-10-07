using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;


namespace Unity.FilmInternalUtilities.Tests {

internal class EnumUtilityTests {

    [Test]
    public void ConvertEnumToValueList() {
        int numElements = m_enumValues.Count;
        NUnit.Framework.Assert.Greater(numElements,0);
        
        Assert.AreEqual(m_enumValues[0], (TestEnum) INSPECTOR_KEY_0);
        Assert.AreEqual(m_enumValues[1], (TestEnum) INSPECTOR_KEY_1);
        Assert.AreEqual(m_enumValues[2], (TestEnum) INSPECTOR_KEY_2);
        Assert.AreEqual(m_enumValues[3], (TestEnum) INSPECTOR_KEY_3);
        Assert.AreEqual(m_enumValues[4], (TestEnum) INSPECTOR_KEY_4);
    }

    [Test]
    public void ConvertEnumToInspectorNames() {
        List<string> inspectorNames = EnumUtility.ToInspectorNames(typeof(TestEnum));
        string[]     names         = Enum.GetNames(typeof(TestEnum));
        Assert.AreEqual(names.Length, inspectorNames.Count);
                    
        Assert.AreEqual(INSPECTOR_NAME_0, inspectorNames[0]);
        Assert.AreEqual(INSPECTOR_NAME_1, inspectorNames[1]);
        Assert.AreEqual(INSPECTOR_NAME_2, inspectorNames[2]);
        Assert.AreEqual(INSPECTOR_NAME_3, inspectorNames[3]);
        Assert.AreEqual(names[4], inspectorNames[4]);
    }
    
    [Test]
    public void ConvertEnumToInspectorDictionary() {
        Dictionary<int, string> inspectorNameDictionary = EnumUtility.ToInspectorNameDictionary(typeof(TestEnum));
        string[]                names                   = Enum.GetNames(typeof(TestEnum));
        Assert.AreEqual(names.Length, inspectorNameDictionary.Count);
                    
        Assert.AreEqual(INSPECTOR_NAME_0, inspectorNameDictionary[INSPECTOR_KEY_0]);
        Assert.AreEqual(INSPECTOR_NAME_1, inspectorNameDictionary[INSPECTOR_KEY_1]);
        Assert.AreEqual(INSPECTOR_NAME_2, inspectorNameDictionary[INSPECTOR_KEY_2]);
        Assert.AreEqual(INSPECTOR_NAME_3, inspectorNameDictionary[INSPECTOR_KEY_3]);
        Assert.AreEqual(names[4], inspectorNameDictionary[INSPECTOR_KEY_4]);
    }

    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    enum TestEnum {
        [InspectorName(INSPECTOR_NAME_0)] Foo     = INSPECTOR_KEY_0,
        [InspectorName(INSPECTOR_NAME_1)] Bar     = INSPECTOR_KEY_1,
        [InspectorName(INSPECTOR_NAME_2)] Foo_Bar = INSPECTOR_KEY_2,
        [InspectorName(INSPECTOR_NAME_3)] Goo     = INSPECTOR_KEY_3,
        Hoge = INSPECTOR_KEY_4,
    };
    
    const string INSPECTOR_NAME_0 ="foo";
    const string INSPECTOR_NAME_1 ="bar";
    const string INSPECTOR_NAME_2 ="Foo_Bar";
    const string INSPECTOR_NAME_3 ="GoO";

    const int INSPECTOR_KEY_0 = 0;
    const int INSPECTOR_KEY_1 = 10;
    const int INSPECTOR_KEY_2 = 20;
    const int INSPECTOR_KEY_3 = 30;
    const int INSPECTOR_KEY_4 = 40;
    
    private readonly List<TestEnum> m_enumValues = EnumUtility.ToValueList<TestEnum>();
    
}
 

        
        
} //end namespace
