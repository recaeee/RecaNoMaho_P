using System;
using UnityEngine;

namespace Unity.FilmInternalUtilities.EditorTests {

[Serializable]
[Json("Assets/TestDummyJsonSingleton.json")]
internal class DummyEditorJsonSingleton : BaseJsonSingleton<DummyEditorJsonSingleton> {

    public DummyEditorJsonSingleton() : base() { }

    protected override int GetLatestVersionV() {
        return 3;
    }

    protected override void UpgradeToLatestVersionV(int prevVersion, int curVersion) {
        Debug.Log($"Upgrading DummyJsonSingleton from {prevVersion} to {curVersion}");
    }

    protected override void OnAfterDeserializeInternalV() {
        m_isDeserialized = true;
    }
//----------------------------------------------------------------------------------------------------------------------

    internal void SetValue(int v) {
        m_basicValue = v;
    }

    internal int GetValue() => m_basicValue;

    internal bool IsDeserialized() => m_isDeserialized;

//----------------------------------------------------------------------------------------------------------------------
    private bool   m_isDeserialized = false;


    [SerializeField] private int m_basicValue = 1;

//----------------------------------------------------------------------------------------------------------------------


}

} //end namespace
