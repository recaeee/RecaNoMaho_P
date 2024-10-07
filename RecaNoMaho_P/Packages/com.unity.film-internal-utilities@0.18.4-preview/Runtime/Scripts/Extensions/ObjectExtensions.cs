using UnityEngine;

namespace Unity.FilmInternalUtilities {

/// <summary>
/// Extension methods for Object class.
/// </summary>
internal static class ObjectExtensions {

    /// <summary>
    /// Returns if the Object is null by using ReferenceEquals.
    /// Caveat: may have a different result from (Object==null). 
    /// </summary>
    /// <param name="obj">The Object to be compared to null.</param>
    public static bool IsNullRef(this Object obj) {
        return ReferenceEquals(obj, null);
    }

    
}

} //end namespace