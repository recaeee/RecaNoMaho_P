using System.IO;
using NUnit.Framework;
using UnityEditor;


namespace Unity.FilmInternalUtilities.EditorTests {

internal class EditorJsonSingletonTests {

    [TearDown]
    public void TearDown() {
        CloseAndDeleteDummyJson();
    }

//----------------------------------------------------------------------------------------------------------------------    

    [Test]
    public void CreateAndSave() {
        DummyEditorJsonSingleton jsonSingleton = DummyEditorJsonSingleton.GetOrCreateInstance();
        Assert.IsFalse(jsonSingleton.IsDeserialized());
        string jsonPath = jsonSingleton.GetJsonPath();
        Assert.IsTrue(File.Exists(jsonPath));
    }

//----------------------------------------------------------------------------------------------------------------------    
    
    [Test]
    public void CreateAndReload() {
        const int TEST_VALUE = 12345;

        DummyEditorJsonSingleton jsonSingleton = DummyEditorJsonSingleton.GetOrCreateInstance();
        jsonSingleton.SetValue(TEST_VALUE);
        jsonSingleton.SaveInEditor();
        DummyEditorJsonSingleton.Close();
        
        jsonSingleton = DummyEditorJsonSingleton.GetOrCreateInstance();
        Assert.IsTrue(jsonSingleton.IsDeserialized());
        Assert.AreEqual(TEST_VALUE, jsonSingleton.GetValue());
    }

//----------------------------------------------------------------------------------------------------------------------    
    [Test]
    public void DeserializeManually() {
        const int TEST_VALUE = 45678;
        
        DummyEditorJsonSingleton jsonSingleton = DummyEditorJsonSingleton.GetOrCreateInstance();
        Assert.IsFalse(jsonSingleton.IsDeserialized());
        jsonSingleton.SetValue(TEST_VALUE);
        jsonSingleton.SaveInEditor();

        string             jsonPath              = jsonSingleton.GetJsonPath();
        DummyEditorJsonSingleton deserializedSingleton = FileUtility.DeserializeFromJson<DummyEditorJsonSingleton>(jsonPath);
        Assert.NotNull(deserializedSingleton);
        Assert.AreEqual(TEST_VALUE, deserializedSingleton.GetValue());
    }
    
//----------------------------------------------------------------------------------------------------------------------
    
    static void CloseAndDeleteDummyJson() {
        DummyEditorJsonSingleton jsonSingleton = DummyEditorJsonSingleton.GetOrCreateInstance();
        string             path          = jsonSingleton.GetJsonPath();
        DummyEditorJsonSingleton.Close();
        if (!File.Exists(path)) 
            return;
        
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.Refresh();
    }
    
}
        
} //end namespace
