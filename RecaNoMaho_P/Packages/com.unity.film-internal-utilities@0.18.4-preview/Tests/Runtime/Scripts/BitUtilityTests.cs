using System;
using NUnit.Framework;

namespace Unity.FilmInternalUtilities.Tests {
internal class BitUtilityTests {
        
    [Test]
    public void VerifyBitSet() {
        AssertBitSet(0b10001, 0b0000001, true);
        AssertBitSet(0b10001, 0b0010000, true);
        AssertBitSet(0b10010, 0b0000001, false);
        AssertBitSet(0b10010, 0b1000000, false);
    }

    private void AssertBitSet(int a, int b, bool expectedResult) {
        Assert.AreEqual(expectedResult, BitUtility.IsBitSet(a, b), 
            $"{Convert.ToString(a,2)} & {Convert.ToString(b,2)} =={Convert.ToString(b,2)} does not equal to {expectedResult}");
        
    }

}

} //end namespace
