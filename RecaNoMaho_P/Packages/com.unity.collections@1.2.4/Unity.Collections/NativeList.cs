using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Collections
{
    /// <summary>
    /// An indexable collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    public interface IIndexable<T> where T : struct
    {
        /// <summary>
        /// The current number of elements in the collection.
        /// </summary>
        /// <value>The current number of elements in the collection.</value>
        int Length { get; set; }

        /// <summary>
        /// Returns a reference to the element at a given index.
        /// </summary>
        /// <param name="index">The index to access. Must be in the range of [0..Length).</param>
        /// <returns>A reference to the element at the index.</returns>
        ref T ElementAt(int index);
    }

    /// <summary>
    /// A resizable list.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    public interface INativeList<T> : IIndexable<T> where T : struct
    {
        /// <summary>
        /// The number of elements that fit in the current allocation.
        /// </summary>
        /// <value>The number of elements that fit in the current allocation.</value>
        /// <param name="value">A new capacity.</param>
        int Capacity { get; set; }

        /// <summary>
        /// Whether this list is empty.
        /// </summary>
        /// <value>True if this list is empty.</value>
        bool IsEmpty { get; }

        /// <summary>
        /// The element at an index.
        /// </summary>
        /// <param name="index">An index.</param>
        /// <value>The element at the index.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if index is out of bounds.</exception>
        T this[int index] { get; set; }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        void Clear();
    }

    /// <summary>
    /// An unmanaged, resizable list.
    /// </summary>
    /// <remarks>The elements are stored contiguously in a buffer rather than as linked nodes.</remarks>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeListDebugView<>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
    public unsafe struct NativeList<T>
        : INativeDisposable
        , INativeList<T>
        , IEnumerable<T> // Used by collection initializers.
        where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal int m_SafetyIndexHint;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeList<T>>();

#if REMOVE_DISPOSE_SENTINEL
#else
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel m_DisposeSentinel;
#endif
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList<T>* m_ListData;

        //@TODO: Unity.Physics currently relies on the specific layout of NativeList in order to
        //       workaround a bug in 19.1 & 19.2 with atomic safety handle in jobified Dispose.
        internal AllocatorManager.AllocatorHandle m_DeprecatedAllocator;

        /// <summary>
        /// Initializes and returns a NativeList with a capacity of one.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        public NativeList(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator, 2)
        {
        }

        /// <summary>
        /// Initializes and returns a NativeList.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list.</param>
        /// <param name="allocator">The allocator to use.</param>
        public NativeList(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
            : this(initialCapacity, allocator, 2)
        {
        }

        [BurstCompatible(GenericTypeArguments = new [] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Initialize<U>(int initialCapacity, ref U allocator, int disposeSentinelStackDepth) where U : unmanaged, AllocatorManager.IAllocator
        {
            var totalSize = sizeof(T) * (long)initialCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);
            CollectionHelper.CheckIsUnmanaged<T>();
            CheckTotalSize(initialCapacity, totalSize);

#if REMOVE_DISPOSE_SENTINEL
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
#else
            if (allocator.IsCustomAllocator)
            {
                m_Safety = AtomicSafetyHandle.Create();
                m_DisposeSentinel = null;
            }
            else
            {
                DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator.ToAllocator);
            }
#endif

            CollectionHelper.SetStaticSafetyId<NativeList<T>>(ref m_Safety, ref s_staticSafetyId.Data);

            m_SafetyIndexHint = (allocator.Handle).AddSafetyHandle(m_Safety);
#endif
            m_ListData = UnsafeList<T>.Create(initialCapacity, ref allocator);
            m_DeprecatedAllocator = allocator.Handle;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        [BurstCompatible(GenericTypeArguments = new [] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static NativeList<T> New<U>(int initialCapacity, ref U allocator, int disposeSentinelStackDepth) where U : unmanaged, AllocatorManager.IAllocator
        {
            var nativelist = new NativeList<T>();
            nativelist.Initialize(initialCapacity, ref allocator, disposeSentinelStackDepth);
            return nativelist;
        }

        [BurstCompatible(GenericTypeArguments = new [] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static NativeList<T> New<U>(int initialCapacity, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
            return New(initialCapacity, ref allocator, 2);
        }

        NativeList(int initialCapacity, AllocatorManager.AllocatorHandle allocator, int disposeSentinelStackDepth)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp, disposeSentinelStackDepth);
        }

        /// <summary>
        /// The element at a given index.
        /// </summary>
        /// <param name="index">An index into this list.</param>
        /// <value>The value to store at the `index`.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return (*m_ListData)[index];
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                (*m_ListData)[index] = value;
            }
        }

        /// <summary>
        /// Returns a reference to the element at an index.
        /// </summary>
        /// <param name="index">An index.</param>
        /// <returns>A reference to the element at the index.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if index is out of bounds.</exception>
        public ref T ElementAt(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return ref m_ListData->ElementAt(index);
        }

        /// <summary>
        /// The count of elements.
        /// </summary>
        /// <value>The current count of elements. Always less than or equal to the capacity.</value>
        /// <remarks>To decrease the memory used by a list, set <see cref="Capacity"/> after reducing the length of the list.</remarks>
        /// <param name="value>">The new length. If the new length is greater than the current capacity, the capacity is increased.
        /// Newly allocated memory is cleared.</param>
        public int Length
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return CollectionHelper.AssumePositive(m_ListData->Length);
            }

            set
            {
                m_ListData->Resize(value, NativeArrayOptions.ClearMemory);
            }
        }

        /// <summary>
        /// The number of elements that fit in the current allocation.
        /// </summary>
        /// <value>The number of elements that fit in the current allocation.</value>
        /// <param name="value">The new capacity. Must be greater or equal to the length.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the new capacity is smaller than the length.</exception>
        public int Capacity
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_ListData->Capacity;
            }

            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                m_ListData->Capacity = value;
            }
        }

        /// <summary>
        /// Returns the internal unsafe list.
        /// </summary>
        /// <remarks>Internally, the elements of a NativeList are stored in an UnsafeList.</remarks>
        /// <returns>The internal unsafe list.</returns>
        public UnsafeList<T>* GetUnsafeList() => m_ListData;

        /// <summary>
        /// Appends an element to the end of this list.
        /// </summary>
        /// <param name="value">The value to add to the end of this list.</param>
        /// <remarks>
        /// Length is incremented by 1. Will not increase the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if incrementing the length would exceed the capacity.</exception>
        public void AddNoResize(T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_ListData->AddNoResize(value);
        }

        /// <summary>
        /// Appends elements from a buffer to the end of this list.
        /// </summary>
        /// <param name="ptr">The buffer to copy from.</param>
        /// <param name="count">The number of elements to copy from the buffer.</param>
        /// <remarks>
        /// Length is increased by the count. Will not increase the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRangeNoResize(void* ptr, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            CheckArgPositive(count);
            m_ListData->AddRangeNoResize(ptr, count);
        }

        /// <summary>
        /// Appends the elements of another list to the end of this list.
        /// </summary>
        /// <param name="list">The other list to copy from.</param>
        /// <remarks>
        /// Length is increased by the length of the other list. Will not increase the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRangeNoResize(NativeList<T> list)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            m_ListData->AddRangeNoResize(*list.m_ListData);
        }

        /// <summary>
        /// Appends an element to the end of this list.
        /// </summary>
        /// <param name="value">The value to add to the end of this list.</param>
        /// <remarks>
        /// Length is incremented by 1. If necessary, the capacity is increased.
        /// </remarks>
        public void Add(in T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->Add(value);
        }

        /// <summary>
        /// Appends the elements of an array to the end of this list.
        /// </summary>
        /// <param name="array">The array to copy from.</param>
        /// <remarks>
        /// Length is increased by the number of new elements. Does not increase the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRange(NativeArray<T> array)
        {
            AddRange(array.GetUnsafeReadOnlyPtr(), array.Length);
        }

        /// <summary>
        /// Appends the elements of a buffer to the end of this list.
        /// </summary>
        /// <param name="ptr">The buffer to copy from.</param>
        /// <param name="count">The number of elements to copy from the buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is negative.</exception>
        public void AddRange(void* ptr, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            CheckArgPositive(count);
            m_ListData->AddRange(ptr, CollectionHelper.AssumePositive(count));
        }

        /// <summary>
        /// Shifts elements toward the end of this list, increasing its length.
        /// </summary>
        /// <remarks>
        /// Right-shifts elements in the list so as to create 'free' slots at the beginning or in the middle.
        ///
        /// The length is increased by `end - begin`. If necessary, the capacity will be increased accordingly.
        ///
        /// If `end` equals `begin`, the method does nothing.
        ///
        /// The element at index `begin` will be copied to index `end`, the element at index `begin + 1` will be copied to `end + 1`, and so forth.
        ///
        /// The indexes `begin` up to `end` are not cleared: they will contain whatever values they held prior.
        /// </remarks>
        /// <param name="begin">The index of the first element that will be shifted up.</param>
        /// <param name="end">The index where the first shifted element will end up.</param>
        /// <exception cref="ArgumentException">Thrown if `end &lt; begin`.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        public void InsertRangeWithBeginEnd(int begin, int end)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->InsertRangeWithBeginEnd(CollectionHelper.AssumePositive(begin), CollectionHelper.AssumePositive(end));
        }

        /// <summary>
        /// Copies the last element of this list to the specified index. Decrements the length by 1.
        /// </summary>
        /// <remarks>Useful as a cheap way to remove an element from this list when you don't care about preserving order.</remarks>
        /// <param name="index">The index to overwrite with the last element.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAtSwapBack(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveAtSwapBack(CollectionHelper.AssumePositive(index));
        }

        /// <summary>
        /// Copies the last *N* elements of this list to a range in this list. Decrements the length by *N*.
        /// </summary>
        /// <remarks>
        /// Copies the last `count` elements to the indexes `index` up to `index + count`.
        ///
        /// Useful as a cheap way to remove elements from a list when you don't care about preserving order.
        /// </remarks>
        /// <param name="index">The index of the first element to overwrite.</param>
        /// <param name="count">The number of elements to copy and remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds, `count` is negative,
        /// or `index + count` exceeds the length.</exception>
        public void RemoveRangeSwapBack(int index, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRangeSwapBack(CollectionHelper.AssumePositive(index), CollectionHelper.AssumePositive(count));
        }

        /// <summary>
        /// Copies the last *N* elements of this list to a range in this list. Decrements the length by *N*.
        /// </summary>
        /// <remarks>
        /// Copies the last `end - begin` elements to the indexes `begin` up to `end`.
        ///
        /// Useful as a cheap way to remove elements from a list when you don't care about preserving order.
        ///
        /// Does nothing if `end - begin` is less than 1.
        /// </remarks>
        /// <param name="begin">The index of the first element to overwrite.</param>
        /// <param name="end">The index one greater than the last element to overwrite.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        [Obsolete("RemoveRangeSwapBackWithBeginEnd(begin, end) is deprecated, use RemoveRangeSwapBack(index, count) instead. (RemovedAfter 2021-06-02)", false)]
        public void RemoveRangeSwapBackWithBeginEnd(int begin, int end)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRangeSwapBackWithBeginEnd(CollectionHelper.AssumePositive(begin), CollectionHelper.AssumePositive(end));
        }

        /// <summary>
        /// Removes the element at an index, shifting everything above it down by one. Decrements the length by 1.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the elements, <see cref="RemoveAtSwapBack(int)"/> is a more efficient way to remove elements.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAt(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveAt(CollectionHelper.AssumePositive(index));
        }

        /// <summary>
        /// Removes *N* elements in a range, shifting everything above the range down by *N*. Decrements the length by *N*.
        /// </summary>
        /// <param name="index">The index of the first element to remove.</param>
        /// <param name="count">The number of elements to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the elements, <see cref="RemoveRangeSwapBackWithBeginEnd"/>
        /// is a more efficient way to remove elements.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds, `count` is negative,
        /// or `index + count` exceeds the length.</exception>
        public void RemoveRange(int index, int count)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRange(index, count);
        }

        /// <summary>
        /// Removes *N* elements in a range, shifting everything above it down by *N*. Decrements the length by *N*.
        /// </summary>
        /// <param name="begin">The index of the first element to remove.</param>
        /// <param name="end">The index one greater than the last element to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the elements, <see cref="RemoveRangeSwapBackWithBeginEnd"/> is a more efficient way to remove elements.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if `end &lt; begin`.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        [Obsolete("RemoveRangeWithBeginEnd(begin, end) is deprecated, use RemoveRange(index, count) instead. (RemovedAfter 2021-06-02)", false)]
        public void RemoveRangeWithBeginEnd(int begin, int end)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->RemoveRangeWithBeginEnd(begin, end);
        }

        /// <summary>
        /// Whether this list is empty.
        /// </summary>
        /// <value>True if the list is empty or if the list has not been constructed.</value>
        public bool IsEmpty => !IsCreated || Length == 0;

        /// <summary>
        /// Whether this list has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this list has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_ListData != null;

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_DeprecatedAllocator.TryRemoveSafetyHandle(m_Safety, m_SafetyIndexHint);
#if REMOVE_DISPOSE_SENTINEL
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#else
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
#endif
            UnsafeList<T>.Destroy(m_ListData);
            m_ListData = null;
        }

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// <typeparam name="U">The type of allocator.</typeparam>
        /// <param name="allocator">The allocator that was used to allocate this list.</param>
        /// </summary>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Dispose<U>(ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckHandleMatches(allocator.Handle);
            m_DeprecatedAllocator.TryRemoveSafetyHandle(m_Safety, m_SafetyIndexHint);
#if REMOVE_DISPOSE_SENTINEL
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#else
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
#endif
            UnsafeList<T>.Destroy(m_ListData, ref allocator);
            m_ListData = null;
        }

        /// <summary>
        /// Creates and schedules a job that releases all resources (memory and safety handles) of this list.
        /// </summary>
        /// <param name="inputDeps">The dependency for the new job.</param>
        /// <returns>The handle of the new job. The job depends upon `inputDeps` and releases all resources (memory and safety handles) of this list.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if REMOVE_DISPOSE_SENTINEL
#else
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif

            var jobHandle = new NativeListDisposeJob { Data = new NativeListDispose { m_ListData = (UntypedUnsafeList*)m_ListData, m_Safety = m_Safety } }.Schedule(inputDeps);

            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new NativeListDisposeJob { Data = new NativeListDispose { m_ListData = (UntypedUnsafeList*)m_ListData } }.Schedule(inputDeps);
#endif
            m_ListData = null;

            return jobHandle;
        }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_ListData->Clear();
        }

        /// <summary>
        /// Returns a native array that aliases the content of a list.
        /// </summary>
        /// <param name="nativeList">The list to alias.</param>
        /// <returns>A native array that aliases the content of the list.</returns>
        public static implicit operator NativeArray<T>(NativeList<T> nativeList)
        {
            return nativeList.AsArray();
        }

        /// <summary>
        /// Returns a native array that aliases the content of this list.
        /// </summary>
        /// <returns>A native array that aliases the content of this list.</returns>
        public NativeArray<T> AsArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_ListData->Ptr, m_ListData->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
            return array;
        }

        /// <summary>
        /// Returns an array that aliases this list. The length of the array is updated when the length of
        /// this array is updated in a prior job.
        /// </summary>
        /// <remarks>
        /// Useful when a job populates a list that is then used by another job.
        ///
        /// If you pass both jobs the same list, you have to complete the first job before you schedule the second:
        /// otherwise, the second job doesn't see the first job's changes to the list's length.
        ///
        /// If instead you pass the second job a deferred array that aliases the list, the array's length is kept in sync with
        /// the first job's changes to the list's length. Consequently, the first job doesn't have to
        /// be completed before you can schedule the second: the second job simply has to depend upon the first.
        /// </remarks>
        /// <returns>An array that aliases this list and whose length can be specially modified across jobs.</returns>
        /// <example>
        /// The following example populates a list with integers in one job and passes that data to a second job as
        /// a deferred array. If we tried to pass the list directly to the second job, that job would not see any
        /// modifications made to the list by the first job. To avoid this, we instead pass the second job a deferred array that aliases the list.
        /// <code>
        /// using UnityEngine;
        /// using Unity.Jobs;
        /// using Unity.Collections;
        ///
        /// public class DeferredArraySum : MonoBehaviour
        ///{
        ///    public struct Populate : IJob
        ///    {
        ///        public NativeList&lt;int&gt; list;
        ///
        ///        public void Execute()
        ///        {
        ///            for (int i = list.Length; i &lt; list.Capacity; i++)
        ///            {
        ///                list.Add(i);
        ///            }
        ///        }
        ///    }
        ///
        ///    // Sums all numbers from deferred.
        ///    public struct Sum : IJob
        ///    {
        ///        [ReadOnly] public NativeArray&lt;int&gt; deferred;
        ///        public NativeArray&lt;int&gt; sum;
        ///
        ///        public void Execute()
        ///        {
        ///            sum[0] = 0;
        ///            for (int i = 0; i &lt; deferred.Length; i++)
        ///            {
        ///                sum[0] += deferred[i];
        ///            }
        ///        }
        ///    }
        ///
        ///    void Start()
        ///    {
        ///        var list = new NativeList&lt;int&gt;(100, Allocator.TempJob);
        ///        var deferred = list.AsDeferredJobArray(),
        ///        var output = new NativeArray&lt;int&gt;(1, Allocator.TempJob);
        ///
        ///        // The Populate job increases the list's length from 0 to 100.
        ///        var populate = new Populate { list = list }.Schedule();
        ///
        ///        // At time of scheduling, the length of the deferred array given to Sum is 0.
        ///        // When Populate increases the list's length, the deferred array's length field in the
        ///        // Sum job is also modified, even though it has already been scheduled.
        ///        var sum = new Sum { deferred = deferred, sum = output }.Schedule(populate);
        ///
        ///        sum.Complete();
        ///
        ///        Debug.Log("Result: " + output[0]);
        ///
        ///        list.Dispose();
        ///        output.Dispose();
        ///    }
        /// }
        /// </code>
        /// </example>
        public NativeArray<T> AsDeferredJobArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif
            byte* buffer = (byte*)m_ListData;
            // We use the first bit of the pointer to infer that the array is in list mode
            // Thus the job scheduling code will need to patch it.
            buffer += 1;
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(buffer, 0, Allocator.Invalid);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif

            return array;
        }

        /// <summary>
        /// Returns a managed array containing a copy of this list's content.
        /// </summary>
        /// <returns>A NativeArray containing copies of all the items in the list.</returns>
        [NotBurstCompatible /* Deprecated */]
        // todo: reenable deprecation warning before release [see: DOTS-DOTS-4217]
        // [Obsolete("Please use `ToArrayNBC` from `Unity.Collections.NotBurstCompatible` namespace instead. (RemovedAfter 2021-06-22)", false)]
        public T[] ToArray() => NotBurstCompatible.Extensions.ToArrayNBC(this);

        /// <summary>
        /// Returns an array containing a copy of this list's content.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array containing a copy of this list's content.</returns>
        public NativeArray<T> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            NativeArray<T> result = CollectionHelper.CreateNativeArray<T>(Length, allocator, NativeArrayOptions.UninitializedMemory);
            result.CopyFrom(this);
            return result;
        }

        /// <summary>
        /// Returns an enumerator over the elements of this list.
        /// </summary>
        /// <returns>An enumerator over the elements of this list.</returns>
        public NativeArray<T>.Enumerator GetEnumerator()
        {
            var array = AsArray();
            return new NativeArray<T>.Enumerator(ref array);
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Overwrites the elements of this list with the elements of an equal-length array.
        /// </summary>
        /// <param name="array">An array to copy into this list.</param>
        /// <exception cref="ArgumentException">Thrown if the array and list have unequal length.</exception>
        [NotBurstCompatible /* Deprecated */]
        [Obsolete("Please use `CopyFromNBC` from `Unity.Collections.NotBurstCompatible` namespace instead. (RemovedAfter 2021-06-22)", false)]
        public void CopyFrom(T[] array) => NotBurstCompatible.Extensions.CopyFromNBC(this, array);

        /// <summary>
        /// Overwrites the elements of this list with the elements of an equal-length array.
        /// </summary>
        /// <param name="array">An array to copy into this list.</param>
        /// <exception cref="ArgumentException">Thrown if the array and list have unequal length.</exception>
        public void CopyFrom(NativeArray<T> array)
        {
            Clear();
            Resize(array.Length, NativeArrayOptions.UninitializedMemory);
            NativeArray<T> thisArray = AsArray();
            thisArray.CopyFrom(array);
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary.
        /// </summary>
        /// <param name="length">The new length of this list.</param>
        /// <param name="options">Whether to clear any newly allocated bytes to all zeroes.</param>
        public void Resize(int length, NativeArrayOptions options)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            m_ListData->Resize(length, options);
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary.
        /// </summary>
        /// <remarks>Does not clear newly allocated bytes.</remarks>
        /// <param name="length">The new length of this list.</param>
        public void ResizeUninitialized(int length)
        {
            Resize(length, NativeArrayOptions.UninitializedMemory);
        }

        /// <summary>
        /// Sets the capacity.
        /// </summary>
        /// <param name="capacity">The new capacity.</param>
        public void SetCapacity(int capacity)
        {
            m_ListData->SetCapacity(capacity);
        }

        /// <summary>
        /// Sets the capacity to match the length.
        /// </summary>
        public void TrimExcess()
        {
            m_ListData->TrimExcess();
        }

        /// <summary>
        /// Returns a parallel reader of this list.
        /// </summary>
        /// <returns>A parallel reader of this list.</returns>
        public NativeArray<T>.ReadOnly AsParallelReader()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new NativeArray<T>.ReadOnly(m_ListData->Ptr, m_ListData->Length, ref m_Safety);
#else
            return new NativeArray<T>.ReadOnly(m_ListData->Ptr, m_ListData->Length);
#endif
        }

        /// <summary>
        /// Returns a parallel writer of this list.
        /// </summary>
        /// <returns>A parallel writer of this list.</returns>
        public ParallelWriter AsParallelWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new ParallelWriter(m_ListData, ref m_Safety);
#else
            return new ParallelWriter(m_ListData);
#endif
        }

        /// <summary>
        /// A parallel writer for a NativeList.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a list.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public unsafe struct ParallelWriter
        {
            /// <summary>
            /// The data of the list.
            /// </summary>
            public readonly void* Ptr => ListData->Ptr;

            /// <summary>
            /// The internal unsafe list.
            /// </summary>
            /// <value>The internal unsafe list.</value>
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<T>* ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();

            [BurstCompatible(CompileTarget = BurstCompatibleAttribute.BurstCompatibleCompileTarget.Editor)]
            internal unsafe ParallelWriter(UnsafeList<T>* listData, ref AtomicSafetyHandle safety)
            {
                ListData = listData;
                m_Safety = safety;
                CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref m_Safety, ref s_staticSafetyId.Data);
            }
#else
            internal unsafe ParallelWriter(UnsafeList<T>* listData)
            {
                ListData = listData;
            }
#endif

            /// <summary>
            /// Appends an element to the end of this list.
            /// </summary>
            /// <param name="value">The value to add to the end of this list.</param>
            /// <remarks>
            /// Increments the length by 1 unless doing so would exceed the current capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if adding an element would exceed the capacity.</exception>
            public void AddNoResize(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Increment(ref ListData->m_length) - 1;
                CheckSufficientCapacity(ListData->Capacity, idx + 1);

                UnsafeUtility.WriteArrayElement(ListData->Ptr, idx, value);
            }

            /// <summary>
            /// Appends elements from a buffer to the end of this list.
            /// </summary>
            /// <param name="ptr">The buffer to copy from.</param>
            /// <param name="count">The number of elements to copy from the buffer.</param>
            /// <remarks>
            /// Increments the length by `count` unless doing so would exceed the current capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if adding the elements would exceed the capacity.</exception>
            public void AddRangeNoResize(void* ptr, int count)
            {
                CheckArgPositive(count);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Add(ref ListData->m_length, count) - count;
                CheckSufficientCapacity(ListData->Capacity, idx + count);

                var sizeOf = sizeof(T);
                void* dst = (byte*)ListData->Ptr + idx * sizeOf;
                UnsafeUtility.MemCpy(dst, ptr, count * sizeOf);
            }

            /// <summary>
            /// Appends the elements of another list to the end of this list.
            /// </summary>
            /// <param name="list">The other list to copy from.</param>
            /// <remarks>
            /// Increments the length of this list by the length of the other list unless doing so would exceed the current capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if adding the elements would exceed the capacity.</exception>
            public void AddRangeNoResize(UnsafeList<T> list)
            {
                AddRangeNoResize(list.Ptr, list.Length);
            }

            /// <summary>
            /// Appends the elements of another list to the end of this list.
            /// </summary>
            /// <param name="list">The other list to copy from.</param>
            /// <remarks>
            /// Increments the length of this list by the length of the other list unless doing so would exceed the current capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if adding the elements would exceed the capacity.</exception>
            public void AddRangeNoResize(NativeList<T> list)
            {
                AddRangeNoResize(*list.m_ListData);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckTotalSize(int initialCapacity, long totalSize)
        {
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckSufficientCapacity(int capacity, int length)
        {
            if (capacity < length)
                throw new Exception($"Length {length} exceeds capacity Capacity {capacity}");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckArgPositive(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckHandleMatches(AllocatorManager.AllocatorHandle handle)
        {
            if(m_ListData == null)
                throw new ArgumentOutOfRangeException($"Allocator handle {handle} can't match because container is not initialized.");
            if(m_ListData->Allocator.Index != handle.Index)
                throw new ArgumentOutOfRangeException($"Allocator handle {handle} can't match because container handle index doesn't match.");
            if(m_ListData->Allocator.Version != handle.Version)
                throw new ArgumentOutOfRangeException($"Allocator handle {handle} matches container handle index, but has different version.");
        }
    }

    [NativeContainer]
    [BurstCompatible]
    internal unsafe struct NativeListDispose
    {
        [NativeDisableUnsafePtrRestriction]
        public UntypedUnsafeList* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public void Dispose()
        {
            var listData = (UnsafeList<int>*)m_ListData;
            UnsafeList<int>.Destroy(listData);
        }
    }

    [BurstCompile]
    [BurstCompatible]
    internal unsafe struct NativeListDisposeJob : IJob
    {
        internal NativeListDispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }

    sealed class NativeListDebugView<T> where T : unmanaged
    {
        NativeList<T> m_Array;

        public NativeListDebugView(NativeList<T> array)
        {
            m_Array = array;
        }

        public T[] Items => m_Array.AsArray().ToArray();
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// Provides unsafe utility methods for NativeList.
    /// </summary>
    [BurstCompatible]
    public unsafe static class NativeListUnsafeUtility
    {
        /// <summary>
        /// Returns a pointer to this list's internal buffer.
        /// </summary>
        /// <remarks>Performs a job safety check for read-write access.</remarks>
        /// <param name="list">The list.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <returns>A pointer to this list's internal buffer.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static void* GetUnsafePtr<T>(this NativeList<T> list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

        /// <summary>
        /// Returns a pointer to this list's internal buffer.
        /// </summary>
        /// <remarks>Performs a job safety check for read-only access.</remarks>
        /// <param name="list">The list.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <returns>A pointer to this list's internal buffer.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static unsafe void* GetUnsafeReadOnlyPtr<T>(this NativeList<T> list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Returns this list's <see cref="AtomicSafetyHandle"/>.
        /// </summary>
        /// <param name="list">The list.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <returns>The atomic safety handle for this list.</returns>
        /// <remarks>
        /// The job safety checks use a native collection's atomic safety handle to assert safety.
        ///
        /// This method is only available if the symbol `ENABLE_UNITY_COLLECTIONS_CHECKS` is defined.</remarks>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) }, RequiredUnityDefine = "ENABLE_UNITY_COLLECTIONS_CHECKS", CompileTarget = BurstCompatibleAttribute.BurstCompatibleCompileTarget.Editor)]
        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(ref NativeList<T> list) where T : unmanaged
        {
            return list.m_Safety;
        }

#endif

        /// <summary>
        /// Returns a pointer to this list's internal unsafe list.
        /// </summary>
        /// <remarks>Performs no job safety checks.</remarks>
        /// <param name="list">The list.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <returns>A pointer to this list's internal unsafe list.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static void* GetInternalListDataPtrUnchecked<T>(ref NativeList<T> list) where T : unmanaged
        {
            return list.m_ListData;
        }
    }
}
