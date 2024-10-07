using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.FilmInternalUtilities {

internal static class EnumUtility {

    internal static List<T> ToValueList<T>() {
        return new List<T>((T[])Enum.GetValues(typeof(T)));
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------    
    internal static List<string> ToInspectorNames(Type t) {
        List<string> ret = new List<string>();
        foreach (MemberInfo mi in t.GetMembers( BindingFlags.Static | BindingFlags.Public)) {
            InspectorNameAttribute inspectorNameAttribute = (InspectorNameAttribute) Attribute.GetCustomAttribute(mi, typeof(InspectorNameAttribute));
            if (null == inspectorNameAttribute) {
                ret.Add(mi.Name);
                continue;
            }
            
            ret.Add(inspectorNameAttribute.displayName);			
        }

        return ret;
    }
    
//--------------------------------------------------------------------------------------------------------------------------------------------------------------    
    
    internal static Dictionary<int, string> ToInspectorNameDictionary(Type t) {
        Dictionary<int, string> ret = new Dictionary<int, string>();
        foreach (FieldInfo fi in t.GetFields( BindingFlags.Static | BindingFlags.Public)) {
            InspectorNameAttribute inspectorNameAttribute = (InspectorNameAttribute) Attribute.GetCustomAttribute(fi, typeof(InspectorNameAttribute));
            
            object valueObj  = fi.GetValue(null); 
            int    enumValue = (int) valueObj; 
            
            if (null == inspectorNameAttribute) {
                ret.Add(enumValue, fi.Name);
                continue;
            }
            ret.Add(enumValue, inspectorNameAttribute.displayName);
        }

        return ret;
    }    
}

} //end namespace
