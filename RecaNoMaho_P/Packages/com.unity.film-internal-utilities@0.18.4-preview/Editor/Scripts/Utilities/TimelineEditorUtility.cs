using System;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Unity.FilmInternalUtilities.Editor {

/// <summary>
/// A utility class for executing operations related to Timeline assets in the editor.
/// </summary>
internal static class TimelineEditorUtility {

    /// <summary>
    /// Create a TimelineAsset, which can be assigned to a PlayableDirector.
    /// </summary>
    /// <param name="timelineAssetPath"></param>
    /// <returns>The newly created TimelineAsset</returns>
    internal static TimelineAsset CreateAsset(string timelineAssetPath) 
    {
        TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timelineAsset, timelineAssetPath);
        
        return timelineAsset;
    }
    
    /// <summary>
    /// Create a Track and TimelineClip in a TimelineAsset.
    /// </summary>
    /// <param name="timelineAsset">The TimelineAsset in which the track and clip will be created</param>
    /// <param name="trackName">The track Name</param>
    /// <typeparam name="TrackType">The type of the Track</typeparam>
    /// <typeparam name="ClipAssetType">The type of the TimelineClip's asset</typeparam>
    /// <returns>The newly created TimelineClip</returns>
    internal static TimelineClip CreateTrackAndClip<TrackType, ClipAssetType>(TimelineAsset timelineAsset, string trackName) 
        where TrackType: TrackAsset, new() 
        where ClipAssetType : class 
    {
        return CreateTrackAndClip(timelineAsset, trackName, typeof(TrackType), typeof(ClipAssetType));
    }
    

    /// <summary>
    /// Create a Track and TimelineClip in a TimelineAsset.
    /// </summary>
    /// <param name="timelineAsset">The TimelineAsset in which the track and clip will be created</param>
    /// <param name="trackName">The track Name</param>
    /// <param name="trackType">The type of the Track</param>
    /// <param name="clipAssetType">The type of the TimelineClip's asset</param>
    /// <returns>The newly created TimelineClip</returns>
    internal static TimelineClip CreateTrackAndClip(TimelineAsset timelineAsset, string trackName, 
        Type trackType, Type clipAssetType) 
    {
        TrackAsset track = timelineAsset.CreateTrack(trackType, null, trackName);
        TimelineClip clip = TimelineEditorReflection.CreateClipOnTrack(clipAssetType, track, 0);
        return clip;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    
    /// <summary>
    /// Destroy Timeline assets related to the passed TimelineClip
    /// </summary>
    /// <param name="clip">The clip which assets will be destroyed</param>
    internal static void DestroyAssets(TimelineClip clip) {
        TrackAsset    movieTrack    = clip.GetParentTrack();
        TimelineAsset timelineAsset = movieTrack.timelineAsset;        
        timelineAsset.DeleteTrack(movieTrack);
        DestroyAssets(timelineAsset);
    }
    
    internal static void DestroyAssets(PlayableAsset playableAsset) {
            
        string assetPath = AssetDatabase.GetAssetPath(playableAsset);
        Assert.IsFalse(string.IsNullOrEmpty(assetPath));

        Object.DestroyImmediate(playableAsset, true);
        AssetDatabase.DeleteAsset(assetPath);            
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    internal static void ShowTimelineWindow() {
#if !AT_USE_TIMELINE_GE_1_5_0            
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");        
#else 
        TimelineEditor.GetOrCreateWindow();
#endif
    }

    
    internal static void SelectDirectorInTimelineWindow(PlayableDirector director) {
        //Select gameObject and open Timeline Window. This will trigger the TimelineWindow's update etc.
        ShowTimelineWindow();
        Selection.activeObject = director;
    }
    
    internal static void RefreshTimelineEditor(PlayableDirector director, RefreshReason refreshReason = DEFAULT_REFRESH_REASON) {
        ShowTimelineWindow();
        Selection.activeObject = director;
        TimelineEditor.Refresh(refreshReason);
    }


    internal static void RefreshTimelineEditor(RefreshReason reason = DEFAULT_REFRESH_REASON) {
        TimelineEditor.Refresh(reason);
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    //Returns an object with EditorClip type. This object should be manually destroyed
    internal static ScriptableObject SelectTimelineClipInInspector(TimelineClip clip) {
        ScriptableObject editorClip = ScriptableObject.CreateInstance(TimelineEditorReflection.TIMELINE_EDITOR_CLIP_TYPE);
        TimelineEditorReflection.TIMELINE_EDITOR_CLIP_PROPERTY.SetValue(editorClip, clip);
        Selection.activeObject = editorClip;
        return editorClip;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    //TimelineEditor.Refresh() has been optimized in 1.5.0 above, so WindowNeedsRedraw is enough by default. 
    const RefreshReason DEFAULT_REFRESH_REASON = 
#if AT_USE_TIMELINE_GE_1_5_0
        RefreshReason.WindowNeedsRedraw;
#else
        RefreshReason.ContentsAddedOrRemoved;
#endif //AT_USE_TIMELINE_GE_1_5_0
    

}

} //end namespace