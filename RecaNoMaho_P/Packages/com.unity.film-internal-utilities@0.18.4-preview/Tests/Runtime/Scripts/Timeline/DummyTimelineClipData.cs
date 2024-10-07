using System;

namespace Unity.FilmInternalUtilities.Tests {

[Serializable]
internal class DummyTimelineClipData : BaseClipData {
    
    #region ISerializationCallbackReceiver
    protected override void OnBeforeSerializeInternalV() {
    }

    protected override void OnAfterDeserializeInternalV() {
    }    
    #endregion

//----------------------------------------------------------------------------------------------------------------------    
   
    internal override void DestroyV() {


    }    
}

} //end namespace