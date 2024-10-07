# Collection types

## Array-like types

A few key array-like types are provided by the [core module](https://docs.unity3d.com/ScriptReference/UnityEngine.CoreModule), including [`Unity.Collections.NativeArray<T>`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1) and [`Unity.Collections.NativeSlice<T>`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeSlice_1). This package itself provides:

Data structure          | Description
----------------------- | -----------
@Unity.Collections.NativeList`1            | A resizable list. Has thread- and disposal-safety checks. 
@Unity.Collections.LowLevel.Unsafe.UnsafeList`1            | A resizable list.
@Unity.Collections.LowLevel.Unsafe.UnsafePtrList`1         | A resizable list of pointers.
@Unity.Collections.NativeStream          | A set of append-only, untyped buffers. Has thread- and disposal-safety checks.
@Unity.Collections.LowLevel.Unsafe.UnsafeStream          | A set of append-only, untyped buffers.
@Unity.Collections.LowLevel.Unsafe.UnsafeAppendBuffer    | An append-only untyped buffer.
@Unity.Collections.NativeQueue`1           | A resizable queue. Has thread- and disposal-safety checks. 
@Unity.Collections.LowLevel.Unsafe.UnsafeRingQueue`1       | A fixed-size circular buffer.
@Unity.Collections.FixedList32Bytes`1        | A 32-byte list, including 2 bytes of overhead, so 30 bytes are available for storage. Max capacity depends upon T.

`FixedList32Bytes<T>` has variants of larger sizes: `FixedList64Bytes<T>`, `FixedList128Bytes<T>`, `FixedList512Bytes<T>`, `FixedList4096Bytes<T>`.

There are no multi-dimensional array types, but you can simply pack all the data into a single-dimension. For example, for an `int[4][5]` array, use an `int[20]` array instead (because `4 * 5` is `20`).

When using the Entities package, a [DynamicBuffer]() component is often the best choice for an array- or list-like collection.

See also @Unity.Collections.NativeArrayExtensions, @Unity.Collections.ListExtensions, @Unity.Collections.NativeSortExtension.

## Map and set types

Data structure          | Description
----------------------- | -----------
@Unity.Collections.NativeHashMap`2         | An unordered associative array of key-value pairs. Has thread- and disposal-safety checks.
@Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2         | An unordered associative array of key-value pairs.
@Unity.Collections.NativeHashSet`1         | A set of unique values. Has thread- and disposal-safety checks. 
@Unity.Collections.LowLevel.Unsafe.UnsafeHashSet`1         | A set of unique values.
@Unity.Collections.NativeMultiHashMap`2    | An unordered associative array of key-value pairs. The keys do not have to be unique, *i.e.* two pairs can have equal keys. Has thread- and disposal-safety checks. 
@Unity.Collections.LowLevel.Unsafe.UnsafeMultiHashMap`2    | An unordered associative array of key-value pairs. The keys do not have to be unique, *i.e.* two pairs can have equal keys.

See also @Unity.Collections.HashSetExtensions, @Unity.Collections.NotBurstCompatible.Extensions, and @Unity.Collections.LowLevel.Unsafe.NotBurstCompatible.Extensions

## Bit arrays and bit fields

Data structure          | Description
----------------------- | -----------
@Unity.Collections.BitField32            | A fixed-size array of 32 bits.
@Unity.Collections.BitField64            | A fixed-size array of 64 bits.
@Unity.Collections.NativeBitArray        | An arbitrary-sized array of bits. Has thread- and disposal-safety checks.
@Unity.Collections.LowLevel.Unsafe.UnsafeBitArray        | An arbitrary-sized array of bits.

## String types

Data structure          | Description
----------------------- | -----------
@Unity.Collections.NativeText            | A UTF-8 encoded string. Mutable and resizable. Has thread- and disposal-safety checks.
@Unity.Collections.FixedString32Bytes        | A 32-byte UTF-8 encoded string, including 3 bytes of overhead, so 29 bytes available for storage.

`FixedString32Bytes` has variants of larger sizes: `FixedString64Bytes`, `FixedString128Bytes`, `FixedString512Bytes`, `FixedString4096Bytes`.

See also @Unity.Collections.FixedStringMethods
  
## Other types

Data structure          | Description
----------------------- | -----------
@Unity.Collections.NativeReference`1       | A reference to a single value. Functionally equivalent to an array of length 1. Has thread- and disposal-safety checks. 
@Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter32 | A 32-bit atomic counter.
@Unity.Collections.LowLevel.Unsafe.UnsafeAtomicCounter64 | A 64-bit atomic counter.
 
## Job safety checks

The purpose of the job safety checks is to detect job conflicts. Two jobs conflict if:

 1. Both jobs access the same data.
 2. One job or both jobs have write access to the data.
 
In other words, there's no conflict if both jobs just have read only access to the data.

For example, you generally wouldn't want one job to read an array while meanwhile another job is writing the same array, so the safety checks consider that possibility to be a conflict. To resolve such conflicts, you must make one job a dependency of the other to ensure their execution does not overlap. Whichever of the two jobs you want to run first should be the dependency of the other.

When the safety checks are enabled, each `Native-` collection has an `AtomicSafetyHandle` for performing thread-safety checks. Scheduling a job locks the `AtomicSafetyHandle`'s of all `Native-` collections in the job. Completing a job releases the `AtomicSafetyHandle`'s of all `Native-` collections in the job. 

While a `Native-` collection's `AtomicSafetyHandle` is locked:

1. Jobs which use the collection can only be scheduled if they depend upon all the already scheduled job(s) which also use it.
2. Accessing the collection from the main thread will throw an exception.

### Read only access in jobs

As a special case, there's no conflict between two jobs if they both strictly just read the same data, *.e.g.* there's no conflict if one job reads from an array while meanwhile another also job reads from the same array.  

The @Unity.Collections.ReadOnlyAttribute marks a `Native-` collection in a job struct as being read only:

[!code-cs[read_only](../DocCodeSamples.Tests/CollectionsExamples.cs#read_only)]

Marking collections as read only has two benefits:

1. The main thread can still read a collection if all scheduled jobs that use the collection have just read only access.
2. The safety checks will not object if you schedule multiple jobs with read only access to the same collection, even without any dependencies between them. Therefore these jobs can run concurrently with each other. 

## Enumerators

Most of the collections have a `GetEnumerator` method, which returns an implementation of `IEnumerator<T>`. The enumerator's `MoveNext` method advances its `Current` property to the next element.

[!code-cs[enumerator](../DocCodeSamples.Tests/CollectionsExamples.cs#enumerator)]

## Parallel readers and writers

Several of the collection types have nested types for reading and writing from parallel jobs. For example, to write safely to a `NativeList<T>` from a parallel job, you need a `NativeList<T>.ParallelWriter`:

[!code-cs[parallel_writer](../DocCodeSamples.Tests/CollectionsExamples.cs#parallel_writer)]

[!code-cs[parallel_writer_job](../DocCodeSamples.Tests/CollectionsExamples.cs#parallel_writer_job)]

Note that these parallel readers and writers do not usually support the full functionality of the collection. For example, a `NativeList` cannot grow its capacity in a parallel job (because there is no way to safely allow this without incurring significantly more synchronization overhead).

### Deterministic reading and writing

Although a `ParallelWriter` ensures the safety of concurrent writes, the *order* of the concurrent writes is inherently indeterminstic because it depends upon the happenstance of thread scheduling (which is controlled by the operating system and other factors outside of your program's control).

Likewise, although a `ParallelReader` ensures the safety of concurrent reads, the *order* of the concurrent reads is inherently indeterminstic, so it can't be known which threads will read which values.

One solution is to use either @Unity.Collections.NativeStream or @Unity.Collections.LowLevel.Unsafe.UnsafeStream, which splits reads and writes into a separate buffer for each thread and thereby avoids indeterminism.

Alternatively, you can effectively get a deterministic order of parallel reads if you deterministically divide the reads into separate ranges and process each range in its own thread.

You can also get a deterministic order if you deterministically sort the data after it has been written to the list.
