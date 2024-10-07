using System.Collections.Generic;
using JetBrains.Annotations;


namespace Unity.FilmInternalUtilities {

internal static class EnumerableExtensions {

    //Returns -1 if not found
    internal static int FindIndex<T>(this IEnumerable<T> collection, T elementToFind) {
        int i = 0;
        foreach (T obj in collection) {
            if (obj.Equals(elementToFind)) {
                return i;
            }
            ++i;
        }
        
        return -1;
    }
    
    //Returns false with ret set to default(T) if not found
    internal static bool FindElementAt<T>(this IEnumerable<T> collection, int index, out T ret) {

        int i = 0;
        foreach (T obj in collection) {
            if (i == index) {
                ret = obj;
                return true;
            }
            ++i;
        }

        ret = default(T);
        return false;
    }
}
} //end namespace