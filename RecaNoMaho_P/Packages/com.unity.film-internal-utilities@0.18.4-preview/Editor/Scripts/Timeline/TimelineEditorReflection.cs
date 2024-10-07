using System.Reflection;
using UnityEditor;
using System;
using UnityEditor.Timeline;
using UnityEngine.Assertions;
using UnityEngine.Timeline;

namespace Unity.FilmInternalUtilities.Editor {

internal static class TimelineEditorReflection {
    
    [InitializeOnLoadMethod]
    static void TimelineEditorReflection_OnEditorLoad() {
        
        Assert.IsNotNull(TIMELINE_HELPERS_TYPE);        
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        m_createClipOnTrackMethod = TIMELINE_HELPERS_TYPE.GetMethod("CreateClipOnTrack", bindingFlags, null,
            new Type[] { typeof(Type), typeof(TrackAsset), typeof(double) }, null);
        
        Assert.IsNotNull(m_createClipOnTrackMethod);
    }
    
    internal static bool IsInitialized() {
        return null!= m_createClipOnTrackMethod;
    }    
    
    internal static TimelineClip CreateClipOnTrack(Type playableAssetType, TrackAsset trackAsset, double candidateTime) {
        
        //this method requires the TimelineWindow to be open
        EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline"); 
        
        Assert.IsNotNull(m_createClipOnTrackMethod);
        return (TimelineClip) m_createClipOnTrackMethod.Invoke(null, new object[] { playableAssetType, trackAsset, candidateTime} );
    }
    
    
//----------------------------------------------------------------------------------------------------------------------    
    
    static readonly Type TIMELINE_HELPERS_TYPE = typeof(TimelineEditor).Assembly.GetType("UnityEditor.Timeline.TimelineHelpers");
    
    internal static readonly Type TIMELINE_EDITOR_CLIP_TYPE = Type.GetType("UnityEditor.Timeline.EditorClip, Unity.Timeline.Editor");
    internal static readonly PropertyInfo TIMELINE_EDITOR_CLIP_PROPERTY = TIMELINE_EDITOR_CLIP_TYPE.GetProperty("clip"); 
    
    
    private static MethodInfo m_createClipOnTrackMethod = null;

}

} //end namespace

