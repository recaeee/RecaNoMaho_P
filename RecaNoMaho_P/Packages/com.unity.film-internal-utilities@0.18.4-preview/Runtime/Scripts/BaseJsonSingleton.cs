using System;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;


namespace Unity.FilmInternalUtilities {

/// <summary>
/// Default settings path: "[type name].json". Can be overridden with Json attribute
/// To load in runtime, put the settings under Resources folder, ex: [Json("Assets/Resources/MySettings/foo.json")]
/// </summary>
/// <typeparam name="T"></typeparam>
[Serializable]
internal abstract class BaseJsonSingleton<T> : ISerializationCallbackReceiver 
    where T: class, ISerializationCallbackReceiver, new()  
{

    internal static T GetOrCreateInstance() {

        lock (m_lock) {
            if (null != m_instance) {
                return m_instance;
            }
        }

        //Get path
        JsonAttribute attr = (JsonAttribute) Attribute.GetCustomAttribute(typeof(T), typeof (JsonAttribute));
        m_jsonPath = (null == attr) ? typeof(T) + ".json" : attr.GetPath();
        
        lock (m_lock) {
            
#if UNITY_EDITOR
            m_instance = DeserializeFromFile(m_jsonPath);
#else 
            string resPath = AssetUtility.ToResourcesRelativePath(m_jsonPath);
            if (!string.IsNullOrEmpty(resPath)) {
                TextAsset textAsset = Resources.Load<TextAsset>(resPath);
                if (null != textAsset) {
                    m_instance = DeserializeFromText(textAsset.text); 
                }
            }
#endif
            if (null != m_instance) {
                return m_instance;
            }
                
            m_instance = new T();
            SaveInstanceInEditor();

            return m_instance;
        }
    }
    
    private static T DeserializeFromFile(string path) {
        T instance = null;
        if (File.Exists(path)) {
            instance = FileUtility.DeserializeFromJson<T>(path);
        }

        if (null != instance) {
            instance.OnAfterDeserialize();
        }
        return instance;
    }
    
    private static T DeserializeFromText(string jsonText) {
        T instance = JsonUtility.FromJson<T>(jsonText);
        if (null != instance) {
            instance.OnAfterDeserialize();
        }
        return instance;
    }
    
    
    private static void SaveInstanceInEditor() {
#if UNITY_EDITOR
        lock (m_lock) {
            Assert.IsNotNull(m_jsonPath);
            string dir = Path.GetDirectoryName(m_jsonPath);
            if (!string.IsNullOrEmpty(dir)) {
                Directory.CreateDirectory(dir);
            }

            m_instance.OnBeforeSerialize();
            FileUtility.SerializeToJson(m_instance, m_jsonPath);
        }
#endif
        
    }

    internal static void Close() {
        lock (m_lock) {
            if (null != m_instance) {
                SaveInstanceInEditor();
            }
            m_instance = null;
        }
    }
    
//----------------------------------------------------------------------------------------------------------------------

    internal string GetJsonPath() => m_jsonPath;

    protected virtual void OnBeforeSerializeInternalV() { } 
    protected virtual void OnAfterDeserializeInternalV() { }

    protected abstract int GetLatestVersionV();

    protected abstract void UpgradeToLatestVersionV(int prevVersion, int curVersion);
    
//----------------------------------------------------------------------------------------------------------------------
    
    internal void SaveInEditor() {
        SaveInstanceInEditor();
    }   

    public void OnBeforeSerialize() {
        m_version = GetLatestVersionV();
        OnBeforeSerializeInternalV();
    }

    public void OnAfterDeserialize() {
        OnAfterDeserializeInternalV();
        
        int latestVersion = GetLatestVersionV();
        if (m_version == latestVersion) {
            return;
        }

        UpgradeToLatestVersionV(m_version, latestVersion);
        m_version = latestVersion;
    }    
    
//----------------------------------------------------------------------------------------------------------------------

    // ReSharper disable once StaticMemberInGenericType
    private static readonly object m_lock = new object();

    // ReSharper disable once StaticMemberInGenericType
    private static string m_jsonPath = null;
    
    private static T      m_instance     = null;
    
    [SerializeField] private int m_version = 0;
    

}


} //end namespace