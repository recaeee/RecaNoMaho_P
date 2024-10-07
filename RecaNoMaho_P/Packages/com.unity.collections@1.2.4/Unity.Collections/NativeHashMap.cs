using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Collections
{
    /// <summary>
    /// The keys and values of a hash map copied into two parallel arrays.
    /// </summary>
    /// <remarks>For each key-value pair copied from the hash map, the key is stored in `Keys[i]` while the value is stored in `Values[i]` (for the same `i`).
    ///
    /// NativeKeyValueArrays is not actually itself a native collection: it contains a NativeArray for the keys and a NativeArray for the values,
    /// but a NativeKeyValueArrays does not have its own safety handles.</remarks>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
    public struct NativeKeyValueArrays<TKey, TValue>
        : INativeDisposable
        where TKey : struct
        where TValue : struct
    {
        /// <summary>
        /// The keys.
        /// </summary>
        /// <value>The keys. The key at `Keys[i]` is paired with the value at `Values[i]`.</value>
        public NativeArray<TKey> Keys;

        /// <summary>
        /// The values.
        /// </summary>
        /// <value>The values. The value at `Values[i]` is paired with the key at `Keys[i]`.</value>
        public NativeArray<TValue> Values;

        /// <summary>
        /// The number of key-value pairs.
        /// </summary>
        /// <value>The number of key-value pairs.</value>
        public int Length => Keys.Length;

        /// <summary>
        /// Initializes and returns an instance of NativeKeyValueArrays.
        /// </summary>
        /// <param name="length">The number of keys-value pairs.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public NativeKeyValueArrays(int length, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options)
        {
            Keys = CollectionHelper.CreateNativeArray<TKey>(length, allocator, options);
            Values = CollectionHelper.CreateNativeArray<TValue>(length, allocator, options);
        }

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            Keys.Dispose();
            Values.Dispose();
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this collection's key and value arrays.
        /// </summary>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will dispose this collection's key and value arrays.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return Keys.Dispose(Values.Dispose(inputDeps));
        }
    }

    /// <summary>
    /// An unordered, expandable associative array.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Count = {m_HashMapData.Count()}, Capacity = {m_HashMapData.Capacity}, IsCreated = {m_HashMapData.IsCreated}, IsEmpty = {IsEmpty}")]
    [DebuggerTypeProxy(typeof(NativeHashMapDebuggerTypeProxy<,>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
    public unsafe struct NativeHashMap<TKey, TValue>
        : INativeDisposable
        , IEnumerable<KeyValue<TKey, TValue>> // Used by collection initializers.
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        internal UnsafeHashMap<TKey, TValue> m_HashMapData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeHashMap<TKey, TValue>>();

#if REMOVE_DISPOSE_SENTINEL
#else
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif
#endif

        /// <summary>
        /// Initializes and returns an instance of NativeHashMap.
        /// </summary>
        /// <param name="capacity">The number of key-value pairs that should fit in the initial allocation.</param>
        /// <param name="allocator">The allocator to use.</param>
        public NativeHashMap(int capacity, AllocatorManager.AllocatorHandle allocator)
            : this(capacity, allocator, 2)
        {
        }

        NativeHashMap(int capacity, AllocatorManager.AllocatorHandle allocator, int disposeSentinelStackDepth)
        {
            m_HashMapData = new UnsafeHashMap<TKey, TValue>(capacity, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if REMOVE_DISPOSE_SENTINEL
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
#else
            if (AllocatorManager.IsCustomAllocator(allocator.ToAllocator))
            {
                m_Safety = AtomicSafetyHandle.Create();
                m_DisposeSentinel = null;
            }
            else
            {
                DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator.ToAllocator);
            }
#endif

            CollectionHelper.SetStaticSafetyId<NativeHashMap<TKey, TValue>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        /// <summary>
        /// Whether this hash map is empty.
        /// </summary>
        /// <value>True if this hash map is empty or if the map has not been constructed.</value>
        public bool IsEmpty
        {
            get
            {
                if (!IsCreated)
                {
                    return true;
                }

                CheckRead();
                return m_HashMapData.IsEmpty;
            }
        }

        /// <summary>
        /// The current number of key-value pairs in this hash map.
        /// </summary>
        /// <returns>The current number of key-value pairs in this hash map.</returns>
        public int Count()
        {
            CheckRead();
            return m_HashMapData.Count();
        }

        /// <summary>
        /// The number of key-value pairs that fit in the current allocation.
        /// </summary>
        /// <value>The number of key-value pairs that fit in the current allocation.</value>
        /// <param name="value">A new capacity. Must be larger than the current capacity.</param>
        /// <exception cref="Exception">Thrown if `value` is less than the current capacity.</exception>
        public int Capacity
        {
            get
            {
                CheckRead();
                return m_HashMapData.Capacity;
            }

            set
            {
                CheckWrite();
                m_HashMapData.Capacity = value;
            }
        }

        /// <summary>
        /// Removes all key-value pairs.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            CheckWrite();
            m_HashMapData.Clear();
        }


        /// <summary>
        /// Adds a new key-value pair.
        /// </summary>
        /// <remarks>If the key is already present, this method returns false without modifying the hash map.</remarks>
        /// <param name="key">The key to add.</param>
        /// <param name="item">The value to add.</param>
        /// <returns>True if the key-value pair was added.</returns>
        public bool TryAdd(TKey key, TValue item)
        {
            CheckWrite();
            return m_HashMapData.TryAdd(key, item);
        }

        /// <summary>
        /// Adds a new key-value pair.
        /// </summary>
        /// <remarks>If the key is already present, this method throws without modifying the hash map.</remarks>
        /// <param name="key">The key to add.</param>
        /// <param name="item">The value to add.</param>
        /// <exception cref="ArgumentException">Thrown if the key was already present.</exception>
        public void Add(TKey key, TValue item)
        {
            var added = TryAdd(key, item);

            if (!added)
            {
                ThrowKeyAlreadyAdded(key);
            }
        }

        /// <summary>
        /// Removes a key-value pair.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>True if a key-value pair was removed.</returns>
        public bool Remove(TKey key)
        {
            CheckWrite();
            return m_HashMapData.Remove(key);
        }

        /// <summary>
        /// Returns the value associated with a key.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="item">Outputs the value associated with the key. Outputs default if the key was not present.</param>
        /// <returns>True if the key was present.</returns>
        public bool TryGetValue(TKey key, out TValue item)
        {
            CheckRead();
            return m_HashMapData.TryGetValue(key, out item);
        }

        /// <summary>
        /// Returns true if a given key is present in this hash map.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>True if the key was present.</returns>
        public bool ContainsKey(TKey key)
        {
            CheckRead();
            return m_HashMapData.ContainsKey(key);
        }

        /// <summary>
        /// Gets and sets values by key.
        /// </summary>
        /// <remarks>Getting a key that is not present will throw. Setting a key that is not already present will add the key.</remarks>
        /// <param name="key">The key to look up.</param>
        /// <value>The value associated with the key.</value>
        /// <exception cref="ArgumentException">For getting, thrown if the key was not present.</exception>
        public TValue this[TKey key]
        {
            get
            {
                CheckRead();

                TValue res;

                if (m_HashMapData.TryGetValue(key, out res))
                {
                    return res;
                }

                ThrowKeyNotPresent(key);

                return default;
            }

            set
            {
                CheckWrite();
                m_HashMapData[key] = value;
            }
        }

        /// <summary>
        /// Whether this hash map has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this hash map has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_HashMapData.IsCreated;

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
            m_HashMapData.Dispose();
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

            var jobHandle = new UnsafeHashMapDataDisposeJob { Data = new UnsafeHashMapDataDispose { m_Buffer = m_HashMapData.m_Buffer, m_AllocatorLabel = m_HashMapData.m_AllocatorLabel, m_Safety = m_Safety } }.Schedule(inputDeps);

            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new UnsafeHashMapDataDisposeJob { Data = new UnsafeHashMapDataDispose { m_Buffer = m_HashMapData.m_Buffer, m_AllocatorLabel = m_HashMapData.m_AllocatorLabel }  }.Schedule(inputDeps);
#endif
            m_HashMapData.m_Buffer = null;

            return jobHandle;
        }

        /// <summary>
        /// Returns an array with a copy of all this hash map's keys (in no particular order).
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with a copy of all this hash map's keys (in no particular order).</returns>
        public NativeArray<TKey> GetKeyArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_HashMapData.GetKeyArray(allocator);
        }

        /// <summary>
        /// Returns an array with a copy of all this hash map's values (in no particular order).
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with a copy of all this hash map's values (in no particular order).</returns>
        public NativeArray<TValue> GetValueArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_HashMapData.GetValueArray(allocator);
        }

        /// <summary>
        /// Returns a NativeKeyValueArrays with a copy of all this hash map's keys and values.
        /// </summary>
        /// <remarks>The key-value pairs are copied in no particular order. For all `i`, `Values[i]` will be the value associated with `Keys[i]`.</remarks>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>A NativeKeyValueArrays with a copy of all this hash map's keys and values.</returns>
        public NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_HashMapData.GetKeyValueArrays(allocator);
        }

        /// <summary>
        /// Returns a parallel writer for this hash map.
        /// </summary>
        /// <returns>A parallel writer for this hash map.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;
            writer.m_Writer = m_HashMapData.AsParallelWriter();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            writer.m_Safety = m_Safety;
            CollectionHelper.SetStaticSafetyId<ParallelWriter>(ref writer.m_Safety, ref ParallelWriter.s_staticSafetyId.Data);
#endif
            return writer;
        }

        /// <summary>
        /// A parallel writer for a NativeHashMap.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a NativeHashMap.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        [DebuggerDisplay("Capacity = {m_Writer.Capacity}")]
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public unsafe struct ParallelWriter
        {
            internal UnsafeHashMap<TKey, TValue>.ParallelWriter m_Writer;

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
            /// The number of key-value pairs that fit in the current allocation.
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
            /// <remarks>If the key is already present, this method returns false without modifying this hash map.</remarks>
            /// <param name="key">The key to add.</param>
            /// <param name="item">The value to add.</param>
            /// <returns>True if the key-value pair was added.</returns>
            public bool TryAdd(TKey key, TValue item)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                return m_Writer.TryAdd(key, item);
            }
        }

        /// <summary>
        /// Returns an enumerator over the key-value pairs of this hash map.
        /// </summary>
        /// <returns>An enumerator over the key-value pairs of this hash map.</returns>
        public Enumerator GetEnumerator()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var ash = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref ash);
#endif
            return new Enumerator
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = ash,
#endif
                m_Enumerator = new UnsafeHashMapDataEnumerator(m_HashMapData.m_Buffer),
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
        /// An enumerator over the key-value pairs of a hash map.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is not valid to read.
        /// From this state, the first <see cref="MoveNext"/> call advances the enumerator to the first key-value pair.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Enumerator : IEnumerator<KeyValue<TKey, TValue>>
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
            public bool MoveNext()
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
            public KeyValue<TKey, TValue> Current => m_Enumerator.GetCurrent<TKey, TValue>();

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

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ThrowKeyNotPresent(TKey key)
        {
            throw new ArgumentException($"Key: {key} is not present in the NativeHashMap.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ThrowKeyAlreadyAdded(TKey key)
        {
            throw new ArgumentException("An item with the same key has already been added", nameof(key));
        }
    }

    internal sealed class NativeHashMapDebuggerTypeProxy<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
#if !NET_DOTS
        UnsafeHashMap<TKey, TValue> m_Target;

        public NativeHashMapDebuggerTypeProxy(NativeHashMap<TKey, TValue> target)
        {
            m_Target = target.m_HashMapData;
        }

        public List<Pair<TKey, TValue>> Items
        {
            get
            {
                var result = new List<Pair<TKey, TValue>>();
                using (var kva = m_Target.GetKeyValueArrays(Allocator.Temp))
                {
                    for (var i = 0; i < kva.Length; ++i)
                    {
                        result.Add(new Pair<TKey, TValue>(kva.Keys[i], kva.Values[i]));
                    }
                }
                return result;
            }
        }
#endif
    }
}
