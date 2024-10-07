using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.FilmInternalUtilities {


internal static class ExposedReferenceUtility {

#if UNITY_EDITOR
    
    internal static void SetReferenceValueInEditor<T>(ref ExposedReference<T> exposedRef, 
        IExposedPropertyTable propertyTable, T obj) where T: Object 
    {
        //check if exposedName hasn't been initialized
        if (exposedRef.exposedName.ToString() == ":0") {
            exposedRef.exposedName = GUID.Generate().ToString();
        }
        propertyTable.SetReferenceValue(exposedRef.exposedName, obj);
    }
    
    //Can be used to unlink an exposedReference after duplicating it.
    internal static void RecreateReferenceInEditor<T>(ref ExposedReference<T> exposedRef, 
        IExposedPropertyTable table) where T:Object 
    {
        T obj = exposedRef.Resolve(table);
        exposedRef.exposedName = GUID.Generate().ToString();
        table.SetReferenceValue(exposedRef.exposedName, obj);
        
    }    
#endif    
    
}

} //end namespace


