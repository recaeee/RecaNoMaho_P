using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

internal class NativeArrayTests : CollectionsTestFixture
{
    [Test]
    public unsafe void NativeArray_IndexOf_Int()
    {
        var container = new NativeArray<int>(10, Allocator.Persistent);
        container[0] = 123;
        container[1] = 789;

        bool r0 = false, r1 = false, r2 = false;

        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            r0 = -1 != container.IndexOf(456);
            r1 = container.Contains(123);
            r2 = container.Contains(789);
        });

        Assert.False(r0);
        Assert.False(-1 != NativeArrayExtensions.IndexOf<int, int>(container.GetUnsafePtr(), container.Length, 456));

        Assert.True(r1);
        Assert.True(NativeArrayExtensions.Contains<int, int>(container.GetUnsafePtr(), container.Length, 123));

        Assert.True(r2);
        Assert.True(NativeArrayExtensions.Contains<int, int>(container.GetUnsafePtr(), container.Length, 789));

        container.Dispose();
    }

    [Test]
    public unsafe void NativeArray_IndexOf_FixedString128()
    {
        var container = new NativeArray<FixedString128Bytes>(10, Allocator.Persistent);
        container[0] = new FixedString128Bytes("123");
        container[1] = new FixedString128Bytes("789");

        bool r0 = false, r1 = false, r2 = false;

        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            r0 = -1 != container.IndexOf(new FixedString128Bytes("456"));
            r1 = container.Contains(new FixedString128Bytes("123"));
            r2 = container.Contains(new FixedString128Bytes("789"));
        });

        Assert.False(r0);
        Assert.False(-1 != NativeArrayExtensions.IndexOf<FixedString128Bytes, FixedString128Bytes>(container.GetUnsafePtr(), container.Length, new FixedString128Bytes("456")));

        Assert.True(r1);
        Assert.True(NativeArrayExtensions.Contains<FixedString128Bytes, FixedString128Bytes>(container.GetUnsafePtr(), container.Length, new FixedString128Bytes("123")));

        Assert.True(r2);
        Assert.True(NativeArrayExtensions.Contains<FixedString128Bytes, FixedString128Bytes>(container.GetUnsafePtr(), container.Length, new FixedString128Bytes("789")));

        container.Dispose();
    }

    [Test]
    public void NativeArray_DisposeJob()
    {
        var container = new NativeArray<int>(1, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.DoesNotThrow(() => { container[0] = 1; });

        var disposeJob = container.Dispose(default);
        Assert.False(container.IsCreated);
        Assert.Throws<ObjectDisposedException>(
            () => { container[0] = 2; });

        disposeJob.Complete();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct NativeArrayPokeJob : IJob
    {
        NativeArray<int> array;

        public NativeArrayPokeJob(NativeArray<int> array) { this.array = array; }

        public void Execute()
        {
            array[0] = 1;
        }
    }

    [Test]
    public void NativeArray_DisposeJobWithMissingDependencyThrows()
    {
        var array = new NativeArray<int>(1, Allocator.Persistent);
        var deps = new NativeArrayPokeJob(array).Schedule();
        Assert.Throws<InvalidOperationException>(() => { array.Dispose(default); });
        deps.Complete();
        array.Dispose();
    }

    [Test]
    public void NativeArray_DisposeJobCantBeScheduled()
    {
        var array = new NativeArray<int>(1, Allocator.Persistent);
        var deps = array.Dispose(default);
        Assert.Throws<InvalidOperationException>(() => { new NativeArrayPokeJob(array).Schedule(deps); });
        deps.Complete();
    }
}
