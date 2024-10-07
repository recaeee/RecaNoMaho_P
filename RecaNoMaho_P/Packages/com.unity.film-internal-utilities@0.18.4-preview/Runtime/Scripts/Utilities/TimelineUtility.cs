using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Unity.FilmInternalUtilities {


internal static class TimelineUtility {

//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    internal static int CalculateNumFrames(TimelineClip clip) {
        double fps       = clip.GetParentTrack().timelineAsset.editorSettings.GetFPS();
        int   numFrames = Mathf.RoundToInt((float)(clip.duration * fps));
        return numFrames;
            
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    internal static double CalculateTimePerFrame(TimelineClip clip) {
        return CalculateTimePerFrame(clip.GetParentTrack());
    }

    internal static double CalculateTimePerFrame(TrackAsset trackAsset) {
        double fps = trackAsset.timelineAsset.editorSettings.GetFPS();
        double timePerFrame = 1.0f / fps;
        return timePerFrame;
    }

//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    internal static int TimeToFrame(double time, TimelineAsset timelineAsset) {
        double fps = timelineAsset.editorSettings.GetFPS();
        return Mathf.RoundToInt((float)(time * fps));
    }
    
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------

    internal static Dictionary<TimelineClip, T> ConvertClipsToClipAssetsDictionary<T>(System.Collections.Generic.IEnumerable<TimelineClip> clips) 
        where T: class, IPlayableAsset
    {
        Dictionary<TimelineClip, T> clipAssets = new Dictionary<TimelineClip, T>();
        foreach (TimelineClip clip in clips) {
            T clipAsset = clip.asset as T;
            Assert.IsNotNull(clipAsset);
            clipAssets.Add(clip, clipAsset);
        }

        return clipAssets;
    }
    
//----------------------------------------------------------------------------------------------------------------------
    //Only returns one clip if found. Does not support blended clips.
    internal static void GetActiveTimelineClipInto<T>( IEnumerable<TimelineClip> sortedClips, double directorTime, 
        out TimelineClip outClip, out T outAsset) where T: PlayableAsset 
    {

        TimelineClip prevClipWithPostExtrapolation = null;
        TimelineClip nextClipWithPreExtrapolation  = null;
        bool         nextClipChecked               = false; 
               
        foreach (TimelineClip clip in sortedClips) {


            if (directorTime < clip.start) {
                //must check only once since we loop from the start
                if (!nextClipChecked) { 
                    //store next direct clip which has PreExtrapolation
                    nextClipWithPreExtrapolation = clip.hasPreExtrapolation ? clip : null;
                    nextClipChecked              = true;
                }

                continue;
            }

            if (clip.end <= directorTime) {
                //store prev direct clip which has PostExtrapolation
                prevClipWithPostExtrapolation = clip.hasPostExtrapolation ? clip : null;
                continue;                
            }

            outClip  = clip;
            outAsset = clip.asset as T;
            return;
        }
        
        
        //check for post-extrapolation
        if (null != prevClipWithPostExtrapolation) {
            outClip  = prevClipWithPostExtrapolation;
            outAsset = prevClipWithPostExtrapolation.asset as T;
            return;
        }

        //check pre-extrapolation for the first clip
        if (null!=nextClipWithPreExtrapolation) {
            outClip  = nextClipWithPreExtrapolation;
            outAsset = nextClipWithPreExtrapolation.asset as T;
            return;
        }        
        outClip  = null;
        outAsset = null;
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------
    
    internal static void DeleteInvalidMarkers<MarkerType>(TrackAsset track) where MarkerType : Marker, ICanRefresh {
        List<Marker> markersToDelete = new List<Marker>();
        foreach (IMarker m in track.GetMarkers()) {
            MarkerType marker = m as MarkerType;
            if (null == marker)
                continue;

            if (!marker.Refresh()) markersToDelete.Add(marker);
        }

        foreach (Marker marker in markersToDelete) track.DeleteMarker(marker);
    }

}

} //end namespace


