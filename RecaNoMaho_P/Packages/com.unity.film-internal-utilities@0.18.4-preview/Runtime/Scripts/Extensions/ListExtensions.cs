using System.Collections.Generic;

namespace Unity.FilmInternalUtilities {

internal static class ListExtensions {
    
    internal static void RemoveNullMembers<T>(this IList<T> list) {
        for (int i = list.Count-1; i >= 0 ; --i) {
            if (null != list[i])
                continue;
            
            list.RemoveAt(i);
        }
    }
    
    internal static void Move<T>(this List<T> list, int oldIndex, int newIndex) {
        if (oldIndex == newIndex)
            return;
            
        T item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, item);
    }

    internal static bool AreElementsEqual<T>(this List<T> list, IList<T> otherList) {
        if (null == otherList || list.Count != otherList.Count)
            return false;

        int numElements = list.Count;
        for (int i = 0; i < numElements; ++i) {
            if (!list[i].Equals(otherList[i]))
                return false;
        }        

        return true;

    }
}

} //end namespace

