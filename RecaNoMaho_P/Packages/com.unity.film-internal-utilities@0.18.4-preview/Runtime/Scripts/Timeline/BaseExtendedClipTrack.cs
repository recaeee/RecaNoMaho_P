using System;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FilmInternalUtilities { 
/// <summary>
/// A track which requires its TimelineClip to store BaseClipData as an extension
/// </summary>
internal abstract class BaseExtendedClipTrack<D> : BaseTrack where D: BaseClipData, new()
{
    
    void OnEnable() {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        BindClipDataToClip();
        OnEnableInternalV();
    }

    private void OnDisable() {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
   }
    
#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange obj) {
        m_isClipDataDictionaryInitialized = false;
    }
#endif

    protected virtual void OnEnableInternalV() { }
    
    
//----------------------------------------------------------------------------------------------------------------------
    /// <inheritdoc/>
    protected override void OnBeforeTrackSerialize() {
        base.OnBeforeTrackSerialize();

        m_serializedDataCollection.Clear();
        
#pragma warning disable 612
        m_obsoleteClipDataCollection     = null;
        m_obsoleteHashClipDataCollection = null;
#pragma warning restore 612
        
        foreach (TimelineClip clip in GetClips()) {
            int hashCode = clip.asset.GetHashCode();

            if (!m_assetHashToClipDataCollection.TryGetValue(hashCode, out D clipData)) {
                BaseExtendedClipPlayableAsset<D> playableAsset = clip.asset as BaseExtendedClipPlayableAsset<D>;
                Assert.IsNotNull(playableAsset);
                clipData = playableAsset.GetBoundClipData();
            }

            if (null == clipData) {
                clipData = new D();
                clipData.SetOwner(clip);
            }

            m_serializedDataCollection.Add(clipData);
        }

        m_baseExtendedClipTrackVersion = CUR_VERSION;
    }
    
    /// <inheritdoc/>
    protected override  void OnAfterTrackDeserialize() {
        base.OnAfterTrackDeserialize();
        
        if (null == m_serializedDataCollection) {
            m_serializedDataCollection = new List<D>();
        }
        
        ConvertLegacyData();
        if (!m_isClipDataDictionaryInitialized) {
            InitClipDataCollection();
        }
        m_baseExtendedClipTrackVersion = CUR_VERSION;        
    }

    private void InitClipDataCollection() {
        m_assetHashToClipDataCollection.Clear();
        IEnumerator<TimelineClip> clipEnumerator     = GetClips().GetEnumerator();
        List<D>.Enumerator        clipDataEnumerator = m_serializedDataCollection.GetEnumerator();
        while (clipEnumerator.MoveNext() && clipDataEnumerator.MoveNext()) {
            TimelineClip clip = clipEnumerator.Current;
            Assert.IsNotNull(clip);

            D clipData = clipDataEnumerator.Current;
            Assert.IsNotNull(clipData);
            
            m_assetHashToClipDataCollection[clip.asset.GetHashCode()] = clipData;
            
        }
        clipEnumerator.Dispose();
        clipDataEnumerator.Dispose();
        m_isClipDataDictionaryInitialized = true;
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    private void ConvertLegacyData() {
        
#pragma warning disable 612        
        
        // SerializedDictionary<TimelineClip, D> format
        if (null != m_obsoleteClipDataCollection && m_obsoleteClipDataCollection.Count > 0) {
            m_serializedDataCollection.Clear();

            Dictionary<TimelineClip, D> remainingClipData = new Dictionary<TimelineClip, D>(m_obsoleteClipDataCollection);
            foreach (TimelineClip clip in GetClips()) {
                if (remainingClipData.TryGetValue(clip, out D clipData)) {
                    m_serializedDataCollection.Add( clipData );
                    remainingClipData.Remove(clip);
                } else {
                    m_serializedDataCollection.Add( null);
                }
            }

            FillNullListElementWithAnyDictionaryElement<TimelineClip>(m_serializedDataCollection, remainingClipData);
            m_obsoleteClipDataCollection.Clear();
            return;
        }

        switch (m_baseExtendedClipTrackVersion) {
            case (int) BaseExtendedClipTrackVersion.SerializedAssetHash_0_16_2: {
                m_serializedDataCollection.Clear();

                Dictionary<int, D> remainingClipData = new Dictionary<int, D>(m_obsoleteHashClipDataCollection);
                foreach (TimelineClip clip in GetClips()) {
                    int hashCode = clip.asset.GetHashCode();
                    if (remainingClipData.TryGetValue(hashCode, out D clipData)) {
                        m_serializedDataCollection.Add( clipData );
                        remainingClipData.Remove(hashCode);
                    } else {
                        m_serializedDataCollection.Add( null);
                    }
                }

                FillNullListElementWithAnyDictionaryElement<int>(m_serializedDataCollection, remainingClipData);
                m_obsoleteHashClipDataCollection.Clear();
                                
                break;
            }
        }
#pragma warning restore 612
        
    }


    static void FillNullListElementWithAnyDictionaryElement<TKey>(List<D> dataCollection, Dictionary<TKey,D> remainingClipData) {
        int                           numData             = dataCollection.Count;
        Dictionary<TKey, D>.Enumerator remainingEnumerator = remainingClipData.GetEnumerator();
        for (int i = 0; i < numData; ++i) {
            if (null == dataCollection[i] && remainingEnumerator.MoveNext()) {
                dataCollection[i] = remainingEnumerator.Current.Value;
            }
        }
        remainingEnumerator.Dispose();
        
    }

//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    /// <inheritdoc/>
    public sealed override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) {
               
        BindClipDataToClip();
        Playable mixer = CreateTrackMixerInternal(graph, go, inputCount);
        
        return mixer;
    }

    protected abstract Playable CreateTrackMixerInternal(PlayableGraph graph, GameObject go, int inputCount);
    

//----------------------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public override string ToString() { return name; }   

        
//----------------------------------------------------------------------------------------------------------------------

    private void BindClipDataToClip() {
        foreach (TimelineClip clip in GetClips()) {
            
            BaseExtendedClipPlayableAsset<D> playableAsset = clip.asset as BaseExtendedClipPlayableAsset<D>;
            if (null == playableAsset)
                continue;

            int hashCode = playableAsset.GetHashCode();
            //Try to get existing one, either from the collection, or the clip
            if (!m_assetHashToClipDataCollection.TryGetValue(hashCode, out D clipData)) {
                clipData = playableAsset.GetBoundClipData();
            }

            if (null == clipData) {
                clipData = new D();
            }

            //Bind
            m_assetHashToClipDataCollection[hashCode] = clipData;
            clipData.SetOwner(clip);
            playableAsset.BindClipData(clipData);
        }
        
    }


    
//----------------------------------------------------------------------------------------------------------------------


    [FormerlySerializedAs("m_clipDataCollection")] [Obsolete][HideInInspector][SerializeField] 
    private SerializedDictionary<TimelineClip, D> m_obsoleteClipDataCollection = null;

    [FormerlySerializedAs("m_hashClipDataCollection")] [HideInInspector][SerializeField][Obsolete]
    private SerializedDictionary<int, D> m_obsoleteHashClipDataCollection = new SerializedDictionary<int, D>();
    
    [FormerlySerializedAs("m_obsoleteDataCollection")] [HideInInspector][SerializeField] 
    List<D> m_serializedDataCollection = new List<D>();
    
    //No direct serialization for this dictionary because asset hash code may be different for different Unity sessions
    private readonly Dictionary<int, D> m_assetHashToClipDataCollection = new Dictionary<int, D>(); 

#pragma warning disable 414
    [HideInInspector][SerializeField] private int m_baseExtendedClipTrackVersion = CUR_VERSION;
#pragma warning restore 414

    private const int CUR_VERSION = (int)BaseExtendedClipTrackVersion.SerializeClipOrderAndOperateOnAssetHash_0_16_3;

    enum BaseExtendedClipTrackVersion : int {
        SerializedAssetHash_0_16_2 = 1,
        SerializeClipOrderAndOperateOnAssetHash_0_16_3 = 2, //Use clip order again.
    }

    private bool m_isClipDataDictionaryInitialized = false;

}

} //end namespace


