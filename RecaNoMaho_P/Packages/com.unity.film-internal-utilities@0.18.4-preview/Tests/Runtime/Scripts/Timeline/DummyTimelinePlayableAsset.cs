using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.PlayerLoop;
using UnityEngine.Timeline;


namespace Unity.FilmInternalUtilities.Tests {

/// <summary>
/// A Dummy Timeline PlayableAsset 
/// </summary>
[System.Serializable]
internal class DummyTimelinePlayableAsset : BaseExtendedClipPlayableAsset<DummyTimelineClipData>, ITimelineClipAsset {

    public sealed override Playable CreatePlayable(PlayableGraph graph, GameObject go) {
        return Playable.Create(graph);
    }
    
    public ClipCaps clipCaps {
        get { return ClipCaps.None; }
    }
//----------------------------------------------------------------------------------------------------------------------

    internal void Init() {
        m_isInitialized = true;
    }

    internal bool IsInitialized() => m_isInitialized;

//----------------------------------------------------------------------------------------------------------------------
    
    private bool m_isInitialized = false;

}

} //end namespace


