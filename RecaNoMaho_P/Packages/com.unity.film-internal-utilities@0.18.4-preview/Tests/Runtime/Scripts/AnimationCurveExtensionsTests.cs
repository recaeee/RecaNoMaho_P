using NUnit.Framework;
using UnityEngine;


namespace Unity.FilmInternalUtilities.Tests {

internal class AnimationCurveExtensionsTests {

    [Test]
    public void EvaluateMiddle() {
        AnimationCurve curve = CreateTestCurve();
        float half = START_VALUE + END_VALUE;
        half = half * 0.5f;        
        Assert.AreEqual(half, curve.Evaluate(CURVE_DURATION*0.5f));
    }
    
//----------------------------------------------------------------------------------------------------------------------    
    
    [Test]
    public void AddKey() {
        AnimationCurve curve = CreateTestCurve();

        AddKeyAndVerify(curve, CURVE_DURATION * 0.5f, 100.0f);
        Assert.AreEqual(START_VALUE, curve.Evaluate(0));
        Assert.AreEqual(END_VALUE, curve.Evaluate(CURVE_DURATION));
    }


//----------------------------------------------------------------------------------------------------------------------    
    
    [Test]
    public void UpdateKey() {

        AnimationCurve curve = CreateTestCurve();

        float halfTime = CURVE_DURATION * 0.5f;
        AddKeyAndVerify(curve, halfTime, 10.0f);
        AddKeyAndVerify(curve, halfTime, 100.0f);
        AddKeyAndVerify(curve, halfTime, 1000.0f);
        
        Assert.AreEqual(START_VALUE, curve.Evaluate(0));
        Assert.AreEqual(END_VALUE, curve.Evaluate(CURVE_DURATION));
  }

//----------------------------------------------------------------------------------------------------------------------

    static void AddKeyAndVerify(AnimationCurve curve, float time, float val) {
        curve.AddOrUpdateKey(time, val);
        Assert.AreEqual(val, curve.Evaluate(time));
    }
    
    static AnimationCurve CreateTestCurve() => AnimationCurve.Linear(0, START_VALUE, CURVE_DURATION, END_VALUE);        
    
//----------------------------------------------------------------------------------------------------------------------    
    
    //Constants
    private const float CURVE_DURATION = 10.0f;
    private const float START_VALUE    = 0.0f;
    private const float END_VALUE      = 1.0f;

}
 
} //end namespace
