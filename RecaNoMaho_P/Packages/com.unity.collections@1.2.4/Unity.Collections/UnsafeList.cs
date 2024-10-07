using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe
{

    [BurstCompile]
    internal unsafe struct UnsafeDisposeJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public void* Ptr;
        public AllocatorManager.AllocatorHandle Allocator;

        public void Execute()
        {
            AllocatorManager.Free(Allocator, Ptr);
        }
    }

    internal unsafe struct UntypedUnsafeList
    {
#pragma warning disable 169
        [NativeDisableUnsafePtrRestriction]
        public void* Ptr;
        public int m_length;
        public int m_capacity;
        public AllocatorManager.AllocatorHandle Allocator;
        internal int obsolete_length;
        internal int obsolete_capacity;
#pragma warning restore 169
    }

    /// <summary>
    /// An unmanaged, resizable list.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}, IsEmpty = {IsEmpty}")]
    [DebuggerTypeProxy(typeof(UnsafeListTDebugView<>))]
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafeList<T>
        : INativeDisposable
        , INativeList<T>
        , IEnumerable<T> // Used by collection initializers.
        where T : unmanaged
    {
        // <WARNING>
        // 'Header' of this struct must binary match 'UntypedUnsafeList' struct
        // Fields must match UntypedUnsafeList structure, please don't reorder and don't insert anything in between first 4 fields

        /// <summary>
        /// The internal buffer of this list.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public T* Ptr;

        /// <summary>
        /// The number of elements.
        /// </summary>
        public int m_length;

        /// <summary>
        /// The number of elements that can fit in the internal buffer.
        /// </summary>
        public int m_capacity;

        /// <summary>
        /// The allocator used to create the internal buffer.
        /// </summary>
        public AllocatorManager.AllocatorHandle Allocator;

        [Obsolete("Use Length property (UnityUpgradable) -> Length", true)]
        public int length;

        [Obsolete("Use Capacity property (UnityUpgradable) -> Capacity", true)]
        public int capacity;

        /// <summary>
        /// The number of elements.
        /// </summary>
        /// <value>The number of elements.</value>
        public int Length
        {
            get
            {
                return CollectionHelper.AssumePositive(m_length);
            }

            set
            {
                if (value > Capacity)
                {
                    Resize(value);
                }
                else
                {
                    m_length = value;
                }
            }
        }

        /// <summary>
        /// The number of elements that can fit in the internal buffer.
        /// </summary>
        /// <value>The number of elements that can fit in the internal buffer.</value>
        public int Capacity
        {
            get
            {
                return CollectionHelper.AssumePositive(m_capacity);
            }

            set
            {
                SetCapacity(value);
            }
        }

        /// <summary>
        /// The element at an index.
        /// </summary>
        /// <param name="index">An index.</param>
        /// <value>The element at the index.</value>
        public T this[int index]
        {
            get
            {
                CollectionHelper.CheckIndexInRange(index, Length);
                return Ptr[CollectionHelper.AssumePositive(index)];
            }

            set
            {
                CollectionHelper.CheckIndexInRange(index, Length);
                Ptr[CollectionHelper.AssumePositive(index)] = value;
            }
        }

        /// <summary>
        /// Returns a reference to the element at a given index.
        /// </summary>
        /// <param name="index">The index to access. Must be in the range of [0..Length).</param>
        /// <returns>A reference to the element at the index.</returns>
        public ref T ElementAt(int index)
        {
            CollectionHelper.CheckIndexInRange(index, Length);
            return ref Ptr[CollectionHelper.AssumePositive(index)];
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafeList.
        /// </summary>
        /// <param name="ptr">An existing byte array to set as the internal buffer.</param>
        /// <param name="length">The length.</param>
        public UnsafeList(T* ptr, int length) : this()
        {
            Ptr = ptr;
            this.m_length = length;
            m_capacity = 0;
            Allocator = AllocatorManager.None;
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafeList.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public UnsafeList(int initialCapacity, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) : this()
        {
            Ptr = null;
            m_length = 0;
            m_capacity = 0;
            Allocator = allocator;

            if (initialCapacity != 0)
            {
                SetCapacity(initialCapacity);
            }

            if (options == NativeArrayOptions.ClearMemory && Ptr != null)
            {
                var sizeOf = sizeof(T);
                UnsafeUtility.MemClear(Ptr, Capacity * sizeOf);
            }
        }

        [BurstCompatible(GenericTypeArguments = new [] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Initialize<U>(int initialCapacity, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where U : unmanaged, AllocatorManager.IAllocator
        {
            Ptr = null;
            m_length = 0;
            m_capacity = 0;
            Allocator = AllocatorManager.None;
            Initialize(initialCapacity, ref allocator, options);
        }

        [BurstCompatible(GenericTypeArguments = new [] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeList<T> New<U>(int initialCapacity, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where U : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeList<T> instance = default;
            instance.Initialize(initialCapacity, ref allocator, options);
            return instance;
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeList<T>* Create<U>(int initialCapacity, ref U allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where U : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeList<T>* listData = allocator.Allocate(default(UnsafeList<T>), 1);
            UnsafeUtility.MemClear(listData, sizeof(UnsafeList<T>));

            listData->Allocator = allocator.Handle;

            if (initialCapacity != 0)
            {
                listData->SetCapacity(ref allocator, initialCapacity);
            }

            if (options == NativeArrayOptions.ClearMemory
                && listData->Ptr != null)
            {
                var sizeOf = sizeof(T);
                UnsafeUtility.MemClear(listData->Ptr, listData->Capacity * sizeOf);
            }

            return listData;
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static void Destroy<U>(UnsafeList<T>* listData, ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
            CheckNull(listData);
            listData->Dispose(ref allocator);
            allocator.Free(listData, sizeof(UnsafeList<T>), UnsafeUtility.AlignOf<UnsafeList<T>>(), 1);
        }

        /// <summary>
        /// Returns a new list.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        /// <returns>A pointer to the new list.</returns>
        public static UnsafeList<T>* Create(int initialCapacity, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            UnsafeList<T>* listData = AllocatorManager.Allocate<UnsafeList<T>>(allocator);
            *listData = new UnsafeList<T>(initialCapacity, allocator, options);

            return listData;
        }

        /// <summary>
        /// Destroys the list.
        /// </summary>
        /// <param name="listData">The list to destroy.</param>
        public static void Destroy(UnsafeList<T>* listData)
        {
            CheckNull(listData);
            var allocator = listData->Allocator;
            listData->Dispose();
            AllocatorManager.Free(allocator, listData);
        }

        /// <summary>
        /// Whether the list is empty.
        /// </summary>
        /// <value>True if the list is empty or the list has not been constructed.</value>
        public bool IsEmpty => !IsCreated || m_length == 0;

        /// <summary>
        /// Whether this list has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this list has been allocated (and not yet deallocated).</value>
        public bool IsCreated => Ptr != null;

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Dispose<U>(ref U allocator) where U : unmanaged, AllocatorManager.IAllocator
        {
            allocator.Free(Ptr, m_length);
            Ptr = null;
            m_length = 0;
            m_capacity = 0;
        }

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            if (CollectionHelper.ShouldDeallocate(Allocator))
            {
                AllocatorManager.Free(Allocator, Ptr);
                Allocator = AllocatorManager.Invalid;
            }

            Ptr = null;
            m_length = 0;
            m_capacity = 0;
        }

        /// <summary>
        /// Creates and schedules a job that frees the memory of this list.
        /// </summary>
        /// <param name="inputDeps">The dependency for the new job.</param>
        /// <returns>The handle of the new job. The job depends upon `inputDeps` and frees the memory of this list.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (CollectionHelper.ShouldDeallocate(Allocator))
            {
                var jobHandle = new UnsafeDisposeJob { Ptr = Ptr, Allocator = Allocator }.Schedule(inputDeps);

                Ptr = null;
                Allocator = AllocatorManager.Invalid;

                return jobHandle;
            }

            Ptr = null;

            return inputDeps;
        }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            m_length = 0;
        }

        /// <summary>
        /// Sets the length, expanding the capacity if necessary.
        /// </summary>
        /// <param name="length">The new length.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public void Resize(int length, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            var oldLength = m_length;

            if (length > Capacity)
            {
                SetCapacity(length);
            }

            m_length = length;

            if (options == NativeArrayOptions.ClearMemory && oldLength < length)
            {
                var num = length - oldLength;
                byte* ptr = (byte*)Ptr;
                var sizeOf = sizeof(T);
                UnsafeUtility.MemClear(ptr + oldLength * sizeOf, num * sizeOf);
            }
        }

        void Realloc<U>(ref U allocator, int newCapacity) where U : unmanaged, AllocatorManager.IAllocator
        {
            CollectionHelper.CheckAllocator(Allocator);
            T* newPointer = null;

            var alignOf = UnsafeUtility.AlignOf<T>();
            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                newPointer = (T*)allocator.Allocate(sizeOf, alignOf, newCapacity);

                if (m_capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, Capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, Ptr, bytesToCopy);
                }
            }

            allocator.Free(Ptr, Capacity);

            Ptr = newPointer;
            m_capacity = newCapacity;
            m_length = math.min(m_length, newCapacity);
        }

        void Realloc(int capacity)
        {
            Realloc(ref Allocator, capacity);
        }

        void SetCapacity<U>(ref U allocator, int capacity) where U : unmanaged, AllocatorManager.IAllocator
        {
            CollectionHelper.CheckCapacityInRange(capacity, Length);

            var sizeOf = sizeof(T);
            var newCapacity = math.max(capacity, 64 / sizeOf);
            newCapacity = math.ceilpow2(newCapacity);

            if (newCapacity == Capacity)
            {
                return;
            }

            Realloc(ref allocator, newCapacity);
        }

        /// <summary>
        /// Sets the capacity.
        /// </summary>
        /// <param name="capacity">The new capacity.</param>
        public void SetCapacity(int capacity)
        {
            SetCapacity(ref Allocator, capacity);
        }

        /// <summary>
        /// Sets the capacity to match the length.
        /// </summary>
        public void TrimExcess()
        {
            if (Capacity != m_length)
            {
                Realloc(m_length);
            }
        }

        /// <summary>
        /// Adds an element to the end of this list.
        /// </summary>
        /// <remarks>
        /// Increments the length by 1. Never increases the capacity.
        /// </remarks>
        /// <param name="value">The value to add to the end of the list.</param>
        /// <exception cref="Exception">Thrown if incrementing the length would exceed the capacity.</exception>
        public void AddNoResize(T value)
        {
            CheckNoResizeHasEnoughCapacity(1);
            UnsafeUtility.WriteArrayElement(Ptr, m_length, value);
            m_length += 1;
        }

        /// <summary>
        /// Copies elements from a buffer to the end of this list.
        /// </summary>
        /// <remarks>
        /// Increments the length by `count`. Never increases the capacity.
        /// </remarks>
        /// <param name="ptr">The buffer to copy from.</param>
        /// <param name="count">The number of elements to copy from the buffer.</param>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRangeNoResize(void* ptr, int count)
        {
            CheckNoResizeHasEnoughCapacity(count);
            var sizeOf = sizeof(T);
            void* dst = (byte*)Ptr + m_length * sizeOf;
            UnsafeUtility.MemCpy(dst, ptr, count * sizeOf);
            m_length += count;
        }

        /// <summary>
        /// Copies the elements of another list to the end of this list.
        /// </summary>
        /// <param name="list">The other list to copy from.</param>
        /// <remarks>
        /// Increments the length by the length of the other list. Never increases the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public void AddRangeNoResize(UnsafeList<T> list)
        {
            AddRangeNoResize(list.Ptr, CollectionHelper.AssumePositive(list.m_length));
        }

        /// <summary>
        /// Adds an element to the end of the list.
        /// </summary>
        /// <param name="value">The value to add to the end of this list.</param>
        /// <remarks>
        /// Increments the length by 1. Increases the capacity if necessary.
        /// </remarks>
        public void Add(in T value)
        {
            var idx = m_length;

            if (m_length + 1 > Capacity)
            {
                Resize(idx + 1);
            }
            else
            {
                m_length += 1;
            }

            UnsafeUtility.WriteArrayElement(Ptr, idx, value);
        }

        /// <summary>
        /// Copies the elements of a buffer to the end of this list.
        /// </summary>
        /// <param name="ptr">The buffer to copy from.</param>
        /// <param name="count">The number of elements to copy from the buffer.</param>
        /// <remarks>
        /// Increments the length by `count`. Increases the capacity if necessary.
        /// </remarks>
        public void AddRange(void* ptr, int count)
        {
            var idx = m_length;

            if (m_length + count > Capacity)
            {
                Resize(m_length + count);
            }
            else
            {
                m_length += count;
            }

            var sizeOf = sizeof(T);
            void* dst = (byte*)Ptr + idx * sizeOf;
            UnsafeUtility.MemCpy(dst, ptr, count * sizeOf);
        }

        /// <summary>
        /// Copies the elements of another list to the end of the list.
        /// </summary>
        /// <param name="list">The list to copy from.</param>
        /// <remarks>
        /// The length is increased by the length of the other list. Increases the capacity if necessary.
        /// </remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public void AddRange(UnsafeList<T> list)
        {
            AddRange(list.Ptr, list.Length);
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
            CheckBeginEnd(begin, end);

            int items = end - begin;
            if (items < 1)
            {
                return;
            }

            var oldLength = m_length;

            if (m_length + items > Capacity)
            {
                Resize(m_length + items);
            }
            else
            {
                m_length += items;
            }

            var itemsToCopy = oldLength - begin;

            if (itemsToCopy < 1)
            {
                return;
            }

            var sizeOf = sizeof(T);
            var bytesToCopy = itemsToCopy * sizeOf;
            unsafe
            {
                byte* ptr = (byte*)Ptr;
                byte* dest = ptr + end * sizeOf;
                byte* src = ptr + begin * sizeOf;
                UnsafeUtility.MemMove(dest, src, bytesToCopy);
            }
        }

        /// <summary>
        /// Copies the last element of this list to the specified index. Decrements the length by 1.
        /// </summary>
        /// <remarks>Useful as a cheap way to remove an element from this list when you don't care about preserving order.</remarks>
        /// <param name="index">The index to overwrite with the last element.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAtSwapBack(int index)
        {
            RemoveRangeSwapBack(index, 1);
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
            CheckIndexCount(index, count);

            if (count > 0)
            {
                int copyFrom = math.max(m_length - count, index + count);
                var sizeOf = sizeof(T);
                void* dst = (byte*)Ptr + index * sizeOf;
                void* src = (byte*)Ptr + copyFrom * sizeOf;
                UnsafeUtility.MemCpy(dst, src, (m_length - copyFrom) * sizeOf);
                m_length -= count;
            }
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
            CheckBeginEnd(begin, end);

            int itemsToRemove = end - begin;
            if (itemsToRemove > 0)
            {
                int copyFrom = math.max(m_length - itemsToRemove, end);
                var sizeOf = sizeof(T);
                void* dst = (byte*)Ptr + begin * sizeOf;
                void* src = (byte*)Ptr + copyFrom * sizeOf;
                UnsafeUtility.MemCpy(dst, src, (m_length - copyFrom) * sizeOf);
                m_length -= itemsToRemove;
            }
        }

        /// <summary>
        /// Removes the element at an index, shifting everything above it down by one. Decrements the length by 1.
        /// </summary>
        /// <param name="index">The index of the element to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the elements, <see cref="RemoveAtSwapBack(int)"/> is a more efficient way to remove elements.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAt(int index)
        {
            RemoveRange(index, 1);
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
            CheckIndexCount(index, count);

            if (count > 0)
            {
                int copyFrom = math.min(index + count, m_length);
                var sizeOf = sizeof(T);
                void* dst = (byte*)Ptr + index * sizeOf;
                void* src = (byte*)Ptr + copyFrom * sizeOf;
                UnsafeUtility.MemCpy(dst, src, (m_length - copyFrom) * sizeOf);
                m_length -= count;
            }
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
            CheckBeginEnd(begin, end);

            int itemsToRemove = end - begin;
            if (itemsToRemove > 0)
            {
                int copyFrom = math.min(begin + itemsToRemove, m_length);
                var sizeOf = sizeof(T);
                void* dst = (byte*)Ptr + begin * sizeOf;
                void* src = (byte*)Ptr + copyFrom * sizeOf;
                UnsafeUtility.MemCpy(dst, src, (m_length - copyFrom) * sizeOf);
                m_length -= itemsToRemove;
            }
        }

        /// <summary>
        /// Returns a parallel reader of this list.
        /// </summary>
        /// <returns>A parallel reader of this list.</returns>
        public ParallelReader AsParallelReader()
        {
            return new ParallelReader(Ptr, Length);
        }

        /// <summary>
        /// A parallel reader for an UnsafeList&lt;T&gt;.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelReader"/> to create a parallel reader for a list.
        /// </remarks>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public unsafe struct ParallelReader
        {
            /// <summary>
            /// The internal buffer of the list.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public readonly T* Ptr;

            /// <summary>
            /// The number of elements.
            /// </summary>
            public readonly int Length;

            internal ParallelReader(T* ptr, int length)
            {
                Ptr = ptr;
                Length = length;
            }
        }

        /// <summary>
        /// Returns a parallel writer of this list.
        /// </summary>
        /// <returns>A parallel writer of this list.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter((UnsafeList<T>*)UnsafeUtility.AddressOf(ref this));
        }

        /// <summary>
        /// A parallel writer for an UnsafeList&lt;T&gt;.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a list.
        /// </remarks>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public unsafe struct ParallelWriter
        {
            /// <summary>
            /// The data of the list.
            /// </summary>
            public readonly void* Ptr => ListData->Ptr;

            /// <summary>
            /// The UnsafeList to write to.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<T>* ListData;

            internal unsafe ParallelWriter(UnsafeList<T>* listData)
            {
                ListData = listData;
            }

            /// <summary>
            /// Adds an element to the end of the list.
            /// </summary>
            /// <param name="value">The value to add to the end of the list.</param>
            /// <remarks>
            /// Increments the length by 1. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if incrementing the length would exceed the capacity.</exception>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void AddNoResize(T value)
            {
                var idx = Interlocked.Increment(ref ListData->m_length) - 1;
                ListData->CheckNoResizeHasEnoughCapacity(idx, 1);
                UnsafeUtility.WriteArrayElement(ListData->Ptr, idx, value);
            }

            /// <summary>
            /// Copies elements from a buffer to the end of the list.
            /// </summary>
            /// <param name="ptr">The buffer to copy from.</param>
            /// <param name="count">The number of elements to copy from the buffer.</param>
            /// <remarks>
            /// Increments the length by `count`. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void AddRangeNoResize(void* ptr, int count)
            {
                var idx = Interlocked.Add(ref ListData->m_length, count) - count;
                ListData->CheckNoResizeHasEnoughCapacity(idx, count);
                void* dst = (byte*)ListData->Ptr + idx * sizeof(T);
                UnsafeUtility.MemCpy(dst, ptr, count * sizeof(T));
            }

            /// <summary>
            /// Copies the elements of another list to the end of this list.
            /// </summary>
            /// <param name="list">The other list to copy from.</param>
            /// <remarks>
            /// Increments the length by the length of the other list. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void AddRangeNoResize(UnsafeList<T> list)
            {
                AddRangeNoResize(list.Ptr, list.Length);
            }
        }

        /// <summary>
        /// Overwrites the elements of this list with the elements of an equal-length array.
        /// </summary>
        /// <param name="array">An array to copy into this list.</param>
        public void CopyFrom(UnsafeList<T> array)
        {
            Resize(array.Length);
            UnsafeUtility.MemCpy(Ptr, array.Ptr, UnsafeUtility.SizeOf<T>() * Length);
        }


        /// <summary>
        /// Returns an enumerator over the elements of the list.
        /// </summary>
        /// <returns>An enumerator over the elements of the list.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator { m_Ptr = Ptr, m_Length = Length, m_Index = -1 };
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
        /// An enumerator over the elements of a list.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is invalid.
        /// The first <see cref="MoveNext"/> call advances the enumerator to the first element of the list.
        /// </remarks>
        public struct Enumerator : IEnumerator<T>
        {
            internal T* m_Ptr;
            internal int m_Length;
            internal int m_Index;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next element of the list.
            /// </summary>
            /// <remarks>
            /// The first `MoveNext` call advances the enumerator to the first element of the list. Before this call, `Current` is not valid to read.
            /// </remarks>
            /// <returns>True if `Current` is valid to read after the call.</returns>
            public bool MoveNext() => ++m_Index < m_Length;

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => m_Index = -1;

            /// <summary>
            /// The current element.
            /// </summary>
            /// <value>The current element.</value>
            public T Current => m_Ptr[m_Index];

            object IEnumerator.Current => Current;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckNull(void* listData)
        {
            if (listData == null)
            {
                throw new Exception("UnsafeList has yet to be created or has been destroyed!");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckIndexCount(int index, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException($"Value for cound {count} must be positive.");
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException($"Value for index {index} must be positive.");
            }

            if (index > Length)
            {
                throw new ArgumentOutOfRangeException($"Value for index {index} is out of bounds.");
            }

            if (index+count > Length)
            {
                throw new ArgumentOutOfRangeException($"Value for count {count} is out of bounds.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckBeginEnd(int begin, int end)
        {
            if (begin > end)
            {
                throw new ArgumentException($"Value for begin {begin} index must less or equal to end {end}.");
            }

            if (begin < 0)
            {
                throw new ArgumentOutOfRangeException($"Value for begin {begin} must be positive.");
            }

            if (begin > Length)
            {
                throw new ArgumentOutOfRangeException($"Value for begin {begin} is out of bounds.");
            }

            if (end > Length)
            {
                throw new ArgumentOutOfRangeException($"Value for end {end} is out of bounds.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckNoResizeHasEnoughCapacity(int length)
        {
            CheckNoResizeHasEnoughCapacity(length, Length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckNoResizeHasEnoughCapacity(int length, int index)
        {
            if (Capacity < index + length)
            {
                throw new Exception($"AddNoResize assumes that list capacity is sufficient (Capacity {Capacity}, Length {Length}), requested length {length}!");
            }
        }
    }

    /// <summary>
    /// Provides extension methods for UnsafeList.
    /// </summary>
    [BurstCompatible]
    public unsafe static class UnsafeListExtensions
    {
        /// <summary>
        /// Finds the index of the first occurrence of a particular value in this list.
        /// </summary>
        /// <typeparam name="T">The type of elements in this list.</typeparam>
        /// <typeparam name="U">The type of value to locate.</typeparam>
        /// <param name="list">This list.</param>
        /// <param name="value">A value to locate.</param>
        /// <returns>The zero-based index of the first occurrence of the value if it is found. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this UnsafeList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return NativeArrayExtensions.IndexOf<T, U>(list.Ptr, list.Length, value);
        }

        /// <summary>
        /// Returns true if a particular value is present in this list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <typeparam name="U">The type of value to locate.</typeparam>
        /// <param name="list">This list.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in this list.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this UnsafeList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return list.IndexOf(value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in the list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <typeparam name="U">The type of value to locate.</typeparam>
        /// <param name="list">This reader of the list.</param>
        /// <param name="value">A value to locate.</param>
        /// <returns>The zero-based index of the first occurrence of the value if it is found. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this UnsafeList<T>.ParallelReader list, U value) where T : unmanaged, IEquatable<U>
        {
            return NativeArrayExtensions.IndexOf<T, U>(list.Ptr, list.Length, value);
        }

        /// <summary>
        /// Returns true if a particular value is present in the list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <typeparam name="U">The type of value to locate.</typeparam>
        /// <param name="list">This reader of the list.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in the list.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this UnsafeList<T>.ParallelReader list, U value) where T : unmanaged, IEquatable<U>
        {
            return list.IndexOf(value) != -1;
        }


        /// <summary>
        /// Returns true if this array and another have equal length and content.
        /// </summary>
        /// <typeparam name="T">The type of the source array's elements.</typeparam>
        /// <param name="array">The array to compare for equality.</param>
        /// <param name="other">The other array to compare for equality.</param>
        /// <returns>True if the arrays have equal length and content.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static bool ArraysEqual<T>(this UnsafeList<T> array, UnsafeList<T> other) where T : unmanaged, IEquatable<T>
        {
            if (array.Length != other.Length)
                return false;

            for (int i = 0; i != array.Length; i++)
            {
                if (!array[i].Equals(other[i]))
                    return false;
            }

            return true;
        }

    }

    internal sealed class UnsafeListTDebugView<T>
        where T : unmanaged
    {
        UnsafeList<T> Data;

        public UnsafeListTDebugView(UnsafeList<T> data)
        {
            Data = data;
        }

        public unsafe T[] Items
        {
            get
            {
                T[] result = new T[Data.Length];

                for (var i = 0; i < result.Length; ++i)
                {
                    result[i] = Data.Ptr[i];
                }

                return result;
            }
        }
    }

    /// <summary>
    /// An unmanaged, resizable list of pointers.
    /// </summary>
    /// <typeparam name="T">The type of pointer element.</typeparam>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}, IsEmpty = {IsEmpty}")]
    [DebuggerTypeProxy(typeof(UnsafePtrListTDebugView<>))]
    [StructLayout(LayoutKind.Sequential)]
    [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct UnsafePtrList<T>
        : INativeDisposable
        // IIndexable<T> and INativeList<T> can't be implemented because this[index] and ElementAt return T* instead of T.
        , IEnumerable<IntPtr> // Used by collection initializers.
        where T : unmanaged
    {
        /// <summary>
        /// The internal buffer of this list.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public readonly T** Ptr;

        /// <summary>
        /// The number of elements.
        /// </summary>
        public readonly int m_length;

        /// <summary>
        /// The number of elements that can fit in the internal buffer.
        /// </summary>
        public readonly int m_capacity;

        /// <summary>
        /// The allocator used to create the internal buffer.
        /// </summary>
        public readonly AllocatorManager.AllocatorHandle Allocator;

        [Obsolete("Use Length property (UnityUpgradable) -> Length", true)]
        public int length;

        [Obsolete("Use Capacity property (UnityUpgradable) -> Capacity", true)]
        public int capacity;

        /// <summary>
        /// The number of elements.
        /// </summary>
        /// <value>The number of elements.</value>
        public int Length
        {
            get
            {
                return this.ListData().Length;
            }

            set
            {
                this.ListData().Length = value;
            }
        }

        /// <summary>
        /// The number of elements that can fit in the internal buffer.
        /// </summary>
        /// <value>The number of elements that can fit in the internal buffer.</value>
        public int Capacity
        {
            get
            {
                return this.ListData().Capacity;
            }

            set
            {
                this.ListData().Capacity = value;
            }
        }

        /// <summary>
        /// The element at an index.
        /// </summary>
        /// <param name="index">An index.</param>
        /// <value>The element at the index.</value>
        public T* this[int index]
        {
            get
            {
                CollectionHelper.CheckIndexInRange(index, Length);
                return Ptr[CollectionHelper.AssumePositive(index)];
            }

            set
            {
                CollectionHelper.CheckIndexInRange(index, Length);
                Ptr[CollectionHelper.AssumePositive(index)] = value;
            }
        }

        /// <summary>
        /// Returns a reference to the element at a given index.
        /// </summary>
        /// <param name="index">The index to access. Must be in the range of [0..Length).</param>
        /// <returns>A reference to the element at the index.</returns>
        public ref T* ElementAt(int index)
        {
            CollectionHelper.CheckIndexInRange(index, Length);
            return ref Ptr[CollectionHelper.AssumePositive(index)];
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafePtrList.
        /// </summary>
        /// <param name="ptr">An existing pointer array to set as the internal buffer.</param>
        /// <param name="length">The length.</param>
        public unsafe UnsafePtrList(T** ptr, int length) : this()
        {
            Ptr = ptr;
            this.m_length = length;
            this.m_capacity = length;
            Allocator = AllocatorManager.None;
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafePtrList.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public unsafe UnsafePtrList(int initialCapacity, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) : this()
        {
            Ptr = null;
            m_length = 0;
            m_capacity = 0;
            Allocator = AllocatorManager.None;

            this.ListData() = new UnsafeList<IntPtr>(initialCapacity, allocator, options);
        }

        /// <summary>
        /// Returns a new list of pointers.
        /// </summary>
        /// <param name="ptr">An existing pointer array to set as the internal buffer.</param>
        /// <param name="length">The length.</param>
        /// <returns>A pointer to the new list.</returns>
        public static UnsafePtrList<T>* Create(T** ptr, int length)
        {
            UnsafePtrList<T>* listData = AllocatorManager.Allocate<UnsafePtrList<T>>(AllocatorManager.Persistent);
            *listData = new UnsafePtrList<T>(ptr, length);
            return listData;
        }

        /// <summary>
        /// Returns a new list of pointers.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        /// <returns>A pointer to the new list.</returns>
        public static UnsafePtrList<T>* Create(int initialCapacity, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            UnsafePtrList<T>* listData = AllocatorManager.Allocate<UnsafePtrList<T>>(allocator);
            *listData = new UnsafePtrList<T>(initialCapacity, allocator, options);
            return listData;
        }

        /// <summary>
        /// Destroys the list.
        /// </summary>
        /// <param name="listData">The list to destroy.</param>
        public static void Destroy(UnsafePtrList<T>* listData)
        {
            UnsafeList<IntPtr>.CheckNull(listData);
            var allocator = listData->ListData().Allocator.Value == AllocatorManager.Invalid.Value
                ? AllocatorManager.Persistent
                : listData->ListData().Allocator
            ;
            listData->Dispose();
            AllocatorManager.Free(allocator, listData);
        }

        /// <summary>
        /// Whether the list is empty.
        /// </summary>
        /// <value>True if the list is empty or the list has not been constructed.</value>
        public bool IsEmpty => !IsCreated || Length == 0;

        /// <summary>
        /// Whether this list has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this list has been allocated (and not yet deallocated).</value>
        public bool IsCreated => Ptr != null;

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            this.ListData().Dispose();
        }

        /// <summary>
        /// Creates and schedules a job that frees the memory of this list.
        /// </summary>
        /// <param name="inputDeps">The dependency for the new job.</param>
        /// <returns>The handle of the new job. The job depends upon `inputDeps` and frees the memory of this list.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps) => this.ListData().Dispose(inputDeps);

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear() => this.ListData().Clear();

        /// <summary>
        /// Sets the length, expanding the capacity if necessary.
        /// </summary>
        /// <param name="length">The new length.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public void Resize(int length, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) => this.ListData().Resize(length, options);

        /// <summary>
        /// Sets the capacity.
        /// </summary>
        /// <param name="capacity">The new capacity.</param>
        public void SetCapacity(int capacity) => this.ListData().SetCapacity(capacity);

        /// <summary>
        /// Sets the capacity to match the length.
        /// </summary>
        public void TrimExcess() => this.ListData().TrimExcess();

        /// <summary>
        /// Returns the index of the first occurrence of a specific pointer in the list.
        /// </summary>
        /// <param name="ptr">The pointer to search for in the list.</param>
        /// <returns>The index of the first occurrence of the pointer. Returns -1 if it is not found in the list.</returns>
        public int IndexOf(void* ptr)
        {
            for (int i = 0; i < Length; ++i)
            {
                if (Ptr[i] == ptr) return i;
            }

            return -1;
        }

        /// <summary>
        /// Returns true if the list contains at least one occurrence of a specific pointer.
        /// </summary>
        /// <param name="ptr">The pointer to search for in the list.</param>
        /// <returns>True if the list contains at least one occurrence of the pointer.</returns>
        public bool Contains(void* ptr)
        {
            return IndexOf(ptr) != -1;
        }

        /// <summary>
        /// Adds a pointer to the end of this list.
        /// </summary>
        /// <remarks>
        /// Increments the length by 1. Never increases the capacity.
        /// </remarks>
        /// <param name="value">The pointer to add to the end of the list.</param>
        /// <exception cref="Exception">Thrown if incrementing the length would exceed the capacity.</exception>
        public void AddNoResize(void* value)
        {
            this.ListData().AddNoResize((IntPtr)value);
        }

        /// <summary>
        /// Copies pointers from a buffer to the end of this list.
        /// </summary>
        /// <remarks>
        /// Increments the length by `count`. Never increases the capacity.
        /// </remarks>
        /// <param name="ptr">The buffer to copy from.</param>
        /// <param name="count">The number of pointers to copy from the buffer.</param>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRangeNoResize(void** ptr, int count) => this.ListData().AddRangeNoResize(ptr, count);

        /// <summary>
        /// Copies the pointers of another list to the end of this list.
        /// </summary>
        /// <param name="list">The other list to copy from.</param>
        /// <remarks>
        /// Increments the length by the length of the other list. Never increases the capacity.
        /// </remarks>
        /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
        public void AddRangeNoResize(UnsafePtrList<T> list) => this.ListData().AddRangeNoResize(list.Ptr, list.Length);

        /// <summary>
        /// Adds a pointer to the end of the list.
        /// </summary>
        /// <param name="value">The pointer to add to the end of this list.</param>
        /// <remarks>
        /// Increments the length by 1. Increases the capacity if necessary.
        /// </remarks>
        public void Add(in IntPtr value)
        {
            this.ListData().Add(value);
        }

        /// <summary>
        /// Adds a pointer to the end of the list.
        /// </summary>
        /// <param name="value">The pointer to add to the end of this list.</param>
        /// <remarks>
        /// Increments the length by 1. Increases the capacity if necessary.
        /// </remarks>
        public void Add(void* value)
        {
            this.ListData().Add((IntPtr)value);
        }

        /// <summary>
        /// Adds elements from a buffer to this list.
        /// </summary>
        /// <param name="ptr">A pointer to the buffer.</param>
        /// <param name="length">The number of elements to add to the list.</param>
        public void AddRange(void* ptr, int length) => this.ListData().AddRange(ptr, length);

        /// <summary>
        /// Copies the elements of another list to the end of this list.
        /// </summary>
        /// <param name="list">The other list to copy from.</param>
        /// <remarks>
        /// Increments the length by the length of the other list. Increases the capacity if necessary.
        /// </remarks>
        public void AddRange(UnsafePtrList<T> list) => this.ListData().AddRange(list.ListData());

        /// <summary>
        /// Shifts pointers toward the end of this list, increasing its length.
        /// </summary>
        /// <remarks>
        /// Right-shifts pointers in the list so as to create 'free' slots at the beginning or in the middle.
        ///
        /// The length is increased by `end - begin`. If necessary, the capacity will be increased accordingly.
        ///
        /// If `end` equals `begin`, the method does nothing.
        ///
        /// The pointer at index `begin` will be copied to index `end`, the pointer at index `begin + 1` will be copied to `end + 1`, and so forth.
        ///
        /// The indexes `begin` up to `end` are not cleared: they will contain whatever pointers they held prior.
        /// </remarks>
        /// <param name="begin">The index of the first pointer that will be shifted up.</param>
        /// <param name="end">The index where the first shifted pointer will end up.</param>
        /// <exception cref="ArgumentException">Thrown if `end &lt; begin`.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        public void InsertRangeWithBeginEnd(int begin, int end) => this.ListData().InsertRangeWithBeginEnd(begin, end);

        /// <summary>
        /// Copies the last pointer of this list to the specified index. Decrements the length by 1.
        /// </summary>
        /// <remarks>Useful as a cheap way to remove a pointer from this list when you don't care about preserving order.</remarks>
        /// <param name="index">The index to overwrite with the last pointer.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAtSwapBack(int index) => this.ListData().RemoveAtSwapBack(index);

        /// <summary>
        /// Copies the last *N* pointer of this list to a range in this list. Decrements the length by *N*.
        /// </summary>
        /// <remarks>
        /// Copies the last `count` pointers to the indexes `index` up to `index + count`.
        ///
        /// Useful as a cheap way to remove pointers from a list when you don't care about preserving order.
        /// </remarks>
        /// <param name="index">The index of the first pointer to overwrite.</param>
        /// <param name="count">The number of pointers to copy and remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds, `count` is negative,
        /// or `index + count` exceeds the length.</exception>
        public void RemoveRangeSwapBack(int index, int count) => this.ListData().RemoveRangeSwapBack(index, count);

        /// <summary>
        /// Copies the last *N* pointers of this list to a range in this list. Decrements the length by *N*.
        /// </summary>
        /// <remarks>
        /// Copies the last `end - begin` pointers to the indexes `begin` up to `end`.
        ///
        /// Useful as a cheap way to remove pointers from a list when you don't care about preserving order.
        ///
        /// Does nothing if `end - begin` is less than 1.
        /// </remarks>
        /// <param name="begin">The index of the first pointers to overwrite.</param>
        /// <param name="end">The index one greater than the last pointers to overwrite.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        [Obsolete("RemoveRangeSwapBackWithBeginEnd(begin, end) is deprecated, use RemoveRangeSwapBack(index, count) instead. (RemovedAfter 2021-06-02)", false)]
        public void RemoveRangeSwapBackWithBeginEnd(int begin, int end) => this.ListData().RemoveRangeSwapBackWithBeginEnd(begin, end);

        /// <summary>
        /// Removes the pointer at an index, shifting everything above it down by one. Decrements the length by 1.
        /// </summary>
        /// <param name="index">The index of the pointer to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the pointers, <see cref="RemoveAtSwapBack(int)"/> is a more efficient way to remove pointers.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public void RemoveAt(int index) => this.ListData().RemoveAt(index);

        /// <summary>
        /// Removes *N* pointers in a range, shifting everything above the range down by *N*. Decrements the length by *N*.
        /// </summary>
        /// <param name="index">The index of the first pointer to remove.</param>
        /// <param name="count">The number of pointers to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the pointers, <see cref="RemoveRangeSwapBackWithBeginEnd"/>
        /// is a more efficient way to remove pointers.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `index` is out of bounds, `count` is negative,
        /// or `index + count` exceeds the length.</exception>
        public void RemoveRange(int index, int count) => this.ListData().RemoveRange(index, count);

        /// <summary>
        /// Removes *N* pointers in a range, shifting everything above it down by *N*. Decrements the length by *N*.
        /// </summary>
        /// <param name="begin">The index of the first pointer to remove.</param>
        /// <param name="end">The index one greater than the last pointer to remove.</param>
        /// <remarks>
        /// If you don't care about preserving the order of the pointers, <see cref="RemoveRangeSwapBackWithBeginEnd"/> is a more efficient way to remove pointers.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if `end &lt; begin`.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if `begin` or `end` are out of bounds.</exception>
        [Obsolete("RemoveRangeWithBeginEnd(begin, end) is deprecated, use RemoveRange(index, count) instead. (RemovedAfter 2021-06-02)", false)]
        public void RemoveRangeWithBeginEnd(int begin, int end) => this.ListData().RemoveRangeWithBeginEnd(begin, end);

        /// <summary>
        /// This method is not implemented. It will throw NotImplementedException if it is used.
        /// </summary>
        /// <remarks>Use Enumerator GetEnumerator() instead.</remarks>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented. It will throw NotImplementedException if it is used.
        /// </summary>
        /// <remarks>Use Enumerator GetEnumerator() instead.</remarks>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator<IntPtr> IEnumerable<IntPtr>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a parallel reader of this list.
        /// </summary>
        /// <returns>A parallel reader of this list.</returns>
        public ParallelReader AsParallelReader()
        {
            return new ParallelReader(Ptr, Length);
        }

        /// <summary>
        /// A parallel reader for an UnsafePtrList&lt;T&gt;.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelReader"/> to create a parallel reader for a list.
        /// </remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe struct ParallelReader
        {
            /// <summary>
            /// The internal buffer of the list.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public readonly T** Ptr;

            /// <summary>
            /// The number of elements.
            /// </summary>
            public readonly int Length;

            internal ParallelReader(T** ptr, int length)
            {
                Ptr = ptr;
                Length = length;
            }

            /// <summary>
            /// Returns the index of the first occurrence of a specific pointer in the list.
            /// </summary>
            /// <param name="ptr">The pointer to search for in the list.</param>
            /// <returns>The index of the first occurrence of the pointer. Returns -1 if it is not found in the list.</returns>
            public int IndexOf(void* ptr)
            {
                for (int i = 0; i < Length; ++i)
                {
                    if (Ptr[i] == ptr) return i;
                }
                return -1;
            }

            /// <summary>
            /// Returns true if the list contains at least one occurrence of a specific pointer.
            /// </summary>
            /// <param name="ptr">The pointer to search for in the list.</param>
            /// <returns>True if the list contains at least one occurrence of the pointer.</returns>
            public bool Contains(void* ptr)
            {
                return IndexOf(ptr) != -1;
            }
        }

        /// <summary>
        /// Returns a parallel writer of this list.
        /// </summary>
        /// <returns>A parallel writer of this list.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(Ptr, (UnsafeList<IntPtr>*)UnsafeUtility.AddressOf(ref this));
        }

        /// <summary>
        /// A parallel writer for an UnsafePtrList&lt;T&gt;.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a list.
        /// </remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe struct ParallelWriter
        {
            /// <summary>
            /// The data of the list.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public readonly T** Ptr;

            /// <summary>
            /// The UnsafeList to write to.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<IntPtr>* ListData;

            internal unsafe ParallelWriter(T** ptr, UnsafeList<IntPtr>* listData)
            {
                Ptr = ptr;
                ListData = listData;
            }

            /// <summary>
            /// Adds a pointer to the end of the list.
            /// </summary>
            /// <param name="value">The pointer to add to the end of the list.</param>
            /// <remarks>
            /// Increments the length by 1. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if incrementing the length would exceed the capacity.</exception>
            public void AddNoResize(T* value) => ListData->AddNoResize((IntPtr)value);

            /// <summary>
            /// Copies pointers from a buffer to the end of the list.
            /// </summary>
            /// <param name="ptr">The buffer to copy from.</param>
            /// <param name="count">The number of pointers to copy from the buffer.</param>
            /// <remarks>
            /// Increments the length by `count`. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
            public void AddRangeNoResize(T** ptr, int count) => ListData->AddRangeNoResize(ptr, count);

            /// <summary>
            /// Copies the pointers of another list to the end of this list.
            /// </summary>
            /// <param name="list">The other list to copy from.</param>
            /// <remarks>
            /// Increments the length by the length of the other list. Never increases the capacity.
            /// </remarks>
            /// <exception cref="Exception">Thrown if the increased length would exceed the capacity.</exception>
            public void AddRangeNoResize(UnsafePtrList<T> list) => ListData->AddRangeNoResize(list.Ptr, list.Length);
        }
    }

    [BurstCompatible]
    internal static class UnsafePtrListTExtensions
    {
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static ref UnsafeList<IntPtr> ListData<T>(ref this UnsafePtrList<T> from) where T : unmanaged => ref UnsafeUtility.As<UnsafePtrList<T>, UnsafeList<IntPtr>>(ref from);
    }

    internal sealed class UnsafePtrListTDebugView<T>
        where T : unmanaged
    {
        UnsafePtrList<T> Data;

        public UnsafePtrListTDebugView(UnsafePtrList<T> data)
        {
            Data = data;
        }

        public unsafe T*[] Items
        {
            get
            {
                T*[] result = new T*[Data.Length];

                for (var i = 0; i < result.Length; ++i)
                {
                    result[i] = Data.Ptr[i];
                }

                return result;
            }
        }
    }
}
