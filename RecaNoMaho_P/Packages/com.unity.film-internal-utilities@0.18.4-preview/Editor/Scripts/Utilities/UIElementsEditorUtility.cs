using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;


namespace Unity.FilmInternalUtilities.Editor {

/// <summary>
/// A utility class for executing operations related to UIElements.
/// </summary>
internal class UIElementsEditorUtility {
    
    /// <summary>
    /// Load a UXML file
    /// </summary>
    /// <param name="pathWithoutExt">the path to the UXML file without the extension</param>
    /// <param name="ext">The extension of the UXML file. Assumed to be ".uxml" </param>
    /// <returns></returns>
    public static VisualTreeAsset LoadVisualTreeAsset(string pathWithoutExt, string ext = ".uxml") {
        string path = pathWithoutExt + ext;
        VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        if (null == asset) {
            Debug.LogError("[AnimeToolbox] Can't load VisualTreeAsset: " + path);
            return null;
        }
        return asset;
    }    
    
//----------------------------------------------------------------------------------------------------------------------
    
    /// <summary>
    /// Load UIElement style file and adds it to StyleSheetSet
    /// </summary>
    /// <param name="set">StyleSheetSet to which the new StyleSheet will be added</param>
    /// <param name="pathWithoutExt">Path to the file without the extension</param>
    /// <param name="ext">The extension of the file. Assumed to be ".uss" </param>
    public static void LoadAndAddStyle(VisualElementStyleSheetSet set, string pathWithoutExt, string ext = ".uss") {
        string path = pathWithoutExt + ext;
        StyleSheet asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        if (null == asset) {
            Debug.LogError("[AnimeToolbox] Can't load style: " + path);
            return;
        }
        set.Add(asset);
    }
    
//----------------------------------------------------------------------------------------------------------------------
    internal static PopupField<T> AddPopupField<T>(VisualElement parent, GUIContent content,
    	List<T> options, T defaultValue, Action<ChangeEvent<T>> onValueChanged) 
    {
    	TemplateContainer templateInstance = CloneFieldTemplate();
    	VisualElement     fieldContainer   = templateInstance.Query<VisualElement>("FieldContainer").First();
    	PopupField<T>     popupField       = new PopupField<T>(options,defaultValue);
    	
    	Label label = templateInstance.Query<Label>().First();
    	label.text    = content.text;
    	label.tooltip = content.tooltip;
    	popupField.RegisterValueChangedCallback( ( ChangeEvent<T> changeEvent)  => {
	        onValueChanged?.Invoke(changeEvent);
    	});
    			
    	fieldContainer.Add(popupField);
    	parent.Add(templateInstance);
    	return popupField;
    }

//----------------------------------------------------------------------------------------------------------------------
	
	//Support Toggle, FloatField, etc
	internal static F AddField<F,V>(VisualElement parent, GUIContent content,
		V initialValue, Action<ChangeEvent<V>> onValueChanged) where F: VisualElement,INotifyValueChanged<V>, new()  
	{
		TemplateContainer templateInstance = CloneFieldTemplate();
		VisualElement     fieldContainer   = templateInstance.Query<VisualElement>("FieldContainer").First();
		Label             label            = templateInstance.Query<Label>().First();
		label.text    = content.text;
		label.tooltip = content.tooltip;
		      
		F field = new F();
		field.SetValueWithoutNotify(initialValue);
		field.RegisterValueChangedCallback((ChangeEvent<V> changeEvent) => {
			onValueChanged?.Invoke(changeEvent);
		});
		      
		fieldContainer.Add(field);
		parent.Add(templateInstance);
		return field;
	}	

	//Support Toggle, FloatField, etc
	internal static F AddField<F,V>(VisualElement parent, GUIContent content,
		V initialValue, string className, Action<ChangeEvent<V>> onValueChanged) where F: VisualElement,INotifyValueChanged<V>, new() 
	{
		F field = AddField<F,V>(parent, content, initialValue, onValueChanged);
		field.AddToClassList(className);
		return field;
	}	
	
//----------------------------------------------------------------------------------------------------------------------	
	
	private static TemplateContainer CloneFieldTemplate() {
		if (null == m_fieldTemplate) {
			m_fieldTemplate = UIElementsEditorUtility.LoadVisualTreeAsset(FIELD_TEMPLATE_PATH);			
		}
		Assert.IsNotNull(m_fieldTemplate);
		return m_fieldTemplate.CloneTree();
	}
	
//----------------------------------------------------------------------------------------------------------------------	

	private static readonly string FIELD_TEMPLATE_PATH = Path.Combine(FilmInternalUtilitiesEditorConstants.PACKAGE_PATH,"Editor/UIElements/FieldTemplate");
	private static VisualTreeAsset m_fieldTemplate = null;
    
}


} //namespace Unity.AnimeToolbox