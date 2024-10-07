# Changelog

## [1.2.4] - 2022-05-31

### Changed

* Reverted some NativeArray test changes that were introduced in 1.0.0-pre.4

### Fixed

* Added an assembly definition file for sample code in the package to avoid spurious warnings when adding the package

## [1.2.3] - 2022-03-18

### Changed

* Minor fixes to changelog

## [1.2.3-pre.1] - 2022-03-04

### Changed

* Updated package dependencies

## [1.2.2] - 2022-03-03

### Changed

* Updated package `com.unity.test-framework` to version `1.1.31`.
* Updated package `com.unity.burst` to version `1.6.4`.

## [1.2.1] - 2022-02-17

### Fixed

* Shutdown the WordStorage with application exit to ensure memory is freed.
* `NativeList.AsDeferredJobArray` allocator label is changed to `Allocator.Invalid` to infer that the array is in list mode.

### Added

* Added FixedStringMethods.CopyFromTruncated to copy a string to a FixedString explicitly allowing truncation
* Added `NativeText.ReadOnly` type which provides a readonly, lightweight copy of a `NativeText` or `UnsafeText` type.
* New public API AllocatorHandle.UnmanagedUnregister, which unregisters an allocator without using managed code.

### Changed

* `Native/UnsafeMultiHashMap.GetUniqueKeyArrayNBC` extension methods from `Unity.Collections.NotBurstCompatible` are not necessary anymore. Burst supports tuple. Original methods `Native/UnsafeMultiHashMap.GetUniqueKeyArray` are now available again.
* Reverted some NativeArray test changes that were introduced in 1.0.0-pre.4
* Static safety ID created for all types containing a uniquely represented AtomicSafetyHandle


## [1.1.0] - 2021-10-27

### Added

* `REMOVE_DISPOSE_SENTINEL` ifdefs in all containers for future removal of DisposeSentinel.
* Bounds check to `Fixed/Native/UnsafeList`.
* `SetCapacity` and `TrimExcess` to `NativeList`.
* A custom allocator wrapper `AllocatorHelper` to facilitate custom allocator creation and destruction.
* NativeList<>.ArraysEqual & UnsafeList<>.ArraysEqual
* UnsafeList.CopyFrom

### Changed

* Only lower 15 bits of an allocator handle version are valid.

### Fixed

* Error in leak detection for NativeList created by RewindableAllocator.
* Removed pointer caching from `Native/UnsafeList.ParallelWriter`.
* `AtomicSafetyHandle` issue preventing use of `foreach` iterator in jobs for `NativeHashSet`, `NativeHashMap`, and `NativeMultiHashMap` containers.



## [1.0.0-pre.6] - 2021-08-31

### Removed

* VirtualMemoryUtility
* BaselibErrorState
* BaselibSourceLocation
* VMRange
* DisposeSentinel (managed object) from all `Native*` containers.

### Changed

* Native container memory allocations align to multiple cacheline size.
* UnsafeText is marshalable now (doesn't contain generic field UnsafeList<byte>).

### Fixed

* AllocatorManager.AllocateBlock no longer ignores alignment when allocating.
* Redundant and wrong computation of memory allocation alignment.



## [1.0.0-pre.5] - 2021-08-20

### Changed

* Renamed FixedListN to FixedListNBytes, for all N, and same for FixedString

### Fixed

* NativeBitArray, NativeQueue, NativeStream, and NativeText will no longer throw an exception when using a custom allocator inside of a job.


## [1.0.0-pre.4] - 2021-08-11

### Added

* `FixedList*` overflow checks when `UNITY_DOTS_DEBUG` is enabled.
* Disposed NativeArray related tests and updated some invalidated native array from native list tests to confirm that exceptions are thrown when accessing an object's Length and indexer following its disposal

### Changed

* Updated internal dependencies
* InvalidArrayAccessFromListJob check in the InvalidatedArrayAccessFromListThrowsInsideJob unit test to expect an ObjectDisposedException due to a change in the type thrown for AtomicSafetyHandle.CheckAndThrow

### Fixed

* Setting UnsafeList.Length will now resize the storage properly.

## [1.0.0-pre.3] - 2021-06-29

### Added

* `Native/UnsafeList*.RemoveRange*` with index/count arguments.
* Upgraded to burst 1.5.2
* `UnsafeText` added.

### Changed

* Burst compatibility tests now treat any explicit uses of `[BurstCompatible]` on private methods as an error (as opposed to silently ignoring) to avoid giving the impression that private methods are being tested.
* `NativeList<T>` generic constraint `T` is changed from `struct` to `unmanaged` to match `UnsafeList<T>`. User code can be simply fixed by changing `struct` to `unmanaged` when using `NativeList<T>` inside generic container.
* `NativeHashMap.GetBucketData` renamed to `NativeHashMap.GetUnsafeBucketData`
* Update the package to 1.0.0
* `HeapString` renamed to `NativeText`. `NativeText` is based on `UnsafeText`.

### Deprecated

* Generated `FixedList[Byte/Int/Float][32/64/128/256/512]` are deprecated, and replaced with generics `FixedList[32/64/128/256/512]<T>`.
* `UnsafeMultiHashMap<TKey, TValue>.GetUniqueKeyArray` replaced with extension method `UnsafeMultiHashMap<TKey, TValue>.GetUniqueKeyValueNBC` from `Unity.Collections.NotBurstCompatible` namespace.
* `NativeMultiHashMap<TKey, TValue>.GetUniqueKeyArray` replaced with extension method `NativeMultiHashMap<TKey, TValue>.GetUniqueKeyValueNBC` from `Unity.Collections.NotBurstCompatible` namespace.
* `NativeList<T>.ToArray` replaced with extension method `NativeList<T>.ToArrayNBC` from `Unity.Collections.NotBurstCompatible`
* `NativeList<T>.CopyFrom` replaced with extension method `NativeList<T>.CopyFromNBC` from `Unity.Collections.NotBurstCompatible` namespace.
* `UnsafeAppendBuffer.Add` replaced with extension method`UnsafeAppendBuffer.AddNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace.
* `UnsafeAppendBuffer.ToBytes` replaced with extension method `UnsafeAppendBuffer.ToBytesNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace.
* `UnsafeAppendBuffer.Reader.ReadNext` replaced with extension method `UnsafeAppendBuffer.Reader.ReadNextNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace.
* `Native/UnsafeList*.RemoveRange*WithBeginEnd` methods with begin/end arguments in favor of `Native/UnsafeList*.RemoveRange*` with index/count arguments.
* `UnsafeList` and replaced it with `UnsafeList<T>`.
* VirtualMemoryUtility.

### Removed

* `NativeQueue.PersistentMemoryBlockCount` and `NativeQueue.MemoryBlockSize` are now internal APIs.

### Fixed

* Burst compatibility tests will now ignore any method containing a '$' in the name, which can be generated by the Burst direct call IL post processor.
* xxHash3 is initialized after assembly load to avoid an exception that could be thrown if xxHash3 is accessed for the first time on a thread other than the main thread.

### Security




## [0.17.0] - 2021-03-15

### Added

* `[NotBurstCompatible]` attribute to FixedStringN constructors that use a String argument.
* `[NotBurstCompatible]` attribute to NativeQueue constructor.
* `UnsafeList<T>.Create` and `UnsafeList<T>.Destroy` API.
* BurstCompatibilityTests now has a constructor that accepts multiple assembly names to verify Burst compatibility. This allows one test to verify multiple assemblies and can dramatically reduce CI times by avoiding costly setup overhead.
* `UnsafePtrList<T>` to replace deprecated untyped `UnsafePtrList`.
* Burst compatibility tests now also write the generated code to the Temp directory in order to make it easier to inspect.
* `FixedList*.RemoveRange*` with index/count arguments.
* FixedString parsing to type uint


### Deprecated

* untyped UnsafePtrList, and added `UnsafePtrList<T>` as replacement.
* `FixedList*.RemoveRange*WithBeginEnd` methods with begin/end arguments in favor of `FixedList*.RemoveRange*` with index/count arguments.

### Removed

* Removed single arg `FixedString*.Format` extension methods, use `Clear()` followed by `Append()`.
* CollectionsBurstTests has been removed and placed into the Entities test project.
* `com.unity.test-framework.performance` preview package dependency, and moved performance unit tests depending on it into different location.

### Fixed

* `*BitArray.Clear` when clearing with very short bit arrays.
* `NativeQueue.AsParallelWriter` doesn't need to be cached when chaining jobs. Removed unnecessary safety handle that was preventing calling `NativeQueue.AsParallelWriter()` multiple times when scheduling jobs.


## [0.16.0] - 2021-01-26

### Deprecated

* Sort methods that return a JobHandle deprecated in favor of new SortJob methods that return a job struct. Less conveniently, the user is responsible for calling Schedule on the struct, but this pattern better accommodates scheduling generic jobs from Bursted code (See https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/ecs_generic_jobs.html).

### Removed

* Removed deprecated `FixedListN.IndexOf` and `SortJob` variants

### Fixed

* An ENABLE_UNITY_COLLECTIONS_CHECKS define was misspelled. Now Memory is checked for reasonable byte length when enabled.
* Many methods that use `IJob` were marked as `[NotBurstCompatible]` to reflect their true Burst compatibility.

### Changed

* Updated `com.unity.burst` to `1.4.4`


## [0.15.0] - 2020-11-13

### Added

* `NativeReference` constructor to initialize it with existing value.
* `T[] *HashSet.ToArray()` returns an array of all elements in the set.
* xxHash3 now also has a utility method to hash a struct directly (Previously it was only pointer + size)
* `[BurstCompatible]` attribute to FixedList and extensions.
* `[BurstCompatible]` attribute to CollectionHelper.
* `[BurstCompatible]` attribute to FixedBytesN.
* `[BurstCompatible]` attribute to HeapString.
* `[BurstCompatible]` attribute to NativeArrayExtensions.
* `[BurstCompatible]` attribute to NativeBitArray.
* `[BurstCompatible]` attribute to NativeBitArrayUnsafeUtility.
* `[BurstCompatible]` attribute to NativeHashMap.
* `[BurstCompatible]` attribute to NativeHashMapExtensions.
* `[BurstCompatible]` attribute to NativeHashSet.
* `[BurstCompatible]` attribute to NativeList.
* `[BurstCompatible]` attribute to NativeListUnsafeUtility.
* `[BurstCompatible]` attribute to NativeMultiHashMap.
* `[BurstCompatible]` attribute to NativeQueue.
* `[BurstCompatible]` attribute to NativeReference.
* `[BurstCompatible]` attribute to NativeStream.
* `[BurstCompatible]` attribute to NativeString.
* `[BurstCompatible]` attribute to UTF8ArrayUnsafeUtility.
* `[BurstCompatible]` attribute to Unicode and Rune.
* `[BurstCompatible]` attribute to NativeStringView.
* `[BurstCompatible]` attribute to UnsafeAppendBuffer.
* `[BurstCompatible]` attribute to UnsafeAtomicCounter32 and UnsafeAtomicCounter64.
* `[BurstCompatible]` attribute to UnsafeHashMap.
* `[BurstCompatible]` attribute to UnsafeHashSet.
* `[BurstCompatible]` attribute to UnsafeList, UnsafeListExtensions, UnsafePtrList, UnsafePtrListExtensions.
* `[BurstCompatible]` attribute to UnsafeRingQueue.
* `[BurstCompatible]` attribute to UnsafeScratchAllocator.
* `[BurstCompatible]` attribute to UnsafeUtilityExtensions.
* `[BurstCompatible]` attribute to VMRange, Baselib_ErrorState, and VirtualMemoryUtility.
* `[BurstCompatible]` attribute to xxHash3.

### Changed

* Update burst to 1.4.1.
* *BitArray `Length` would previously report backing capacity which is always 64-bit aligned, changed it to report number of bits user requested on allocation. For example, allocating 3 bits will now report `Length` 3 instead capacity which is always aligned to 64-bits.
* Update minimum editor version to 2020.1.2f1

### Fixed

* Code generation for indexers in structs with [BurstCompatible] attribute.
* *BitArray search for empty bits when number of bits is less than 7 and backing storage is fragmented with 0x81 bit pattern.
* Namespace for `*HashSet.ExceptWith/IntersectWith/UnionWith` extension methods, so that use of `Unity.Collections.LowLevel.Unsafe` namespace is not necessary.
* using FixedList.ToNativeArray with collection checks enabled used to throw a null reference exception.

## [0.14.0] - 2020-09-24

### Added

* `*UnsafeBitArray.Find` with pos/count search range arguments.

### Changed

* `UnsafeStream` block allocation performance has been improved by ~16% by appending to the start of the per-thread block lists rather than the end.

### Removed

* `FixedList*.InsertRange`, `FixedList*.RemoveRangeSwapBack`, `FixedList*.RemoveRange`, `NativeString*`, `NativeList.RemoveRangeSwapBack`, `NativeList.RemoveRange`, `UnsafeList.RemoveRangeSwapBack`, `UnsafeList.RemoveRange`, `FixedString*.Format`, `FixedString*.AppendFrom`, `NativeHashSet.TryAdd`, `UnsafeHashSet.TryAdd`.
* `[NativeContainerSupportsDeallocateOnJobCompletion]` attribute from `NativeReference` container. It didn't work properly. Users can use `Dispose(JobHandle)` method instead.

### Fixed

* `FixedList<T>` `Remove` and `RemoveSwapBack` extension methods were modifying copy, fixed by passing `this` by reference to modify actual container.

## [0.13.0] - 2020-08-26

### Added

* Added `*BitArray.Find` linear search for 0-bit range.
* Added `SortJob` extension methods for `NativeList`, `UnsafeList`, `UnsafeList<T>`, and `NativeSlice`.
* Added `Sort` method that accepts custom comparator, and job dependency, to all supported containers.
* Added `BinarySearch` extension methods for `NativeArray`, `NativeList`, `UnsafeList`, `UnsafeList<T>`, and `NativeSlice`.
* Added `foreach` support to `UnsafeList<T>`.

### Changed

* `Sort` functions that take an `IComparer` no longer require the sorted elements to be `IComparable`
* Bumped Burst to 1.3.5.

### Deprecated

* Deprecated `SortJob` with default job dependency argument. Use `Sort` that require an explicit JobHandle argument. If no dependency is needed, pass a default valued JobHandle.

### Removed

* Removed: `UnsafeUtilityEx`, `Unity.Collections.Experimental*`,`FixedString*.UTF8LengthInBytes`, and `*Stream.ComputeItemCount()`

### Fixed

* Fixed performance regression of `*HashMap.Count()` introduced in Collections 0.10.0.


## [0.12.0] - 2020-08-04

### Added

 * Added `Sort` method with custom comparer to `FixedList*` and `UnsafeList<T>`.
 * Added `IsEmpty` property and `Clear` method to `INativeList` intefrace.
 * Added `INativeDisposable` interface which provides a mechanism for scheduling
   release of unmanaged resources.
 * Added `InsertRangeWithBeginEnd` to `NativeList`, `UnsafeList`, `UnsafeList<T>`,
   and `UnsafePtrList`.
 * Added `AddRange` and `AddRangeNoResize` to `FixedList*`.
 * Added properties to `BaselibErrorState` to check if an operation resulted in success, out of memory, or accessing an invalid address range.
 * Added `HeapString` type, for arbitrary-length (up to 2GB) heap-allocated strings
   compatible with the `FixedString*` methods.  Allocating a `HeapString` requires
   specifying an allocator and disposing appropriately.


### Deprecated

 * Deprecated `FixedList*` method `IndexOf` with `index` and `index/count` arguments.

### Removed

 * Removed:
    `IJobNativeMultiHashMapMergedSharedKeyIndices`
    `JobNativeMultiHashMapUniqueHashExtensions`
    `IJobNativeMultiHashMapVisitKeyValue`
    `JobNativeMultiHashMapVisitKeyValue`
    `IJobNativeMultiHashMapVisitKeyMutableValue`
    `JobNativeMultiHashMapVisitKeyMutableValue`
    `IJobUnsafeMultiHashMapMergedSharedKeyIndices`
    `JobUnsafeMultiHashMapUniqueHashExtensions`
    `IJobUnsafeMultiHashMapVisitKeyValue`
    `JobUnsafeMultiHashMapVisitKeyValue`
    `IJobUnsafeMultiHashMapVisitKeyMutableValue`
    `JobUnsafeMultiHashMapVisitKeyMutableValue`

### Fixed

 * Fixed `*HashMap.IsEmpty` when items are added and removed from `*HashMap. IsEmpty` previously
   used allocated count only to report emptiness, but returning not-empty didn't actually
   meant that `*HashMap` is not empty.
 * Fixed bug where `*HashSet.Enumerator.Current` would always return the default value instead of the actual value from the set.
 * Fixed bug with `*HashMap/Set.Enumerator` returning wrong index and dereferencing out of bounds memory.


## [0.11.0] - 2020-07-10

### Added

 * Added `VirtualMemoryUtility` providing low-level virtual memory utility functions backed by baselib.
 * `*HashMap` and `*HashSet` now implement `IEnumerable<>`.
 * `ReadArrayElementBoundsChecked` and `WriteArrayElementBoundsChecked` for ease of debugging `ReadArrayElement` and `WriteArrayElement` without sacrificing performance by adding bounds checking directly to those functions.
 * Added `InsertRangeWithBeginEnd`, `RemoveRangeSwapBackWithBeginEnd`, and `RemoveRangeWithBeginEnd` to list containers.
   `*WithBeginEnd` in name signifies that arguments are begin/end instead of more standard index/count. Once
   `InsertRange`, `RemoveRangeSwapBack`, and `RemoveRange` are completely deprecated and removed,
   those methods will be added with correct index/count arguments.
 * Added `xxHash3` type to expose 64/128bits hashing API using xxHash3 algorithm (corresponding to the C++ version https://github.com/Cyan4973/xxHash/releases/tag/v0.8.0)
### Changed

 * Updated minimum Unity Editor version to 2020.1.0b15 (40d9420e7de8)
 * Bumped burst to 1.3.2 version.
 * Changed `*HashSet.Add` API to return bool when adding element to set.
 * `UnsafeUtilityExtensions` is now public.
 * `NativeReference` methods `Equals` and `GetHashCode` will now operate on the value instead of the data pointer.
 * `FixedString{32,64,128,512,4096}` have been reworked.
   * Functionality is shared via generics as much as possible.  The API attempts to follow `StringBuilder` semantics.
   * `Append` methods now consistently append.
   * `Append` variant to append a char was added (appends the `char`, does not resolve to int overload).
   * `Format` methods that replaced the contents of the target have been deprecated.  Use `Clear()` followed by `Append()`.  Because FixedStrings start out cleared, in most cases just an `Append` is sufficient.
   * `Format` that takes a format string has been renamed to `AppendFormat`.  The static `FixedString.Format` methods still exist for convenience, and return a `FixedString128`.
   * It is possible for users to extend the `Append` family of methods to support appending their own types.  See `FixedStringAppendMethods.cs` for examples of how to declare your own extension methods.

### Deprecated

 * Deprecated `*HashSet.TryAdd`. `*HashSet.Add` is equivalent.
 * Deprecated `NativeString*`. The functionality is replaced by `FixedString*`.
 * Deprecated `InsertRange`, `RemoveRangeSwap`, and `RemoveRange` from list containers, and added
   `InsertRangeWithBeginEnd`, `RemoveRangeSwapBackWithBeginEnd`, and `RemoveRangeWithBeginEnd`.
   `*WithBeginEnd` in name signifies that arguments are begin/end instead of more standard index/count. Once
   `InsertRange`, `RemoveRangeSwapBack`, and `RemoveRange` are completely deprecated and removed,
   those methods will be added with correct index/count arguments.

### Removed

 * Removed `System.Runtime.CompilerServices.Unsafe.dll` from package.


### Known Issues

* This version is not compatible with 2020.2.0a17. Please update to the forthcoming alpha.

All containers allocated with `Allocator.Temp` on the same thread use a shared `AtomicSafetyHandle` instance. This is problematic when using `NativeHashMap`, `NativeMultiHashMap`, `NativeHashSet` and `NativeList` together in situations where their secondary safety handle is used. This means that operations that invalidate an enumerator for either of these collections (or the `NativeArray` returned by `NativeList.AsArray`) will also invalidate all other previously acquired enumerators.
For example, this will throw when safety checks are enabled:
```
var list = new NativeList<int>(Allocator.Temp);
list.Add(1);

// This array uses the secondary safety handle of the list, which is
// shared between all Allocator.Temp allocations.
var array = list.AsArray();

var list2 = new NativeHashSet<int>(Allocator.Temp);

// This invalidates the secondary safety handle, which is also used
// by the list above.
list2.TryAdd(1);

// This throws an InvalidOperationException because the shared safety
// handle was invalidated.
var x = array[0];
```
This defect will be addressed in a future release.




## [0.10.0] - 2020-05-27


### Added

 * Added `Native/UnsafeHashSet` containers.
 * Added `IsEmpty` method to `*Queue`, `*HashMap`, `*MultiHashMap`, `*List`, `FixedString`.
   This method should be prefered to `Count() > 0` due to simpler checks for empty container.
 * Added a new container `NativeReference` to hold unmanaged allocation.
 * Added `CollectionsTestFixture` to enable jobs debugger and verify safety checks are enabled.
 * Added `NativeList.CopyFrom(NativeArray<> array)`

### Changed
 * Updated minimum Unity Editor version to 2020.1.0b9 (9c0aec301c8d)
 * Updated package `com.unity.burst` to version `1.3.0-preview.12`.
 * Made several tests inherit `CollectionsTestFixture` to prevent crashing when running tests without jobs debugger or safety checks enabled.
 * Added `NativeBitArray.AsNativeArray<T>` method to reinterpret `NativeBitArray` as
   `NativeArray` of desired type.

### Deprecated

 * Deprecated `NativeArrayChunked8` and `NativeArrayFullSOA` from Unity.Collections.Experimental.
 * Deprecated `UnsafeUtilityEx.As/AsRef/ArrayElementAsRef`. The functionality is available in `UnsafeUtility`.

### Fixed

 * `FixedString` and `FixedList` types now display their contents in the Entity Inspector.
 * Fixed `NativeHashMap.ParallelWriter.TryAdd` race condition.

## [0.9.0] - 2020-05-04

### Added

 * Added `RemoveAt` and `RemoveRange` to List containers in collections. These methods remove
   elements in list container while preserving order of the list. These methods are slower than
   `Remove*SwapBack` methods and users should prefer `Remove*SwapBack` if they don't care about
   preserving order inside \*List container.
 * Added `*BitArray.Copy` between two different bit arrays.
 * Added `NativeBitArrayUnsafeUtility.ConvertExistingDataToNativeBitArray` for assigning view into
   data as bit array.

### Changed

* Updated package `com.unity.burst` to version `1.3.0-preview.11`

### Fixed

 * Moved `NativeMultiHashMap.Remove<TValueEQ>(TKey key, TValueEq value)` into an extension method and made it Burst compatible
 * Fixed bug in `*HashMap.Remove` to not throw when removing from empty hash map.


## [0.8.0] - 2020-04-24

### Added

 * Added `Native/UnsafeBitArray.Copy` for copying or shifting bits inside array.
 * Added `UnsafeAtomicCounter32/64` providing helper interface for atomic counter functionality.
 * Added `NativeBitArray` providing arbitrary sized bit array functionality with safety mechanism.

### Changed

 * Bumped Burst version to improve compile time and fix multiple bugs.

### Deprecated

 * Deprecated `IJobNativeMultiHashMapMergedSharedKeyIndices`, `JobNativeMultiHashMapUniqueHashExtensions`,
   `IJobNativeMultiHashMapVisitKeyValue`, `JobNativeMultiHashMapVisitKeyValue`, `IJobNativeMultiHashMapVisitKeyMutableValue`,
   `JobNativeMultiHashMapVisitKeyMutableValue`, and introduced `NativeHashMap.GetUnsafeBucketData` and
   `NativeMultiHashMap.GetUnsafeBucketData` to obtain internals to implement deprecated functionality
   inside user code. If this functionality is used, the best is to copy deprecated code into user code.

### Removed

* Removed expired API `class TerminatesProgramAttribute`


## [0.7.1] - 2020-04-08

### Deprecated

 * Deprecated `Length` property from `NativeHashMap`, `UnsafeHashMap`, `NativeMultiHashMap`,
   `UnsafeMultiHashMap`, `NativeQueue`, and replaced it with `Count()` to reflect that there
   is computation being done.

### Fixed

 * Fixed an issue where `FixedListDebugView<T>` only existed for IComparable types, which lead to a crash while debugging other types.
 * Removed code that made NativeStream incompatible with Burst.


## [0.7.0] - 2020-03-13

### Added

 * Added ability to dispose NativeKeyValueArrays from job (DisposeJob).
 * Added `NativeQueue<T>.ToArray` to copy a native queue to an array efficiently

### Changed

 * Upgraded Burst to fix multiple issues and introduced a native debugging feature.

### Deprecated

 * Deprecated `Length` property from `NativeHashMap`, `UnsafeHashMap`, `NativeMultiHashMap`,
   `UnsafeMultiHashMap`, `NativeQueue`, and replaced it with `Count()` to reflect that there
   is computation being done.

### Removed

* Removed expired API `CollectionHelper.CeilPow2()`
* Removed expired API `CollectionHelper.lzcnt()`
* Removed expired API `struct ResizableArray64Byte<T>`

### Fixed

* Removed code that made `NativeStream` incompatible with Burst.


## [0.6.0] - 2020-03-03

### Added

 * Added ability to dispose `UnsafeAppendBuffer` from a `DisposeJob`.
 * Added NativeHashSetExtensions and UnsafeHashSetExtensions for HashSetExtensions in different namespaces.

### Changed

 * `UnsafeAppendBuffer` field `Size` renamed to `Length`.
 * Removed `[BurstDiscard]` from all validation check functions. Validation is present in code compiled with Burst.

### Removed

* Removed expired overloads for `NativeStream.ScheduleConstruct` without explicit allocators.
* Removed HashSetExtensions, replaced with NativeHashSetExtensions and UnsafeHashSetExtensions.

### Fixed

 * Fixed `UnsafeBitArray` out-of-bounds access.


## [0.5.2] - 2020-02-17

### Changed

* Changed `NativeList<T>` parallel reader/writer to match functionality of `UnsafeList` parallel reader/writer.
* Updated dependencies of this package.

### Removed

* Removed expired API `UnsafeUtilityEx.RestrictNoAlias`

### Fixed

 * Fixed bug in `NativeList.CopyFrom`.


## [0.5.1] - 2020-01-28

### Changed

 * Updated dependencies of this package.


## [0.5.0] - 2020-01-16

### Added

 * Added `UnsafeRingQueue<T>` providing fixed-size circular buffer functionality.
 * Added missing `IDisposable` constraint to `UnsafeList` and `UnsafeBitArray`.
 * Added `ReadNextArray<T>` to access a raw array (pointer and length) from an `UnsafeAppendBuffer.Reader`.
 * Added FixedString types, guaranteed binary-layout identical to NativeString types, which they are intended to replace.
 * Added `FixedList<T>` generic self-contained List struct
 * Added `BitArray.SetBits` with arbitrary ulong value.
 * Added `BitArray.GetBits` to retrieve bits as ulong value.

### Changed

 * Changed `UnsafeBitArray` memory initialization option default to `NativeArrayOptions.ClearMemory`.
 * Changed `FixedList` structs to pad to natural alignment of item held in list

### Deprecated

 * `BlobAssetComputationContext.AssociateBlobAssetWithGameObject(int, GameObject)` replaced by its `UnityEngine.Object` counterpart `BlobAssetComputationContext.AssociateBlobAssetWithUnityObject(int, UnityEngine.Object)` to allow association of BlobAsset with any kind of `UnityEngine.Object` derived types.
 * Adding removal dates to the API that have been deprecated but did not have the date set.

### Removed

 * Removed `IEquatable` constraint from `UnsafeList<T>`.

### Fixed

 * Fixed `BitArray.SetBits`.


## [0.4.0] - 2019-12-16

**This version requires Unity 2019.3.0f1+**

### New Features

* Adding `FixedListTN` as a non-generic replacement for `ResizableArrayN<T>`.
* Added `UnsafeBitArray` providing arbitrary sized bit array functionality.

### Fixes

* Updated performance package dependency to 1.3.2 which fixes an obsoletion warning
* Adding `[NativeDisableUnsafePtrRestriction]` to `UnsafeList` to allow burst compilation.


## [0.3.0] - 2019-12-03

### New Features

* Added fixed-size `BitField32` and `BitField64` bit array.

### Changes

Removed the following deprecated API as announced in/before `0.1.1-preview`:

* Removed `struct Concurrent` and `ToConcurrent()` for `NativeHashMap`, `NativeMultiHashMap` and `NativeQueue` (replaced by the *ParallelWriter* API).
* From NativeStream.cs: `struct NativeStreamReader` and `struct NativeStreamWriter`, replaced by `struct NativeStream.Reader` and `struct NativeStream.Writer`.
* From NativeList.cs: `ToDeferredJobArray()` (replaced by `AsDeferredJobArray()` API).


## [0.2.0] - 2019-11-22

**This version requires Unity 2019.3 0b11+**

### New Features

* Added fixed-size UTF-8 NativeString in sizes of 32, 64, 128, 512, and 4096 bytes.
* Added HPC# functions for float-to-string and string-to-float.
* Added HPC# functions for int-to-string and string-to-int.
* Added HPC# functions for UTF16-to-UTF8 and UTF8-to-UTF16.
* New `Native(Multi)HashMap.GetKeyValueArrays` that will query keys and values
  at the same time into parallel arrays.
* Added `UnsafeStream`, `UnsafeHashMap`, and `UnsafeMultiHashMap`, providing
  functionality of `NativeStream` container but without any safety mechanism
  (intended for advanced users only).
* Added `AddNoResize` methods to `NativeList`. When it's known ahead of time that
  list won't grow, these methods won't try to resize. Rather exception will be
  thrown if capacity is insufficient.
* Added `ParallelWriter` support for `UnsafeList`.
* Added `UnsafeList.TrimExcess` to set capacity to actual number of elements in
  the container.
* Added convenience blittable `UnsafeList<T>` managed container with unmanaged T
  constraint.

### Changes

* `UnsafeList.Resize` now doesn't resize to lower capacity. User must call
  `UnsafeList.SetCapacity` to lower capacity of the list. This applies to all other
  containers based on `UnsafeList`.
* Updated dependencies for this package.

### Fixes

* Fixed NativeQueue pool leak.


## [0.1.1] - 2019-08-06

### Fixes

* `NativeHashMap.Remove(TKey key, TValueEQ value)` is now supported in bursted code.
* Adding deprecated `NativeList.ToDeferredJobArray()` back in - Use `AsDeferredJobArray()`
  instead. The deprecated function will be removed in 3 months. This can not be auto-upgraded
  prior to Unity `2019.3`.
* Fixing bug where `TryDequeue` on an empty `NativeQueue` that previously had enqueued elements could leave it in
  an invalid state where `Enqueue` would fail silently afterwards.

### Changes

* Updated dependencies for this package.


## [0.1.0] - 2019-07-30

### New Features

* NativeMultiHashMap.Remove(key, value) has been addded. It lets you remove
  all key & value pairs from the hashmap.
* Added ability to dispose containers from job (DisposeJob).
* Added UnsafeList.AddNoResize, and UnsafeList.AddRangeNoResize.
* BlobString for storing string data in a blob

### Upgrade guide

* `Native*.Concurrent` is renamed to `Native*.ParallelWriter`.
* `Native*.ToConcurrent()` function is renamed to `Native*.AsParallelWriter()`.
* `NativeStreamReader/Writer` structs are subclassed and renamed to
  `NativeStream.Reader/Writer` (note: changelot entry added retroactively).

### Changes

* Deprecated ToConcurrent, added AsParallelWriter instead.
* Allocator is not an optional argument anymore, user must always specify the allocator.
* Added Allocator to Unsafe\*List container, and removed per method allocator argument.
* Introduced memory intialization (NativeArrayOptions) argument to Unsafe\*List constructor and Resize.

### Fixes

* Fixed UnsafeList.RemoveRangeSwapBack when removing elements near the end of UnsafeList.
* Fixed safety handle use in NativeList.AddRange.


## [0.0.9-preview.20] - 2019-05-24

### Changes

* Updated dependencies for `Unity.Collections.Tests`


## [0.0.9-preview.19] - 2019-05-16

### New Features

* JobHandle NativeList.Dispose(JobHandle dependency) allows Disposing the container from a job.
* Exposed unsafe NativeSortExtension.Sort(T* array, int length) method for simpler sorting of unsafe arrays
* Imporoved documentation for `NativeList`
* Added `CollectionHelper.WriteLayout` debug utility

### Fixes

* Fixes a `NativeQueue` alignment issue.


## [0.0.9-preview.18] - 2019-05-01

Change tracking started with this version.
