using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class EditorReflectionTests {

    [Test]
    public void VerifyEditorReflection() {
        Assert.IsNotNull(UnityEditorReflection.SCROLLABLE_TEXT_AREA_METHOD);
    }
    
    [Test]
    public void VerifyTimelineReflection() {
        Assert.IsTrue(TimelineEditorReflection.IsInitialized());
        Assert.IsNotNull(TimelineEditorReflection.TIMELINE_EDITOR_CLIP_TYPE);
        Assert.IsNotNull(TimelineEditorReflection.TIMELINE_EDITOR_CLIP_PROPERTY);
    }
    
    [Test]
    public void VerifyWindowLayoutReflection() {
        Assert.IsNotNull(LayoutUtility.LOAD_WINDOW_LAYOUT_METHOD);
        Assert.IsNotNull(LayoutUtility.SAVE_WINDOW_LAYOUT_METHOD);
    }
    

}

    

} //end namespace