using JetBrains.Annotations;
using UnityEngine;

namespace Unity.FilmInternalUtilities
{

internal abstract class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour {

    [NotNull]
    public static T GetOrCreateInstance() {
        lock (m_lock) {
            
            if (null!=m_instance)
                return m_instance;
            
            //Can only be called from the main thread
            T[] instances = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            int count     = instances.Length;

            switch (count) {
                case 0: {
                    return m_instance = new GameObject().AddComponent<T>();
                }
                case 1: {
                    return m_instance = instances[0];                    
                }
                default: {
                    Debug.LogWarning($"[FIU] There should never be more than one singleton " +
                        $"of type {typeof(T)} in the scene, but {count} were found. " +
                        $"The first instance found will be used, and all others will be destroyed.");
                    
                    for (int i = 1; i < instances.Length; ++i)
                        Object.Destroy(instances[i]);
                    return m_instance = instances[0];
                    
                }
            }

        }
    }

//----------------------------------------------------------------------------------------------------------------------
    
    //Don't use new modifier to "override" this method. Override AwakeInternalV() instead
    protected void Awake() {

        lock (m_lock) {

            if (null != m_instance) {
                if (this != m_instance) {
                    Debug.LogWarning($"[FIU] Duplicate singleton of type {typeof(T)} is detected. Destroying.");
                    FilmInternalUtilities.ObjectUtility.Destroy(this.gameObject);
                    return;
                }

            }

            m_instance = this as T;
        }

        if (Application.isPlaying && m_persistent)
            DontDestroyOnLoad(gameObject);
        
        AwakeInternalV();
    }

    protected virtual void AwakeInternalV() { }

//----------------------------------------------------------------------------------------------------------------------    

    protected void Reset() {

        name = GetDefaultNameV();
    }

    protected virtual string GetDefaultNameV() => $"Singleton {typeof(T)}";        
    
//----------------------------------------------------------------------------------------------------------------------    
    //Static vars   
    [CanBeNull] private static          T      m_instance;
    [NotNull]   private static readonly object m_lock       = new object();    
        
//----------------------------------------------------------------------------------------------------------------------
   

    [SerializeField] private bool m_persistent = true;
    
}

} //end namespace
