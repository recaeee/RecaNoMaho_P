using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Unity.FilmInternalUtilities {

[Serializable]
internal abstract class BaseClipData : ISerializationCallbackReceiver {

//----------------------------------------------------------------------------------------------------------------------
    #region ISerializationCallbackReceiver

    public void OnBeforeSerialize() {
        m_baseClipDataVersion = CUR_CLIP_DATA_VERSION; 
            
        OnBeforeSerializeInternalV();
    }

    public void OnAfterDeserialize() {
        OnAfterDeserializeInternalV();
    }
    
    protected abstract void OnBeforeSerializeInternalV();
    protected abstract void OnAfterDeserializeInternalV();
    
    #endregion
    
//----------------------------------------------------------------------------------------------------------------------
    internal abstract void DestroyV();
    

//----------------------------------------------------------------------------------------------------------------------
    internal void SetOwner(TimelineClip clip) { m_clipOwner = clip;}
    
    internal TimelineClip GetOwner() { return m_clipOwner; }

//----------------------------------------------------------------------------------------------------------------------    
    
    //The owner of this ClipData
    [NonSerialized] private TimelineClip  m_clipOwner = null;

#pragma warning disable 414    
    [HideInInspector][SerializeField] private int m_baseClipDataVersion = CUR_CLIP_DATA_VERSION;        
#pragma warning restore 414    

    private const int    CUR_CLIP_DATA_VERSION = 1;
    
}


} //end namespace


