namespace Unity.FilmInternalUtilities {

internal static class BitUtility {
    
    internal static bool IsBitSet(int a, int b) {
        return (a & b) == b;
    }
    
}

} //end namespace
