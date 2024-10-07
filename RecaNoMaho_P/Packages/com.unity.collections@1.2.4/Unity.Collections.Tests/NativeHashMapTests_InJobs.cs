using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Assert = FastAssert;

internal class NativeHashMapTests_InJobs : NativeHashMapTestsFixture
{
    [Test]
    public void NativeHashMap_Read_And_Write()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        for (int i = 0; i < hashMapSize; ++i)
        {
            Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
            Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
        }

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [Test]
    [IgnoreInPortableTests("The hash map exception is fatal.")]
    public void NativeHashMap_Read_And_Write_Full()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        var missing = new Dictionary<int, bool>();
        for (int i = 0; i < hashMapSize; ++i)
        {
            if (writeStatus[i] == -2)
            {
                missing[i] = true;
                Assert.AreEqual(-1, readValues[i], "Job read a value form hash map which should not be there");
            }
            else
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
            }
        }
        Assert.AreEqual(hashMapSize - hashMapSize / 2, missing.Count, "Wrong indices written to hash map");

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [Test]
    public void NativeHashMap_Key_Collisions()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = 16,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        var missing = new Dictionary<int, bool>();
        for (int i = 0; i < hashMapSize; ++i)
        {
            if (writeStatus[i] == -1)
            {
                missing[i] = true;
                Assert.AreNotEqual(i, readValues[i], "Job read a value form hash map which should not be there");
            }
            else
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
            }
        }
        Assert.AreEqual(hashMapSize - writeData.keyMod, missing.Count, "Wrong indices written to hash map");

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct Clear : IJob
    {
        public NativeHashMap<int, int> hashMap;

        public void Execute()
        {
            hashMap.Clear();
        }
    }

    [Test]
    [IgnoreInPortableTests("Hash map throws when full.")]
    public void NativeHashMap_Clear_And_Write()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var clearJob = new Clear
        {
            hashMap = hashMap
        };

        var clearJobHandle = clearJob.Schedule();

        var writeJob = new HashMapWriteJob
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var writeJobHandle = writeJob.Schedule(clearJobHandle);
        writeJobHandle.Complete();

        writeStatus.Dispose();
        hashMap.Dispose();
    }

    [Test]
    public void NativeHashMap_DisposeJob()
    {
        var container0 = new NativeHashMap<int, int>(1, Allocator.Persistent);
        Assert.True(container0.IsCreated);
        Assert.DoesNotThrow(() => { container0.Add(0, 1); });
        Assert.True(container0.ContainsKey(0));

        var container1 = new NativeMultiHashMap<int, int>(1, Allocator.Persistent);
        Assert.True(container1.IsCreated);
        Assert.DoesNotThrow(() => { container1.Add(1, 2); });
        Assert.True(container1.ContainsKey(1));

        var disposeJob0 = container0.Dispose(default);
        Assert.False(container0.IsCreated);
        Assert.Throws<ObjectDisposedException>(
            () => { container0.ContainsKey(0); });

        var disposeJob = container1.Dispose(disposeJob0);
        Assert.False(container1.IsCreated);
        Assert.Throws<ObjectDisposedException>(
            () => { container1.ContainsKey(1); });

        disposeJob.Complete();
    }

    [Test]
    public void NativeHashMap_DisposeJobWithMissingDependencyThrows()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var deps = new Clear { hashMap = hashMap }.Schedule();
        Assert.Throws<InvalidOperationException>(() => { hashMap.Dispose(default); });
        deps.Complete();
        hashMap.Dispose();
    }

    [Test]
    public void NativeHashMap_DisposeJobCantBeScheduled()
    {
        var hashMap = new NativeHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var deps = hashMap.Dispose(default);
        Assert.Throws<InvalidOperationException>(() => { new Clear { hashMap = hashMap }.Schedule(deps); });
        deps.Complete();
    }
}
