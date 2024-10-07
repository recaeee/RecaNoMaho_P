using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;
using UnityEngine.Playables;
using Unity.FilmInternalUtilities.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class TimelineEditorUtilityTest {

    [UnityTearDown]
    public IEnumerator TearDown() {
        if (File.Exists(TIMELINE_ASSET_PATH)) {
            AssetDatabase.DeleteAsset(TIMELINE_ASSET_PATH);
        }

        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
    }
    
//----------------------------------------------------------------------------------------------------------------------
                
    [Test]
    public void CreateAndDestroyTimelineAssets() {
        PlayableDirector director = CreateDirectorWithTimelineAsset(TIMELINE_ASSET_PATH,out TimelineAsset _);
        string           timelineAssetPath = AssetDatabase.GetAssetPath(director.playableAsset);
        TimelineEditorUtility.DestroyAssets(director.playableAsset);
        Assert.IsFalse(File.Exists(timelineAssetPath));
    }

//----------------------------------------------------------------------------------------------------------------------


    [UnityTest]
    public IEnumerator CreateClip() {
        PlayableDirector director = CreateDirectorWithTimelineAsset(TIMELINE_ASSET_PATH,out TimelineAsset timelineAsset);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
        
        TimelineClip clip = TimelineEditorUtility.CreateTrackAndClip<DummyTimelineTrack, DummyTimelinePlayableAsset>(timelineAsset, "FirstTrack");
        VerifyClip(clip);
        Assert.IsTrue(clip.GetParentTrack().GetClips().Contains<DummyTimelinePlayableAsset>());
        TimelineEditorUtility.SelectDirectorInTimelineWindow(director); //trigger the TimelineWindow's update etc.
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(3);
        
        DummyTimelineClipData clipData = clip.GetClipData<DummyTimelineClipData>();
        Assert.IsNotNull(clipData);
        
        TimelineEditorUtility.DestroyAssets(clip); //Cleanup
    }

//----------------------------------------------------------------------------------------------------------------------
    
    [UnityTest]
    public IEnumerator ShowClipInInspector() {
        CreateDirectorWithTimelineAsset(TIMELINE_ASSET_PATH,out TimelineAsset timelineAsset);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
        
        TrackAsset track = timelineAsset.CreateTrack<DummyTimelineTrack>(null, "FooTrack");        
        TimelineClip clip = TimelineEditorReflection.CreateClipOnTrack(typeof(DummyTimelinePlayableAsset), track, 0);
        VerifyClip(clip);
        
        ScriptableObject editorClip = TimelineEditorUtility.SelectTimelineClipInInspector(clip);
        Assert.IsNotNull(editorClip);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
        
        Object.DestroyImmediate(editorClip);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
                   
        TimelineEditorUtility.DestroyAssets(clip); //Cleanup

    }

//----------------------------------------------------------------------------------------------------------------------
    
    [UnityTest]
    public IEnumerator DetectActiveClip() {
        PlayableDirector director = CreateDirectorWithTimelineAsset(TIMELINE_ASSET_PATH,out TimelineAsset timelineAsset);
        TimelineEditorUtility.SelectDirectorInTimelineWindow(director); //trigger the TimelineWindow's update etc.
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
        
        TrackAsset         track     = timelineAsset.CreateTrack<DummyTimelineTrack>(null, "FooTrack");
        List<TimelineClip> clips     = new List<TimelineClip>();
        const int          NUM_CLIPS = 3;

        double nextClipStart = 0;
        for (int i = 0; i < NUM_CLIPS; ++i) {
            TimelineClip curClip = TimelineEditorReflection.CreateClipOnTrack(typeof(DummyTimelinePlayableAsset), track, 0);
            curClip.asset.name =  $"Clip Asset {i}";
            curClip.start      =  nextClipStart;
            nextClipStart      += curClip.duration;
            clips.Add(curClip);
            
            yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
        }

        //Check that all clips have been created successfully
        List<TimelineClip>    trackClips        = new List<TimelineClip>(track.GetClips());
        HashSet<TimelineClip> trackClipsToCheck = new HashSet<TimelineClip>(trackClips);
        for (int i = 0; i < NUM_CLIPS; ++i) {
            Assert.IsTrue(trackClipsToCheck.Contains(clips[i]));
            trackClipsToCheck.Remove(clips[i]);
        }
        NUnit.Framework.Assert.Zero(trackClipsToCheck.Count);
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(3);
        
        //Ensure the active clip can be detected
        for (int i = 0; i < NUM_CLIPS; ++i) {
            TimelineClip curClip = clips[i];
            double time = curClip.start + curClip.duration * 0.5f;
            TimelineUtility.GetActiveTimelineClipInto(trackClips, time, out TimelineClip detectedClip, out TimelineAsset _);
            Assert.AreEqual(curClip, detectedClip);
        }
       
        yield return YieldEditorUtility.WaitForFramesAndIncrementUndo(1);
                   
        TimelineEditorUtility.DestroyAssets(timelineAsset); //Cleanup

    }
    
//----------------------------------------------------------------------------------------------------------------------


    private static PlayableDirector CreateDirectorWithTimelineAsset(string candidatePath, 
        out TimelineAsset timelineAsset) 
    {
        string           timelineAssetPath = AssetDatabase.GenerateUniqueAssetPath(candidatePath);        
        PlayableDirector director          = new GameObject("Director").AddComponent<PlayableDirector>();
        timelineAsset = TimelineEditorUtility.CreateAsset(timelineAssetPath);
        Assert.IsNotNull(timelineAsset);

        director.playableAsset = timelineAsset; 
        Assert.IsTrue(File.Exists(timelineAssetPath));
        return director;

    }

    private static void VerifyClip(TimelineClip clip) {
        Assert.IsNotNull(clip);
        DummyTimelinePlayableAsset playableAsset = clip.asset as DummyTimelinePlayableAsset;
        Assert.IsNotNull(playableAsset);
        Assert.IsTrue(playableAsset.IsInitialized());
        
    }

//----------------------------------------------------------------------------------------------------------------------

    private const string TIMELINE_ASSET_PATH = "Assets/TimelineEditorUtilityTest.playable";

}
}
