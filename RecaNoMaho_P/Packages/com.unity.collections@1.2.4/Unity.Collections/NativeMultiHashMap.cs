using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;

namespace Unity.Collections
{
    /// <summary>
    /// An iterator over all values associated with an individual key in a multi hash map.
    /// </summary>
    /// <remarks>The iteration order over the values associated with a key is an implementation detail. Do not rely upon any particular ordering.</remarks>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
    public struct NativeMultiHashMapIterator<TKey>
        where TKey : struct
    {
        internal TKey key;
        internal int NextEntryIndex;
        internal int EntryIndex;

        /// <summary>
        /// Returns the entry index.
        /// </summary>
        /// <returns>The entry index.</returns>
        public int GetEntryIndex() => EntryIndex;
    }

    /// <summary>
    /// An unordered, expandable associative array. Each key can have more than one associated value.
    /// </summary>
    /// <remarks>
    /// Unlike a regular NativeHashMap, a NativeMultiHashMap can store multiple key-value pairs with the same key.
    ///
    /// The keys are not deduplicated: two key-value pairs with the same key are stored as fully separate key-value pairs.
    /// </remarks>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerTypeProxy(typeof(NativeMultiHashMapDebuggerTypeProxy<,>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
    public unsafe struct NativeMultiHashMap<TKey, TValue>
        : INativeDisposable
        , IEnumerable<KeyValue<TKey, TValue>> // Used by collection initializers.
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        internal UnsafeMultiHashMap<TKey, TValue> m_MultiHashMapData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeMultiHashMap<TKey, TValue>>();

#if REMOVE_DISPOSE_SENTINEL
#else
        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel m_DisposeSentinel;
#endif
#endif

        /// <summary>
        /// Returns a newly allocated multi hash map.
        /// </summary>
        /// <param name="capacity">The number of key-value pairs that should fit in the initial allocation.</param>
        /// <param name="allocator">The allocator to use.</param>
        public NativeMultiHashMap(int capacity, AllocatorManager.AllocatorHandle allocator)
            : this(capacity, allocator, 2)
        {
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal void Initialize<U>(int capacity, ref U allocator, int disposeSentinelStackDepth)
            where U : unmanaged, AllocatorManager.IAllocator
        {
            m_MultiHashMapData = new UnsafeMultiHashMap<TKey, TValue>(capacity, allocator.Handle);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if REMOVE_DISPOSE_SENTINEL
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
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

            CollectionHelper.SetStaticSafetyId<NativeMultiHashMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        NativeMultiHashMap(int capacity, AllocatorManager.AllocatorHandle allocator, int disposeSentinelStackDepth)
        {
            this = default;
            Initialize(capacity, ref allocator, disposeSentinelStackDepth);
        }

        /// <summary>
        /// Whether this hash map is empty.
        /// </summary>
        /// <value>True if the hash map is empty or if the hash map has not been constructed.</value>
        public bool IsEmpty
        {
            get
            {
                CheckRead();
                return m_MultiHashMapData.IsEmpty;
            }
        }

        /// <summary>
        /// Returns the current number of key-value pairs in this hash map.
        /// </summary>
        /// <remarks>Key-value pairs with matching keys are counted as separate, individual pairs.</remarks>
        /// <returns>The current number of key-value pairs in this hash map.</returns>
        public int Count()
        {
            CheckRead();
            return m_MultiHashMapData.Count();
        }

        /// <summary>
        /// Returns the number of key-value pairs that fit in the current allocation.
        /// </summary>
        /// <value>The number of key-value pairs that fit in the current allocation.</value>
        /// <param name="value">A new capacity. Must be larger than the current capacity.</param>
        /// <exception cref="Exception">Thrown if `value` is less than the current capacity.</exception>
        public int Capacity
        {
            get
            {
                CheckRead();
                return m_MultiHashMapData.Capacity;
            }

            set
            {
                CheckWrite();
                m_MultiHashMapData.Capacity = value;
            }
        }

        /// <summary>
        /// Removes all key-value pairs.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            CheckWrite();
            m_MultiHashMapData.Clear();
        }

        /// <summary>
        /// Adds a new key-value pair.
        /// </summary>
        /// <remarks>
        /// If a key-value pair with this key is already present, an additional separate key-value pair is added.
        /// </remarks>
        /// <param name="key">The key to add.</param>
        /// <param name="item">The value to add.</param>
        public void Add(TKey key, TValue item)
        {
            CheckWrite();
            m_MultiHashMapData.Add(key, item);
        }

        /// <summary>
        /// Removes a key and its associated value(s).
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>The number of removed key-value pairs. If the key was not present, returns 0.</returns>
        public int Remove(TKey key)
        {
            CheckWrite();
            return m_MultiHashMapData.Remove(key);
        }

        /// <summary>
        /// Removes a single key-value pair.
        /// </summary>
        /// <param name="it">An iterator representing the key-value pair to remove.</param>
        /// <exception cref="InvalidOperationException">Thrown if the iterator is invalid.</exception>
        public void Remove(NativeMultiHashMapIterator<TKey> it)
        {
            CheckWrite();
            m_MultiHashMapData.Remove(it);
        }

        /// <summary>
        /// Gets an iterator for a key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="item">Outputs the associated value represented by the iterator.</param>
        /// <param name="it">Outputs an iterator.</param>
        /// <returns>True if the key was present.</returns>
        public bool TryGetFirstValue(TKey key, out TValue item, out NativeMultiHashMapIterator<TKey> it)
        {
            CheckRead();
            return m_MultiHashMapData.TryGetFirstValue(key, out item, out it);
        }

        /// <summary>
        /// Advances an iterator to the next value associated with its key.
        /// </summary>
        /// <param name="item">Outputs the next value.</param>
        /// <param name="it">A reference to the iterator to advance.</param>
        /// <returns>True if the key was present and had another value.</returns>
        public bool TryGetNextValue(out TValue item, ref NativeMultiHashMapIterator<TKey> it)
        {
            CheckRead();
            return m_MultiHashMapData.TryGetNextValue(out item, ref it);
        }

        /// <summary>
        /// Returns true if a given key is present in this hash map.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>True if the key was present in this hash map.</returns>
        public bool ContainsKey(TKey key)
        {
            return TryGetFirstValue(key, out var temp0, out var temp1);
        }

        /// <summary>
        /// Returns the number of values associated with a given key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>The number of values associated with the key. Returns 0 if the key was not present.</returns>
        public int CountValuesForKey(TKey key)
        {
            if (!TryGetFirstValue(key, out var value, out var iterator))
            {
                return 0;
            }

            var count = 1;
            while (TryGetNextValue(out value, ref iterator))
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Sets a new value for an existing key-value pair.
        /// </summary>
        /// <param name="item">The new value.</param>
        /// <param name="it">The iterator representing a key-value pair.</param>
        /// <returns>True if a value was overwritten.</returns>
        public bool SetValue(TValue item, NativeMultiHashMapIterator<TKey> it)
        {
            CheckWrite();
            return m_MultiHashMapData.SetValue(item, it);
        }

        /// <summary>
        /// Whether this hash map has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this hash map has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_MultiHashMapData.IsCreated;

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if REMOVE_DISPOSE_SENTINEL
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#else
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
#endif
            m_MultiHashMapData.Dispose();
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this hash map.
        /// </summary>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will dispose this hash map.</returns>
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
            var jobHandle = new UnsafeHashMapDataDisposeJob { Data = new UnsafeHashMapDataDispose { m_Buffer = m_MultiHashMapData.m_Buffer, m_AllocatorLabel = m_MultiHashMapData.m_AllocatorLabel, m_Safety = m_Safety } }.Schedule(inputDeps);

            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new UnsafeHashMapDataDisposeJob { Data = new UnsafeHashMapDataDispose { m_Buffer = m_MultiHashMapData.m_Buffer, m_AllocatorLabel = m_MultiHashMapData.m_AllocatorLabel } }.Schedule(inputDeps);
#endif
            m_MultiHashMapData.m_Buffer = null;

            return jobHandle;
        }

        /// <summary>
        /// Returns an array with a copy of all the keys (in no particular order).
        /// </summary>
        /// <remarks>A key with *N* values is included *N* times in the array.
        ///
        /// Use `GetUniqueKeyArray` of <see cref="Unity.Collections.NativeHashMapExtensions"/> instead if you only want one occurrence of each key.</remarks>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with a copy of all the keys (in no particular order).</returns>
        public NativeArray<TKey> GetKeyArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_MultiHashMapData.GetKeyArray(allocator);
        }

        /// <summary>
        /// Returns an array with a copy of all the values (in no particular order).
        /// </summary>
        /// <remarks>The values are not deduplicated. If you sort the returned array,
        /// you can use <see cref="Unity.Collections.NativeHashMapExtensions.Unique{T}"/> to remove duplicate values.</remarks>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with a copy of all the values (in no particular order).</returns>
        public NativeArray<TValue> GetValueArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_MultiHashMapData.GetValueArray(allocator);
        }

        /// <summary>
        /// Returns a NativeKeyValueArrays with a copy of all the keys and values (in no particular order).
        /// </summary>
        /// <remarks>A key with *N* values is included *N* times in the array.
        /// </remarks>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>A NativeKeyValueArrays with a copy of all the keys and values (in no particular order).</returns>
        public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_MultiHashMapData.GetKeyValueArrays(allocator);
        }

        /// <summary>
        /// Returns a parallel writer for this hash map.
        /// </summary>
        /// <returns>A parallel writer for this hash map.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;
            writer.m_Writer = m_MultiHashMapData.AsParallelWriter();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            writer.m_Safety = m_Safety;
            CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref writer.m_Safety, ref s_staticSafetyId.Data);
#endif
            return writer;
        }

        /// <summary>
        /// A parallel writer for a NativeMultiHashMap.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a NativeMultiHashMap.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public unsafe struct ParallelWriter
        {
            internal UnsafeMultiHashMap<TKey, TValue>.ParallelWriter m_Writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ParallelWriter>();
#endif
            /// <summary>
            /// Returns the index of the current thread.
            /// </summary>
            /// <remarks>In a job, each thread gets its own copy of the ParallelWriter struct, and the job system assigns
            /// each copy the index of its thread.</remarks>
            /// <value>The index of the current thread.</value>
            public int m_ThreadIndex => m_Writer.m_ThreadIndex;

            /// <summary>
            /// Returns the number of key-value pairs that fit in the current allocation.
            /// </summary>
            /// <value>The number of key-value pairs that fit in the current allocation.</value>
            public int Capacity
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return m_Writer.Capacity;
                }
            }

            /// <summary>
            /// Adds a new key-value pair.
            /// </summary>
            /// <remarks>
            /// If a key-value pair with this key is already present, an additional separate key-value pair is added.
            /// </remarks>
            /// <param name="key">The key to add.</param>
            /// <param name="item">The value to add.</param>
            public void Add(TKey key, TValue item)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                m_Writer.Add(key, item);
            }
        }

        /// <summary>
        /// Returns an enumerator over the values of an individual key.
        /// </summary>
        /// <param name="key">The key to get an enumerator for.</param>
        /// <returns>An enumerator over the values of a key.</returns>
        public Enumerator GetValuesForKey(TKey key)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return new Enumerator { hashmap = this, key = key, isFirst = true };
        }

        /// <summary>
        /// An enumerator over the values of an individual key in a multi hash map.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is not valid to read.
        /// The first <see cref="MoveNext"/> call advances the enumerator to the first value of the key.
        /// </remarks>
        public struct Enumerator : IEnumerator<TValue>
        {
            internal NativeMultiHashMap<TKey, TValue> hashmap;
            internal TKey key;
            internal bool isFirst;

            TValue value;
            NativeMultiHashMapIterator<TKey> iterator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next value of the key.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            public bool MoveNext()
            {
                //Avoids going beyond the end of the collection.
                if (isFirst)
                {
                    isFirst = false;
                    return hashmap.TryGetFirstValue(key, out value, out iterator);
                }

                return hashmap.TryGetNextValue(out value, ref iterator);
            }

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => isFirst = true;

            /// <summary>
            /// The current value.
            /// </summary>
            /// <value>The current value.</value>
            public TValue Current => value;

            object IEnumerator.Current => Current;

            /// <summary>
            /// Returns this enumerator.
            /// </summary>
            /// <returns>This enumerator.</returns>
            public Enumerator GetEnumerator() { return this; }
        }

        /// <summary>
        /// Returns an enumerator over the key-value pairs of this hash map.
        /// </summary>
        /// <remarks>A key with *N* values is visited by the enumerator *N* times.</remarks>
        /// <returns>An enumerator over the key-value pairs of this hash map.</returns>
        public KeyValueEnumerator GetEnumerator()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var ash = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref ash);
#endif
            return new KeyValueEnumerator
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = ash,
#endif
                m_Enumerator = new UnsafeHashMapDataEnumerator(m_MultiHashMapData.m_Buffer),
            };
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator<KeyValue<TKey, TValue>> IEnumerable<KeyValue<TKey, TValue>>.GetEnumerator()
        {
            throw new NotImplementedException();
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
        /// An enumerator over the key-value pairs of a multi hash map.
        /// </summary>
        /// <remarks>A key with *N* values is visited by the enumerator *N* times.
        ///
        /// In an enumerator's initial state, <see cref="Current"/> is not valid to read.
        /// The first <see cref="MoveNext"/> call advances the enumerator to the first key-value pair.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct KeyValueEnumerator : IEnumerator<KeyValue<TKey, TValue>>
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
#endif
            internal UnsafeHashMapDataEnumerator m_Enumerator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next key-value pair.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            public unsafe bool MoveNext()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Enumerator.MoveNext();
            }

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                m_Enumerator.Reset();
            }

            /// <summary>
            /// The current key-value pair.
            /// </summary>
            /// <value>The current key-value pair.</value>
            public KeyValue<TKey, TValue> Current
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return m_Enumerator.GetCurrent<TKey, TValue>();
                }
            }

            object IEnumerator.Current => Current;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
        }
    }

    internal sealed class NativeMultiHashMapDebuggerTypeProxy<TKey, TValue>
        where TKey : struct, IEquatable<TKey>, IComparable<TKey>
        where TValue : struct
    {
#if !NET_DOTS
        NativeMultiHashMap<TKey, TValue> m_Target;

        public NativeMultiHashMapDebuggerTypeProxy(NativeMultiHashMap<TKey, TValue> target)
        {
            m_Target = target;
        }

        public List<ListPair<TKey, List<TValue>>> Items
        {
            get
            {
                var result = new List<ListPair<TKey, List<TValue>>>();
                var keys = m_Target.GetUniqueKeyArray(Allocator.Temp);

                using (keys.Item1)
                {
                    for (var k = 0; k < keys.Item2; ++k)
                    {
                        var values = new List<TValue>();
                        if (m_Target.TryGetFirstValue(keys.Item1[k], out var value, out var iterator))
                        {
                            do
                            {
                                values.Add(value);
                            }
                            while (m_Target.TryGetNextValue(out value, ref iterator));
                        }

                        result.Add(new ListPair<TKey, List<TValue>>(keys.Item1[k], values));
                    }
                }

                return result;
            }
        }
#endif
    }

    [BurstCompatible]
    public unsafe static class NativeMultiHashMapExtensions
    {
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(int), typeof(AllocatorManager.AllocatorHandle) })]
        internal static void Initialize<TKey, TValue, U>(ref this NativeMultiHashMap<TKey, TValue> nativeMultiHashMap,
                                                            int capacity,
                                                            ref U allocator,
                                                            int disposeSentinelStackDepth = 2)
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
            where U : unmanaged, AllocatorManager.IAllocator
        {
            nativeMultiHashMap.m_MultiHashMapData = new UnsafeMultiHashMap<TKey, TValue>(capacity, allocator.Handle);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if REMOVE_DISPOSE_SENTINEL
            nativeMultiHashMap.m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
#else
            if (allocator.IsCustomAllocator)
            {
                nativeMultiHashMap.m_Safety = AtomicSafetyHandle.Create();
                nativeMultiHashMap.m_DisposeSentinel = null;
            }
            else
            {
                DisposeSentinel.Create(out nativeMultiHashMap.m_Safety,
                                        out nativeMultiHashMap.m_DisposeSentinel,
                                        disposeSentinelStackDepth,
                                        allocator.ToAllocator);
            }
#endif

            CollectionHelper.SetStaticSafetyId<NativeMultiHashMap<TKey, TValue>>(ref nativeMultiHashMap.m_Safety, ref NativeMultiHashMap<TKey, TValue>.s_staticSafetyId.Data);
#endif
        }
    }
}
