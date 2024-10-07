using JetBrains.Annotations;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Tests;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace Unity.FilmInternalUtilities.EditorTests {

[CustomTimelineEditor(typeof(DummyTimelinePlayableAsset)), UsedImplicitly]
internal class DummyTimelinePlayableAssetEditor : ClipEditor

{
    
//----------------------------------------------------------------------------------------------------------------------    
    /// <inheritdoc/>
    public sealed override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom) {
        DummyTimelinePlayableAsset playableAsset = clip.asset as DummyTimelinePlayableAsset;
        Assert.IsNotNull(playableAsset);
        playableAsset.Init();
    }

//----------------------------------------------------------------------------------------------------------------------    

}
} //end namespace