using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// An unordered, expandable associative array. Each key can have more than one associated value.
    /// </summary>
    /// <remarks>
    /// Unlike a regular UnsafeHashMap, an UnsafeMultiHashMap can store multiple key-value pairs with the same key.
    ///
    /// The keys are not deduplicated: two key-value pairs with the same key are stored as fully separate key-value pairs.
    /// </remarks>
    /// <typeparam name="TKey">The type of the keys.</typeparam>
    /// <typeparam name="TValue">The type of the values.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerTypeProxy(typeof(UnsafeMultiHashMapDebuggerTypeProxy<,>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
    public unsafe struct UnsafeMultiHashMap<TKey, TValue>
        : INativeDisposable
        , IEnumerable<KeyValue<TKey, TValue>> // Used by collection initializers.
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeHashMapData* m_Buffer;
        internal AllocatorManager.AllocatorHandle m_AllocatorLabel;

        /// <summary>
        /// Initializes and returns an instance of UnsafeMultiHashMap.
        /// </summary>
        /// <param name="capacity">The number of key-value pairs that should fit in the initial allocation.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeMultiHashMap(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_AllocatorLabel = allocator;
            // Bucket size if bigger to reduce collisions
            UnsafeHashMapData.AllocateHashMap<TKey, TValue>(capacity, capacity * 2, allocator, out m_Buffer);
            Clear();
        }

        /// <summary>
        /// Whether this hash map is empty.
        /// </summary>
        /// <value>True if this hash map is empty or the hash map has not been constructed.</value>
        public bool IsEmpty => !IsCreated || UnsafeHashMapData.IsEmpty(m_Buffer);

        /// <summary>
        /// Returns the current number of key-value pairs in this hash map.
        /// </summary>
        /// <remarks>Key-value pairs with matching keys are counted as separate, individual pairs.</remarks>
        /// <returns>The current number of key-value pairs in this hash map.</returns>
        public int Count()
        {
            if (m_Buffer->allocatedIndexLength <= 0)
            {
                return 0;
            }

            return UnsafeHashMapData.GetCount(m_Buffer);
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
                UnsafeHashMapData* data = m_Buffer;
                return data->keyCapacity;
            }

            set
            {
                UnsafeHashMapData* data = m_Buffer;
                UnsafeHashMapData.ReallocateHashMap<TKey, TValue>(data, value, UnsafeHashMapData.GetBucketSize(value), m_AllocatorLabel);
            }
        }

        /// <summary>
        /// Removes all key-value pairs.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            UnsafeHashMapBase<TKey, TValue>.Clear(m_Buffer);
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
            UnsafeHashMapBase<TKey, TValue>.TryAdd(m_Buffer, key, item, true, m_AllocatorLabel);
        }

        /// <summary>
        /// Removes a key and its associated value(s).
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>The number of removed key-value pairs. If the key was not present, returns 0.</returns>
        public int Remove(TKey key)
        {
            return UnsafeHashMapBase<TKey, TValue>.Remove(m_Buffer, key, true);
        }

        /// <summary>
        /// Removes all key-value pairs with a particular key and a particular value.
        /// </summary>
        /// <remarks>Removes all key-value pairs which have a particular key and which *also have* a particular value.
        /// In other words: (key *AND* value) rather than (key *OR* value).</remarks>
        /// <typeparam name="TValueEQ">The type of the value.</typeparam>
        /// <param name="key">The key of the key-value pairs to remove.</param>
        /// <param name="value">The value of the key-value pairs to remove.</param>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public void Remove<TValueEQ>(TKey key, TValueEQ value)
            where TValueEQ : struct, IEquatable<TValueEQ>
        {
            UnsafeHashMapBase<TKey, TValueEQ>.RemoveKeyValue(m_Buffer, key, value);
        }

        /// <summary>
        /// Removes a single key-value pair.
        /// </summary>
        /// <param name="it">An iterator representing the key-value pair to remove.</param>
        /// <exception cref="InvalidOperationException">Thrown if the iterator is invalid.</exception>
        public void Remove(NativeMultiHashMapIterator<TKey> it)
        {
            UnsafeHashMapBase<TKey, TValue>.Remove(m_Buffer, it);
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
            return UnsafeHashMapBase<TKey, TValue>.TryGetFirstValueAtomic(m_Buffer, key, out item, out it);
        }

        /// <summary>
        /// Advances an iterator to the next value associated with its key.
        /// </summary>
        /// <param name="item">Outputs the next value.</param>
        /// <param name="it">A reference to the iterator to advance.</param>
        /// <returns>True if the key was present and had another value.</returns>
        public bool TryGetNextValue(out TValue item, ref NativeMultiHashMapIterator<TKey> it)
        {
            return UnsafeHashMapBase<TKey, TValue>.TryGetNextValueAtomic(m_Buffer, out item, ref it);
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
            return UnsafeHashMapBase<TKey, TValue>.SetValue(m_Buffer, ref it, ref item);
        }

        /// <summary>
        /// Whether this hash map has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this hash map has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_Buffer != null;

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            UnsafeHashMapData.DeallocateHashMap(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this hash map.
        /// </summary>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will dispose this hash map.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new UnsafeHashMapDisposeJob { Data = m_Buffer, Allocator = m_AllocatorLabel }.Schedule(inputDeps);
            m_Buffer = null;
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
            var result = CollectionHelper.CreateNativeArray<TKey>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            UnsafeHashMapData.GetKeyArray(m_Buffer, result);
            return result;
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
            var result = CollectionHelper.CreateNativeArray<TValue>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            UnsafeHashMapData.GetValueArray(m_Buffer, result);
            return result;
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
            var result = new NativeKeyValueArrays<TKey, TValue>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            UnsafeHashMapData.GetKeyValueArrays(m_Buffer, result);
            return result;
        }

        /// <summary>
        /// Returns an enumerator over the values of an individual key.
        /// </summary>
        /// <param name="key">The key to get an enumerator for.</param>
        /// <returns>An enumerator over the values of a key.</returns>
        public Enumerator GetValuesForKey(TKey key)
        {
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
            internal UnsafeMultiHashMap<TKey, TValue> hashmap;
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
        /// Returns a parallel writer for this hash map.
        /// </summary>
        /// <returns>A parallel writer for this hash map.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;

#if UNITY_DOTSRUNTIME
            writer.m_ThreadIndex = -1;    // aggressively check that code-gen has patched the ThreadIndex
#else
            writer.m_ThreadIndex = 0;
#endif
            writer.m_Buffer = m_Buffer;

            return writer;
        }

        /// <summary>
        /// A parallel writer for an UnsafeMultiHashMap.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a NativeMultiHashMap.
        /// </remarks>
        [NativeContainerIsAtomicWriteOnly]
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public unsafe struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeHashMapData* m_Buffer;

            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            /// <summary>
            /// Returns the number of key-value pairs that fit in the current allocation.
            /// </summary>
            /// <value>The number of key-value pairs that fit in the current allocation.</value>
            public int Capacity
            {
                get
                {
                    return m_Buffer->keyCapacity;
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
                Assert.IsTrue(m_ThreadIndex >= 0);
                UnsafeHashMapBase<TKey, TValue>.AddAtomicMulti(m_Buffer, key, item, m_ThreadIndex);
            }
        }

        /// <summary>
        /// Returns an enumerator over the key-value pairs of this hash map.
        /// </summary>
        /// <remarks>A key with *N* values is visited by the enumerator *N* times.</remarks>
        /// <returns>An enumerator over the key-value pairs of this hash map.</returns>
        public KeyValueEnumerator GetEnumerator()
        {
            return new KeyValueEnumerator { m_Enumerator = new UnsafeHashMapDataEnumerator(m_Buffer) };
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
        public struct KeyValueEnumerator : IEnumerator<KeyValue<TKey, TValue>>
        {
            internal UnsafeHashMapDataEnumerator m_Enumerator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next key-value pair.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            public bool MoveNext() => m_Enumerator.MoveNext();

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => m_Enumerator.Reset();

            /// <summary>
            /// The current key-value pair.
            /// </summary>
            /// <value>The current key-value pair.</value>
            public KeyValue<TKey, TValue> Current => m_Enumerator.GetCurrent<TKey, TValue>();

            object IEnumerator.Current => Current;
        }
    }

    internal sealed class UnsafeMultiHashMapDebuggerTypeProxy<TKey, TValue>
        where TKey : struct, IEquatable<TKey>, IComparable<TKey>
        where TValue : struct
    {
#if !NET_DOTS
        UnsafeMultiHashMap<TKey, TValue> m_Target;

        public UnsafeMultiHashMapDebuggerTypeProxy(UnsafeMultiHashMap<TKey, TValue> target)
        {
            m_Target = target;
        }

        public static (NativeArray<TKey>, int) GetUniqueKeyArray(ref UnsafeMultiHashMap<TKey, TValue> hashMap, AllocatorManager.AllocatorHandle allocator)
        {
            var withDuplicates = hashMap.GetKeyArray(allocator);
            withDuplicates.Sort();
            int uniques = withDuplicates.Unique();
            return (withDuplicates, uniques);
        }

        public List<ListPair<TKey, List<TValue>>> Items
        {
            get
            {
                var result = new List<ListPair<TKey, List<TValue>>>();
                var keys = GetUniqueKeyArray(ref m_Target, Allocator.Temp);

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
}
