//#define USE_NOT_BURST_COMPATIBLE_EXTENSIONS

using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections.NotBurstCompatible
{
    /// <summary>
    /// Provides some extension methods for various collections.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Returns a new managed array with all the elements copied from a set.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="set">The set whose elements are copied to the array.</param>
        /// <returns>A new managed array with all the elements copied from a set.</returns>
        [NotBurstCompatible]
        public static T[] ToArray<T>(this NativeHashSet<T> set)
            where T : unmanaged, IEquatable<T>
        {
            var array = set.ToNativeArray(Allocator.TempJob);
            var managed = array.ToArray();
            array.Dispose();
            return managed;
        }

        /// <summary>
        /// Returns a new managed array which is a copy of this list.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="list">The list to copy.</param>
        /// <returns>A new managed array which is a copy of this list.</returns>
        [NotBurstCompatible]
        public static T[] ToArrayNBC<T>(this NativeList<T> list)
            where T : unmanaged
        {
            return list.AsArray().ToArray();
        }

        /// <summary>
        /// Clears this list and then copies all the elements of an array to this list.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="list">This list.</param>
        /// <param name="array">The managed array to copy from.</param>
        [NotBurstCompatible]
        public static void CopyFromNBC<T>(this NativeList<T> list, T[] array)
            where T : unmanaged
        {
            list.Clear();
            list.Resize(array.Length, NativeArrayOptions.UninitializedMemory);
            NativeArray<T> na = list.AsArray();
            na.CopyFrom(array);
        }

#if !NET_DOTS // Tuple is not supported by TinyBCL
        /// <summary>
        /// Returns an array with the unique keys of this multi hash map.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys.</typeparam>
        /// <typeparam name="TValue">The type of the values.</typeparam>
        /// <param name="hashmap">The multi hash map.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with the unique keys of this multi hash map.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(int) })]
        [Obsolete("Burst now supports tuple, please use `GetUniqueKeyArray` method from `Unity.Collections.UnsafeMultiHashMap` instead.", false)]
        public static (NativeArray<TKey>, int) GetUniqueKeyArrayNBC<TKey, TValue>(this UnsafeMultiHashMap<TKey, TValue> hashmap, AllocatorManager.AllocatorHandle allocator)
        where TKey : struct, IEquatable<TKey>, IComparable<TKey>
        where TValue : struct => hashmap.GetUniqueKeyArray(allocator);

        /// <summary>
        /// Returns an array with the unique keys of this multi hash map.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys.</typeparam>
        /// <typeparam name="TValue">The type of the values.</typeparam>
        /// <param name="hashmap">The multi hash map.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with the unique keys of this multi hash map.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(int) })]
        [Obsolete("Burst now supports tuple, please use `GetUniqueKeyArray` method from `Unity.Collections.NativeMultiHashMap` instead.", false)]
        public static (NativeArray<TKey>, int) GetUniqueKeyArrayNBC<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> hashmap, AllocatorManager.AllocatorHandle allocator)
            where TKey : struct, IEquatable<TKey>, IComparable<TKey>
            where TValue : struct => hashmap.GetUniqueKeyArray(allocator);
#endif
    }
}
