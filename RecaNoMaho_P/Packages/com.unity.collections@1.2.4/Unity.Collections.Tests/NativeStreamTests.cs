using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using Assert = FastAssert;

internal class NativeStreamTests : CollectionsTestFixture
{
    [BurstCompile(CompileSynchronously = true)]
    struct WriteInts : IJobParallelFor
    {
        public NativeStream.Writer Writer;

        public void Execute(int index)
        {
            Writer.BeginForEachIndex(index);
            for (int i = 0; i != index; i++)
                Writer.Write(i);
            Writer.EndForEachIndex();
        }
    }

    struct ReadInts : IJobParallelFor
    {
        public NativeStream.Reader Reader;

        public void Execute(int index)
        {
            int count = Reader.BeginForEachIndex(index);
            Assert.AreEqual(count, index);

            for (int i = 0; i != index; i++)
            {
                Assert.AreEqual(index - i, Reader.RemainingItemCount);
                var peekedValue = Reader.Peek<int>();
                var value = Reader.Read<int>();
                Assert.AreEqual(i, value);
                Assert.AreEqual(i, peekedValue);
            }

            Reader.EndForEachIndex();
        }
    }

    [Test]
    public void PopulateInts([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        var jobHandle = fillInts.Schedule(count, batchSize);

        var compareInts = new ReadInts {Reader = stream.AsReader()};
        var res0 = compareInts.Schedule(count, batchSize, jobHandle);
        var res1 = compareInts.Schedule(count, batchSize, jobHandle);

        res0.Complete();
        res1.Complete();

        stream.Dispose();
    }

    static void ExpectedCount(ref NativeStream container, int expected)
    {
        Assert.AreEqual(expected == 0, container.IsEmpty());
        Assert.AreEqual(expected, container.Count());
    }

    [Test]
    public void CreateAndDestroy([Values(1, 100, 200)] int count)
    {
        var stream = new NativeStream(count, Allocator.Temp);

        Assert.IsTrue(stream.IsCreated);
        Assert.IsTrue(stream.ForEachCount == count);
        ExpectedCount(ref stream, 0);

        stream.Dispose();
        Assert.IsFalse(stream.IsCreated);
    }

    [Test]
    public void ItemCount([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        fillInts.Schedule(count, batchSize).Complete();

        ExpectedCount(ref stream, count * (count - 1) / 2);

        stream.Dispose();
    }

    [Test]
    public void ToArray([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        fillInts.Schedule(count, batchSize).Complete();
        ExpectedCount(ref stream, count * (count - 1) / 2);

        var array = stream.ToNativeArray<int>(Allocator.Temp);
        int itemIndex = 0;

        for (int i = 0; i != count; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                Assert.AreEqual(j, array[itemIndex]);
                itemIndex++;
            }
        }

        array.Dispose();
        stream.Dispose();
    }

    [Test]
    public void NativeStream_DisposeJob()
    {
        var stream = new NativeStream(100, CommonRwdAllocator.Handle);
        Assert.IsTrue(stream.IsCreated);

        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        var writerJob = fillInts.Schedule(100, 16);

        var disposeJob = stream.Dispose(writerJob);
        Assert.IsFalse(stream.IsCreated);

        disposeJob.Complete();
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    public void ParallelWriteThrows()
    {
        var stream = new NativeStream(100, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};

        var writerJob = fillInts.Schedule(100, 16);
        Assert.Throws<InvalidOperationException>(() => fillInts.Schedule(100, 16));

        writerJob.Complete();
        stream.Dispose();
    }

    [Test]
    public void ScheduleCreateThrows_NativeList()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        container.Add(2);

        NativeStream stream;
        var jobHandle = NativeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);

        Assert.Throws<InvalidOperationException>(() => { int val = stream.ForEachCount; });

        jobHandle.Complete();

        Assert.AreEqual(1, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void ScheduleCreateThrows_NativeArray()
    {
        var container = new NativeArray<int>(1, Allocator.Persistent);
        container[0] = 1;

        NativeStream stream;
        var jobHandle = NativeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);

        Assert.Throws<InvalidOperationException>(() => { int val = stream.ForEachCount; });

        jobHandle.Complete();

        Assert.AreEqual(1, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void OutOfBoundsWriteThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(-1));
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(2));

        stream.Dispose();
    }

    [Test]
    public void EndForEachIndexWithoutBeginThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.EndForEachIndex());

        stream.Dispose();
    }

    [Test]
    public void WriteWithoutBeginThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.Write(5));

        stream.Dispose();
    }

    [Test]
    public void WriteAfterEndThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(2);
        Assert.AreEqual(1, writer.ForEachCount);
        writer.EndForEachIndex();

        Assert.AreEqual(1, writer.ForEachCount);
        Assert.Throws<ArgumentException>(() => writer.Write(5));

        stream.Dispose();
    }

    [Test]
    public void UnbalancedBeginThrows()
    {
        var stream = new NativeStream(2, Allocator.Temp);
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        // Missing EndForEachIndex();
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(1));

        stream.Dispose();
    }

    static void CreateBlockStream1And2Int(out NativeStream stream)
    {
        stream = new NativeStream(2, Allocator.Temp);

        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(0);
        writer.EndForEachIndex();

        writer.BeginForEachIndex(1);
        writer.Write(1);
        writer.Write(2);
        writer.EndForEachIndex();
    }

    [Test]
    public void IncompleteReadThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        reader.Read<byte>();
        Assert.Throws<ArgumentException>(() => reader.EndForEachIndex());

        reader.BeginForEachIndex(1);

        stream.Dispose();
    }

    [Test]
    public void ReadWithoutBeginThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();
        Assert.Throws<ArgumentException>(() => reader.Read<int>());

        stream.Dispose();
    }

    [Test]
    public void TooManyReadsThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        reader.Read<byte>();
        Assert.Throws<ArgumentException>(() => reader.Read<byte>());

        stream.Dispose();
    }

    [Test]
    public void OutOfBoundsReadThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        Assert.Throws<ArgumentException>(() => reader.Read<long>());

        stream.Dispose();
    }

    [Test]
    public void CopyWriterByValueThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        writer.BeginForEachIndex(0);

        Assert.Throws<ArgumentException>(() =>
        {
            var writerCopy = writer;
            writerCopy.Write(5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var writerCopy = writer;
            writerCopy.BeginForEachIndex(1);
            writerCopy.Write(5);
        });

        stream.Dispose();
    }

    [Test]
    public void WriteSameIndexTwiceThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        writer.BeginForEachIndex(0);
        writer.Write(1);
        writer.EndForEachIndex();

        Assert.Throws<ArgumentException>(() =>
        {
            writer.BeginForEachIndex(0);
            writer.Write(2);
        });

        stream.Dispose();
    }

#if !UNITY_DOTSRUNTIME  // managed issue
    struct ManagedRef
    {
        string Value;
    }
    [Test]
    public void WriteManagedThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        writer.BeginForEachIndex(0);

        Assert.Throws<ArgumentException>(() =>
        {
            writer.Write(new ManagedRef());
        });

        stream.Dispose();
    }
#endif

#endif

    [Test]
    public void UnsafeStream_ScheduleCreate_NativeList()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        container.Add(13);
        container.Add(13);
        container.Add(13);
        container.Add(13);

        UnsafeStream stream;
        var jobHandle = UnsafeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);
        jobHandle.Complete();

        Assert.AreEqual(4, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void UnsafeStream_ScheduleCreate_NativeArray()
    {
        var container = new NativeArray<int>(1, Allocator.Persistent);
        container[0] = 4;

        UnsafeStream stream;
        var jobHandle = UnsafeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);
        jobHandle.Complete();

        Assert.AreEqual(4, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void NativeStream_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeStream(1, allocator.Handle))
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
                using (var container = new NativeStream(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeStream_BurstedCustomAllocatorTest()
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
