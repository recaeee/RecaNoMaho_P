using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
#if !UNITY_PORTABLE_TEST_RUNNER
using System.Text.RegularExpressions;
#endif
using Assert = FastAssert;

internal class NativeQueueTests : CollectionsTestCommonBase
{
    static void ExpectedCount<T>(ref NativeQueue<T> container, int expected) where T : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty());
        Assert.AreEqual(expected, container.Count);
    }

    [Test]
    public void Enqueue_Dequeue()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void ConcurrentEnqueue_Dequeue()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        var cQueue = queue.AsParallelWriter();
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        for (int i = 0; i < 16; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void Enqueue_Dequeue_Peek()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
        {
            Assert.AreEqual(i, queue.Peek(), "Got the wrong value from the queue");
            queue.Dequeue();
        }
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void Enqueue_Dequeue_Clear()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 8; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 8);
        queue.Clear();
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void Double_Deallocate_Throws()
    {
        var queue = new NativeQueue<int>(CommonRwdAllocator.Handle);
        queue.Dispose();
        Assert.Throws<ObjectDisposedException>(
            () => { queue.Dispose(); });
    }

    [Test]
    public void EnqueueScalability()
    {
        var queue = new NativeQueue<int>(Allocator.Persistent);
        for (int i = 0; i != 1000 * 100; i++)
        {
            queue.Enqueue(i);
        }

        ExpectedCount(ref queue, 1000 * 100);

        for (int i = 0; i != 1000 * 100; i++)
            Assert.AreEqual(i, queue.Dequeue());
        ExpectedCount(ref queue, 0);

        queue.Dispose();
    }

    [Test]
    public void Enqueue_Wrap()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);

        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });

        for (int i = 0; i < 256; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 128; i < 256; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void ConcurrentEnqueue_Wrap()
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        var cQueue = queue.AsParallelWriter();
        ExpectedCount(ref queue, 0);
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });

        for (int i = 0; i < 256; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 128; i < 256; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
        queue.Dispose();
    }

    [Test]
    public void NativeQueue_DisposeJob()
    {
        var container = new NativeQueue<int>(Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.DoesNotThrow(() => { container.Enqueue(0); });

        var disposeJob = container.Dispose(default);
        Assert.False(container.IsCreated);
        Assert.Throws<ObjectDisposedException>(
            () => { container.Enqueue(0); });

        disposeJob.Complete();
    }

    [Test]
    public void TryDequeue_OnEmptyQueueWhichHadElements_RetainsValidState()
    {
        using (var queue = new NativeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 3; i++)
            {
                queue.Enqueue(i);
                Assert.AreEqual(1, queue.Count);

                int value;
                while (queue.TryDequeue(out value))
                {
                    Assert.AreEqual(i, value);
                }

                Assert.AreEqual(0, queue.Count);
            }
        }
    }

    [Test]
    public void TryDequeue_OnEmptyQueue_RetainsValidState()
    {
        using (var queue = new NativeQueue<int>(Allocator.Temp))
        {
            Assert.IsFalse(queue.TryDequeue(out _));
            queue.Enqueue(1);
            Assert.AreEqual(1, queue.Count);
        }
    }

    [Test]
    public void ToArray_ContainsCorrectElements()
    {
        using (var queue = new NativeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 100; i++)
                queue.Enqueue(i);
            using (var array = queue.ToArray(Allocator.Temp))
            {
                Assert.AreEqual(queue.Count, array.Length);
                for (int i = 0; i < array.Length; i++)
                    Assert.AreEqual(i, array[i]);
            }
        }
    }

    [Test]
    public void ToArray_RespectsDequeue()
    {
        using (var queue = new NativeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 100; i++)
                queue.Enqueue(i);
            for (int i = 0; i < 50; i++)
                queue.Dequeue();
            using (var array = queue.ToArray(Allocator.Temp))
            {
                Assert.AreEqual(queue.Count, array.Length);
                for (int i = 0; i < array.Length; i++)
                    Assert.AreEqual(50 + i, array[i]);
            }
        }
    }

    // These tests require:
    // - JobsDebugger support for static safety IDs (added in 2020.1)
    // - Asserting throws
#if !UNITY_DOTSRUNTIME
    [Test,DotsRuntimeIgnore]
    public void NativeQueue_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var container = new NativeQueue<int>(CommonRwdAllocator.Handle);
        container.Enqueue(123);
        container.Dispose();
        NUnit.Framework.Assert.That(() => container.Dequeue(),
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {container.GetType()} has been deallocated"));
    }
#endif

    [Test]
    public void NativeQueue_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeQueue<int>(allocator.Handle))
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
                using (var container = new NativeQueue<int>(Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeQueue_BurstedCustomAllocatorTest()
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
