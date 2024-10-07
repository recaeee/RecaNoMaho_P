using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FilmInternalUtilities.Tests {

[AddComponentMenu("")]
internal class DummySerializedDictionaryComponent : MonoBehaviour {


    internal void AddElement(DummyScriptableObject obj, int num) {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Add Dictionary");
#endif
        m_dic2019.Add(obj, num);
        m_dic2020.Add(obj, num);
    }

    internal int GetNumElements() {

#if UNITY_2020_3_OR_NEWER
        UnityEngine.Assertions.Assert.AreEqual(m_dic2019.Count, m_dic2020.Count);
#endif
        return m_dic2019.Count;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    [Serializable]
    internal class DummyScriptableObjectDic : SerializedDictionary<DummyScriptableObject, int> {}
    
    [SerializeField] private DummyScriptableObjectDic m_dic2019 = new DummyScriptableObjectDic();

//----------------------------------------------------------------------------------------------------------------------
    
    //Only works from 2020.x
    [SerializeField] private SerializedDictionary<DummyScriptableObject, int> m_dic2020 = new SerializedDictionary<DummyScriptableObject, int>();

    
}


} //end namespace

