using UnityEngine;

namespace Unity.FilmInternalUtilities {

internal interface IAnimationCurveOwner {
    void SetAnimationCurve(AnimationCurve curve);
    AnimationCurve  GetAnimationCurve();
}

} //end namespace
