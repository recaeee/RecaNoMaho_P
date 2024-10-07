using UnityEngine;


namespace Unity.FilmInternalUtilities
{
internal static class AnimationCurveExtensions {

    internal static void AddOrUpdateKey(this AnimationCurve curve, float time, float val) {

        int numKeys = curve.keys.Length;
        for (int i = 0; i < numKeys; ++i) { 
            if (curve.keys[i].time == time) {
                curve.RemoveKey(i);
                break; 
            } 
        }
        curve.AddKey(time, val);
    }    
    
}

} //end namespace