using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Unity.FilmInternalUtilities 
{

//[Note-sin: 2022-03-30] In theory, we are able to make this IExposedPropertyTable implementation as a non-component,
//But the convention of IExposedPropertyTable seems that it is better to make it as a UnityEngine.Object.
//See ExposedReferenceDrawer.cs 
//https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ScriptAttributeGUI/Implementations/ExposedReferenceDrawer.cs
[AddComponentMenu("")]
[Serializable]
internal class SimpleExposedPropertyTable : MonoBehaviour, IExposedPropertyTable {

    #region IExposedPropertyTable
    public void SetReferenceValue(PropertyName id, Object value) {
        m_referenceMap[id] = value;
    }

    public Object GetReferenceValue(PropertyName id, out bool idValid)
    {
        if (m_referenceMap.TryGetValue(id, out var obj)) {
            idValid = true;
            return obj;
        }

        idValid = false;
        return null;
    }

    public void ClearReferenceValue(PropertyName id) {
        m_referenceMap.Remove(id);
    }
    #endregion

//----------------------------------------------------------------------------------------------------------------------
    
    internal void Add(List<PropertyName> propNames, List<Object> objects) {
        int numData = propNames.Count;
        Assert.AreEqual(numData, objects.Count);
        for (int i = 0; i < numData; ++i) {
            m_referenceMap[propNames[i]] = objects[i];
        }
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    [SerializeField] private SerializedDictionary<PropertyName, Object>  m_referenceMap = new SerializedDictionary<PropertyName, Object>();
    
}


} //end namespace