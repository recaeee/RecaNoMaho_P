using System;
using JetBrains.Annotations;
using UnityEngine.Playables;
using UnityEngine.Timeline;


namespace Unity.FilmInternalUtilities {

internal abstract class BaseExtendedClipPlayableAsset<D> : PlayableAsset where D: BaseClipData {

    private void OnDestroy() {          
        m_clipData?.DestroyV();
        
        OnDestroyInternalV();
    }

    protected virtual void OnDestroyInternalV() { }
    
//----------------------------------------------------------------------------------------------------------------------
    
    internal void BindClipData(D data) { m_clipData = data;}         
    
    [CanBeNull]
    internal D GetBoundClipData() { return m_clipData; }
    
    internal T BindNewClipData<T>(TimelineClip clip) where T: D, new() {
        T newData = new T();
        m_clipData = newData;
        m_clipData.SetOwner(clip);
        return newData;
    }    
    
//----------------------------------------------------------------------------------------------------------------------
    
    //[Note-sin: 2021-1-21] BaseClipData stores extra data for TimelineClip
    [NonSerialized] private D m_clipData = null;
    
}

} //end namespace

