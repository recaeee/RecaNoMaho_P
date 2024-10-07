#if !UNITY_DOTSRUNTIME
using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;
using Unity.Burst;
using Unity.Jobs;

namespace FixedStringTests
{
    internal class UnsafeTextTests
    {
        void AssertAreEqualInTest(string expected, in UnsafeText actual)
        {
            var actualString = actual.ToString();
            Assert.AreEqual(expected, actualString);
        }

        // NOTE: If you call this function from Mono and T is not marshalable - your app (Editor or the player built with Mono scripting backend) could/will crash.
        bool IsMarshalable<T>() where T : struct
        {
            try
            {
                unsafe
                {
                    var size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
                    IntPtr memoryIntPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
                    try
                    {
                        var obj = new T();
                        System.Runtime.InteropServices.Marshal.StructureToPtr(obj, memoryIntPtr, false);
                        System.Runtime.InteropServices.Marshal.DestroyStructure<T>(memoryIntPtr);
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(memoryIntPtr);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("ERROR in IsMarshalable<" + typeof(T).FullName + "> " + e);
                return false;
            }
        }

        [Test]
        public void UnsafeTextIsMarshalable()
        {
            var result = IsMarshalable<UnsafeText>();
            Assert.IsTrue(result);
        }

        [Test]
        public unsafe void UnsafeTextCorrectBinaryHeader()
        {
            var text = new UnsafeText(42, Allocator.Persistent);
            var ptr = text.GetUnsafePtr();

            Assert.AreEqual(0 + 1, text.m_UntypedListData.m_length);
            Assert.AreEqual(Allocator.Persistent, text.m_UntypedListData.Allocator.ToAllocator);
            Assert.IsTrue(ptr == text.m_UntypedListData.Ptr, "ptr != text.m_UntypedListData.Ptr");

            var listOfBytesCast = text.AsUnsafeListOfBytes();

            Assert.AreEqual(0 + 1, listOfBytesCast.Length);
            Assert.AreEqual(Allocator.Persistent, listOfBytesCast.Allocator.ToAllocator);
            Assert.IsTrue(ptr == listOfBytesCast.Ptr, "ptr != listOfBytesCast.Ptr");

            Assert.AreEqual(text.m_UntypedListData.m_capacity, listOfBytesCast.Capacity);

            text.Dispose();
        }

        [Test]
        public void UnsafeTextCorrectLengthAfterClear()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            Assert.True(aa.IsCreated);
            Assert.AreEqual(0, aa.Length, "Length after creation is not 0");
            aa.AssertNullTerminated();

            aa.Junk();

            aa.Clear();
            Assert.AreEqual(0, aa.Length, "Length after clear is not 0");
            aa.AssertNullTerminated();

            aa.Dispose();
        }

        [Test]
        public void UnsafeTextFormatExtension1Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            Assert.True(aa.IsCreated);
            aa.Junk();
            FixedString32Bytes format = "{0}";
            FixedString32Bytes arg0 = "a";
            aa.AppendFormat(format, arg0);
            aa.Append('a');
            aa.AssertNullTerminated();
            AssertAreEqualInTest("aa", aa);
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension2Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            aa.AppendFormat(format, arg0, arg1);
            AssertAreEqualInTest("a b", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension3Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            aa.AppendFormat(format, arg0, arg1, arg2);
            AssertAreEqualInTest("a b c", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension4Params()
        {
            UnsafeText aa = new UnsafeText(512, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3);
            AssertAreEqualInTest("a b c d", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension5Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4);
            AssertAreEqualInTest("a b c d e", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension6Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4} {5}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5);
            AssertAreEqualInTest("a b c d e f", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension7Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString32Bytes format = "{0} {1} {2} {3} {4} {5} {6}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
            AssertAreEqualInTest("a b c d e f g", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension8Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            AssertAreEqualInTest("a b c d e f g h", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension9Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7} {8}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            AssertAreEqualInTest("a b c d e f g h i", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }


        [Test]
        public void UnsafeTextFormatExtension10Params()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            FixedString128Bytes format = "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}";
            FixedString32Bytes arg0 = "a";
            FixedString32Bytes arg1 = "b";
            FixedString32Bytes arg2 = "c";
            FixedString32Bytes arg3 = "d";
            FixedString32Bytes arg4 = "e";
            FixedString32Bytes arg5 = "f";
            FixedString32Bytes arg6 = "g";
            FixedString32Bytes arg7 = "h";
            FixedString32Bytes arg8 = "i";
            FixedString32Bytes arg9 = "j";
            aa.AppendFormat(format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            AssertAreEqualInTest("a b c d e f g h i j", aa);
            aa.AssertNullTerminated();
            aa.Dispose();
        }

        [Test]
        public void UnsafeTextAppendGrows()
        {
            UnsafeText aa = new UnsafeText(1, Allocator.Temp);
            var origCapacity = aa.Capacity;
            for (int i = 0; i < origCapacity; ++i)
                aa.Append('a');
            Assert.AreEqual(origCapacity, aa.Capacity);
            aa.Append('b');
            Assert.GreaterOrEqual(aa.Capacity, origCapacity);
            Assert.AreEqual(new String('a', origCapacity) + "b", aa.ToString());
            aa.Dispose();
        }

        [Test]
        public void UnsafeTextAppendString()
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Append("aa");
            Assert.AreEqual("aa", aa.ToString());
            aa.Append("bb");
            Assert.AreEqual("aabb", aa.ToString());
            aa.Dispose();
        }


        [TestCase("Antidisestablishmentarianism")]
        [TestCase("⁣🌹🌻🌷🌿🌵🌾⁣")]
        public void UnsafeTextCopyFromBytesWorks(String a)
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Junk();
            var utf8 = Encoding.UTF8.GetBytes(a);
            unsafe
            {
                fixed (byte* b = utf8)
                    aa.Append(b, (ushort) utf8.Length);
            }

            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();

            aa.Append("tail");
            Assert.AreEqual(a + "tail", aa.ToString());
            aa.AssertNullTerminated();

            aa.Dispose();
        }

        [TestCase("red")]
        [TestCase("紅色", TestName = "{m}(Chinese-Red)")]
        [TestCase("George Washington")]
        [TestCase("村上春樹", TestName = "{m}(HarukiMurakami)")]
        public void UnsafeTextToStringWorks(String a)
        {
            UnsafeText aa = new UnsafeText(4, Allocator.Temp);
            aa.Append(new FixedString128Bytes(a));
            Assert.AreEqual(a, aa.ToString());
            aa.AssertNullTerminated();
            aa.Dispose();
        }

        [Test]
        public void UnsafeTextIndexOf()
        {
            UnsafeText a = new UnsafeText(16, Allocator.Temp);
            a.Append((FixedString64Bytes) "bookkeeper bookkeeper");
            UnsafeText b = new UnsafeText(8, Allocator.Temp);
            b.Append((FixedString32Bytes) "ookkee");

            Assert.AreEqual(1, a.IndexOf(b));
            Assert.AreEqual(-1, b.IndexOf(a));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void UnsafeTextLastIndexOf()
        {
            UnsafeText a = new UnsafeText(16, Allocator.Temp);
            a.Append((FixedString64Bytes) "bookkeeper bookkeeper");
            UnsafeText b = new UnsafeText(8, Allocator.Temp);
            b.Append((FixedString32Bytes) "ookkee");

            Assert.AreEqual(12, a.LastIndexOf(b));
            Assert.AreEqual(-1, b.LastIndexOf(a));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void UnsafeTextContains()
        {
            UnsafeText a = new UnsafeText(16, Allocator.Temp);
            a.Append((FixedString64Bytes) "bookkeeper bookkeeper");
            UnsafeText b = new UnsafeText(8, Allocator.Temp);
            b.Append((FixedString32Bytes) "ookkee");

            Assert.AreEqual(true, a.Contains(b));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void UnsafeTextComparisons()
        {
            UnsafeText a = new UnsafeText(16, Allocator.Temp);
            a.Append((FixedString64Bytes) "apple");
            UnsafeText b = new UnsafeText(8, Allocator.Temp);
            b.Append((FixedString32Bytes) "banana");

            Assert.AreEqual(false, a.Equals(b));
            Assert.AreEqual(true, !b.Equals(a));
            a.Dispose();
            b.Dispose();
        }

        [Test]
        public void UnsafeText_CustomAllocatorTest()
        {
            AllocatorManager.Initialize();
            var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
            ref var allocator = ref allocatorHelper.Allocator;
            allocator.Initialize();

            using (var container = new UnsafeText(1, allocator.Handle))
            {
            }

            Assert.IsTrue(allocator.WasUsed);
            allocator.Dispose();
            allocatorHelper.Dispose();
            AllocatorManager.Shutdown();
        }

        [BurstCompile]
        struct BurstedCustomAllocatorJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public unsafe CustomAllocatorTests.CountingAllocator* Allocator;

            public void Execute()
            {
                unsafe
                {
                    using (var container = new UnsafeText(1, Allocator->Handle))
                    {
                    }
                }
            }
        }

        [Test]
        public unsafe void UnsafeText_BurstedCustomAllocatorTest()
        {
            AllocatorManager.Initialize();
            var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
            ref var allocator = ref allocatorHelper.Allocator;
            allocator.Initialize();

            var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
            unsafe
            {
                var handle = new BurstedCustomAllocatorJob {Allocator = allocatorPtr}.Schedule();
                handle.Complete();
            }

            Assert.IsTrue(allocator.WasUsed);
            allocator.Dispose();
            allocatorHelper.Dispose();
            AllocatorManager.Shutdown();
        }
    }
}
#endif
