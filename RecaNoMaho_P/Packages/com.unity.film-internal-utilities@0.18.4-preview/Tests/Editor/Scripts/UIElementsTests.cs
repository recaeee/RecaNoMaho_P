using System.Collections.Generic;
using NUnit.Framework;
using Unity.FilmInternalUtilities.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.FilmInternalUtilities.EditorTests {
internal class UIElementsTests {
                
    [Test]
    public void AddFields() {
        VisualElement parent = new VisualElement();
        
        GUIContent content = new GUIContent("My field");
        IntegerField intField = UIElementsEditorUtility.AddField<IntegerField, int>(parent, content, 100, null);            
        Assert.IsNotNull(intField);
        PopupField<int> popupField = UIElementsEditorUtility.AddPopupField(parent, content, new List<int> {0,1,2,3,4}, 0,null);
        Assert.IsNotNull(popupField);
    }

//----------------------------------------------------------------------------------------------------------------------

    
}
}