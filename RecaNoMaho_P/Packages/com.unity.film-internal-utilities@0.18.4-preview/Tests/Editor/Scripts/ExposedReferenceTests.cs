using System.Reflection;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;
using Unity.FilmInternalUtilities.Tests;
using UnityEngine;
using Object = UnityEngine.Object;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.FilmInternalUtilities.EditorTests {

internal class ExposedReferenceTests {
    
    [Test]
    public void HasExposedNameField() {
        FieldInfo fi = typeof(ExposedReference<>).GetField(ExposedReferenceEditorUtility.EXPOSED_NAME_FIELD_NAME);
        Assert.IsNotNull(fi);
    }
    
    [Test]
    public void SetExposedReferenceOfScriptableObject() {

        SimpleExposedPropertyTable propTable = CreatePropertyTableGameObject();
        DummyScriptableObject      obj       = ScriptableObject.CreateInstance<DummyScriptableObject>();

        GameObject fooGameObject = new GameObject("Foo");
        ExposedReferenceUtility.SetReferenceValueInEditor(ref obj.exposedGameObject, propTable, fooGameObject);

        GameObject resolvedGameObject = obj.exposedGameObject.Resolve(propTable);
        Assert.AreEqual(fooGameObject, resolvedGameObject);
        
        Object.DestroyImmediate(obj);
    }


    [Test]
    public void TestDuplicatedExposedReferenceAreNotLinked() {

        SimpleExposedPropertyTable propTable = CreatePropertyTableGameObject();
        DummyScriptableObject      obj0      = ScriptableObject.CreateInstance<DummyScriptableObject>();

        GameObject fooGameObject = new GameObject("Foo");
        ExposedReferenceUtility.SetReferenceValueInEditor(ref obj0.exposedGameObject, propTable, fooGameObject);

        //Duplicate
        DummyScriptableObject obj1 = Object.Instantiate(obj0);
        GameObject barGameObject = new GameObject("Bar");
        ExposedReferenceUtility.RecreateReferenceInEditor(ref obj1.exposedGameObject, propTable);
        Assert.AreEqual(obj0.exposedGameObject.Resolve(propTable), obj1.exposedGameObject.Resolve(propTable));

        //Change value 
        ExposedReferenceUtility.SetReferenceValueInEditor(ref obj1.exposedGameObject, propTable, barGameObject);
        Assert.AreNotEqual(obj0.exposedGameObject.Resolve(propTable), obj1.exposedGameObject.Resolve(propTable));
        
        Object.DestroyImmediate(obj1);
        Object.DestroyImmediate(obj0);
    }
    

    [Test]
    public void TestDuplicatedExposedReferenceAreNotLinkedByFieldInfo() {
        SimpleExposedPropertyTable propTable     = CreatePropertyTableGameObject();
        DummyScriptableObject      obj0          = ScriptableObject.CreateInstance<DummyScriptableObject>();
        GameObject                 fooGameObject = new GameObject("Foo");
        ExposedReferenceUtility.SetReferenceValueInEditor(ref obj0.exposedGameObject, propTable, fooGameObject);

        //Duplicate
        DummyScriptableObject obj1          = Object.Instantiate(obj0);
        GameObject            barGameObject = new GameObject("Bar");
        object                obj1Casted    = obj1;
        FieldInfo fieldInfo = typeof(DummyScriptableObject).GetField("exposedGameObject", REFLECTION_BINDING_FLAGS);
        Assert.IsNotNull(fieldInfo);

        ExposedReferenceEditorUtility.RecreateReference(ref obj1Casted, fieldInfo, propTable);
        Assert.AreEqual(obj0.exposedGameObject.Resolve(propTable), obj1.exposedGameObject.Resolve(propTable));

        //Change value 
        ExposedReferenceUtility.SetReferenceValueInEditor(ref obj1.exposedGameObject, propTable, barGameObject);
        Assert.AreNotEqual(obj0.exposedGameObject.Resolve(propTable), obj1.exposedGameObject.Resolve(propTable));

        Object.DestroyImmediate(obj1);
        Object.DestroyImmediate(obj0);
    }
   

//--------------------------------------------------------------------------------------------------------------------------------------------------------------    

    private SimpleExposedPropertyTable CreatePropertyTableGameObject() {
        return new GameObject("PropertyTable").AddComponent<SimpleExposedPropertyTable>();
    } 
    
    private const BindingFlags REFLECTION_BINDING_FLAGS = BindingFlags.Public
        | BindingFlags.Instance
        | BindingFlags.NonPublic
        | BindingFlags.Static;
    
    
}

} //end namespace

