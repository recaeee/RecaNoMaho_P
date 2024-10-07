---
uid: collections-allocation
---

# Using unmanaged memory

The `Native-` and `Unsafe-` collections in this package are allocated from unmanaged memory, meaning their existence is unknown to the garbage collector.
You are responsible for deallocating any unmanaged memory that you no longer need. Failing to deallocate large or numerous allocations can lead to wasting more and more memory, which may eventually slow down or even crash your program.

## Allocators

An *allocator* governs some unmanaged memory from which you can make allocations. Different allocators organize and track their memory in different ways. The three standard provided allocators are:

### `Allocator.Temp`

**The fastest allocator. For very short-lived allocations. Temp allocations *cannot* be passed into jobs.**

Each frame, the main thread creates a Temp allocator which is deallocated in its entirety at the end of the frame. Each job also creates one Temp allocator per thread, and these are deallocated in their entireties at the end of the job. Because a Temp allocator gets discarded as a whole, you actually don't need to manually deallocate your Temp allocations (in fact, doing so is a no-op).

Temp allocations are only safe to use within the thread where they were allocated. So while Temp allocations can be made *within* a job, **main thread Temp allocations cannot be passed into a job**. For example, a NativeArray that's Temp allocated in the main thread cannot be passed into a job.

### `Allocator.TempJob`

**The next fastest allocator. For short-lived allocations. TempJob allocations can be passed into jobs.**

You are expected to deallocate your TempJob allocations within 4 frames of their creation. The number 4 was chosen because it's common to want allocations that last a couple frames: the limit of 4 accommodates this need with a comfortable extra margin.

For the `Native-` collection types, the disposal safety checks will throw an exception if a TempJob allocation lives longer than 4 frames. For the `Unsafe-` collection types, you are still expected to deallocate them within 4 frames, but no safety checks are performed to ensure you do so.
   
### `Allocator.Persistent`

**The slowest allocator. For indefinite lifetime allocations. Persistent allocations can be passed into jobs.**

Because Persistent allocations are allowed to live indefinitely, no safety check can detect if a Persistent allocation has outlived its intended lifetime. Consequently, you should be extra careful to deallocate a Persistent allocation when you no longer need it.

## Disposal (deallocation)

Each collection retains a reference to the allocator from which its memory was allocated because deallocation requires specifying the allocator.

- An `Unsafe-` collection's `Dispose` method deallocates its memory.
- A `Native-` collection's `Dispose` method deallocates its memory and frees the handles needed for safety checks. 
- An enumerator's `Dispose` method is a no-op. The method is included only to fulfill the `IEnumerator<T>` interface.

We often want to dispose a collection after the jobs which need it have run. The `Dispose(JobHandle)` method creates and schedules a job which will dispose the collection, and this new job takes the input handle as its dependency. Effectively, the method differs disposal until after the dependency runs:

[!code-cs[allocation_dispose_job](../DocCodeSamples.Tests/CollectionsAllocationExamples.cs#allocation_dispose_job)]

### The `IsCreated` property

The `IsCreated` property of a collection is false only in two cases:

1. Immediately after creating a collection with its default constructor.
2. After `Dispose` has been called on the collection.

Understand, however, that you're not intended to use a collections's default constructor. It's only made available because C# requires all structs to have a public default constructor.

Also note that calling `Dispose` on a collection sets `IsCreated` to false *only in that struct*, not in any copies of the struct. Consequently, `IsCreated` may still be true even after the collection's underlying memory was deallocated if...

- `Dispose` was called on a different copy of the struct.
- Or the underlying memory was deallocated *via* an [alias](#aliasing).

## Aliasing

An *alias* is a collection which does not have its own allocation but instead shares the allocation of another collection, in whole or in part. For example, an UnsafeList can be created that doesn't allocate its own memory but instead uses a NativeList's allocation. Writing to this shared memory *via* the UnsafeList affects the content of the NativeList, and *vice versa*.

You do not need to dispose aliases, and in fact, calling `Dispose` on an alias does nothing. Once an original is disposed, the aliases of that original can no longer be used:

[!code-cs[allocation_aliasing](../DocCodeSamples.Tests/CollectionsAllocationExamples.cs#allocation_aliasing)]

Aliasing can be useful in a few scenarios:

- Getting a collection's data in the form of another collection type without copying the data. For example, you can create an UnsafeList that aliases a NativeArray.
- Getting a subrange of a collection's data without copying the data. For example, you can create an UnsafeList that aliases a subrange of another list or array.   
- [Array reinterpretation](#array-reinterpretation).

Perhaps surprisingly, it's allowed for an `Unsafe-` collection to alias a `Native-` collection even though such cases undermine the safety checks. For example, if an UnsafeList aliases a NativeList, it's not safe to schedule a job that accesses one while also another job is scheduled that accesses the other, but the safety checks do not catch these cases. It is your responsibility to avoid such mistakes.

### Array reinterpretation

A *reinterpretation* of an array is an alias of the array that reads and writes the content as a different element type. For example, a `NativeArray<int>` which reinterprets a `NativeArray<ushort>` shares the same bytes, but it reads and writes the bytes as ints instead of ushorts; because each int is 4 bytes while each ushort is 2 bytes, each int corresponds to two ushorts, and the reinterpretation has half the length of the original.

[!code-cs[allocation_reinterpretation](../DocCodeSamples.Tests/CollectionsAllocationExamples.cs#allocation_reinterpretation)]

