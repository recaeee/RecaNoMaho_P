using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using Unity.Collections.Tests;
using System;
using Unity.Burst;

internal class UnsafeHashMapTests : CollectionsTestCommonBase
{
    // Burst error BC1071: Unsupported assert type
    // [BurstCompile(CompileSynchronously = true)]
    public struct UnsafeHashMapAddJob : IJob
    {
        public UnsafeHashMap<int, int>.ParallelWriter Writer;

        public void Execute()
        {
            Assert.True(Writer.TryAdd(123, 1));
        }
    }

    [Test]
    public void UnsafeHashMap_AddJob()
    {
        var container = new UnsafeHashMap<int, int>(32, CommonRwdAllocator.Handle);

        var job = new UnsafeHashMapAddJob()
        {
            Writer = container.AsParallelWriter(),
        };

        job.Schedule().Complete();

        Assert.True(container.ContainsKey(123));

        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            var count = 0;
            foreach (var kv in container)
            {
                int value;
                Assert.True(container.TryGetValue(kv.Key, out value));
                Assert.AreEqual(value, kv.Value);
                Assert.AreEqual(kv.Key * 37, kv.Value);

                seen[kv.Key] = seen[kv.Key] + 1;
                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }
        }
    }

#if !NET_DOTS // Array.Sort is not supported
    [Test]
    public void UnsafeHashSet_ToArray()
    {
        using (var set = new UnsafeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 })
        {
            var array = set.ToArray();
            Array.Sort(array);
            for (int i = 0, num = set.Count(); i < num; i++)
            {
                Assert.AreEqual(array[i], i);
            }
        }
    }
#endif

    [Test]
    public void UnsafeHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeHashMap<int, int>(1, allocator.Handle))
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
                using (var container = new UnsafeHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeHashMap_BurstedCustomAllocatorTest()
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
