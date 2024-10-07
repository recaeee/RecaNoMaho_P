using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FilmInternalUtilities
{

[Serializable]
internal class SerializedDictionary<K,V> : Dictionary<K,V>, ISerializationCallbackReceiver
{
    public void OnBeforeSerialize() {
        m_keys.Clear();
        m_values.Clear();
        foreach (var kv in this) {
            K curKey = kv.Key;
            if (null == curKey)
                continue;
            
            m_keys.Add(kv.Key);
            m_values.Add(kv.Value);
        }
        
    }

    public void OnAfterDeserialize() {
        this.Clear();
        int count = Mathf.Min(m_keys.Count, m_values.Count);
        for (int i = 0; i < count; ++i) {
            K curKey = m_keys[i];
            if (null == curKey)
                continue;
            
            this[m_keys[i]] = m_values[i];            
        }        
    }
    
//----------------------------------------------------------------------------------------------------------------------    
        
    [SerializeField] private List<K> m_keys = new List<K>();
    [SerializeField] private List<V> m_values = new List<V>();
    
}
        

} //namespace Unity.Composition