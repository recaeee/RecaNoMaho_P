using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using static Unity.Collections.AllocatorManager;

namespace Unity.Collections
{
    /// <summary>
    /// Extension methods for NativeArray.
    /// </summary>
    [BurstCompatible]
    public unsafe static class NativeArrayExtensions
    {
        public struct NativeArrayStaticId<T>
            where T : struct
        {
            internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeArray<T>>();
        }

        /// <summary>
        /// Returns true if a particular value is present in this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in this array.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in this array.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this NativeArray<T> array, U value) where T : struct, IEquatable<U>
        {
            return IndexOf<T, U>(array.GetUnsafeReadOnlyPtr(), array.Length, value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in this array.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>The index of the first occurrence of the value in this array. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this NativeArray<T> array, U value) where T : struct, IEquatable<U>
        {
            return IndexOf<T, U>(array.GetUnsafeReadOnlyPtr(), array.Length, value);
        }

        /// <summary>
        /// Returns true if a particular value is present in this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in this array.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in this array.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this NativeArray<T>.ReadOnly array, U value) where T : struct, IEquatable<U>
        {
            return IndexOf<T, U>(array.m_Buffer, array.m_Length, value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in this array.</typeparam>
        /// <typeparam name="U">The type of value to locate.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>The index of the first occurrence of the value in this array. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this NativeArray<T>.ReadOnly array, U value) where T : struct, IEquatable<U>
        {
            return IndexOf<T, U>(array.m_Buffer, array.m_Length, value);
        }

        /// <summary>
        /// Returns true if a particular value is present in this list.
        /// </summary>
        /// <typeparam name="T">The type of elements in this list.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in this list.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this NativeList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return IndexOf<T, U>(list.GetUnsafeReadOnlyPtr(), list.Length, value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in this list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="list">The list to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>The index of the first occurrence of the value in this list. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this NativeList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return IndexOf<T, U>(list.GetUnsafeReadOnlyPtr(), list.Length, value);
        }

        /// <summary>
        /// Returns true if a particular value is present in a buffer.
        /// </summary>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="ptr">The buffer.</param>
        /// <param name="length">Number of elements in the buffer.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in the buffer.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(void* ptr, int length, U value) where T : struct, IEquatable<U>
        {
            return IndexOf<T, U>(ptr, length, value) != -1;
        }

        /// <summary>
        /// Finds the index of the first occurrence of a particular value in a buffer.
        /// </summary>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="ptr">A buffer.</param>
        /// <param name="length">Number of elements in the buffer.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>The index of the first occurrence of the value in the buffer. Returns -1 if no occurrence is found.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(void* ptr, int length, U value) where T : struct, IEquatable<U>
        {
            for (int i = 0; i != length; i++)
            {
                if (UnsafeUtility.ReadArrayElement<T>(ptr, i).Equals(value))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the reinterpretation of this array into another kind of NativeArray.
        /// See [Array reinterpretation](https://docs.unity3d.com/Packages/com.unity.collections@latest?subfolder=/manual/allocation.html#array-reinterpretation).
        /// </summary>
        /// <param name="array">The array to reinterpret.</param>
        /// <typeparam name="T">Type of elements in the array.</typeparam>
        /// <typeparam name="U">Type of elements in the reinterpreted array.</typeparam>
        /// <returns>The reinterpretation of this array into another kind of NativeArray.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this array's capacity cannot be evenly divided by `sizeof(U)`.</exception>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static NativeArray<U> Reinterpret<T, U>(this NativeArray<T> array) where U : struct where T : struct
        {
            var tSize = UnsafeUtility.SizeOf<T>();
            var uSize = UnsafeUtility.SizeOf<U>();

            var byteLen = ((long)array.Length) * tSize;
            var uLen = byteLen / uSize;

            CheckReinterpretSize<T, U>(ref array);

            var ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(ptr, (int)uLen, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var handle = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(array);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, handle);
#endif

            return result;
        }

        /// <summary>
        /// Returns true if this array and another have equal length and content.
        /// </summary>
        /// <typeparam name="T">The type of the source array's elements.</typeparam>
        /// <param name="array">The array to compare for equality.</param>
        /// <param name="other">The other array to compare for equality.</param>
        /// <returns>True if the arrays have equal length and content.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static bool ArraysEqual<T>(this NativeArray<T> array, NativeArray<T> other) where T : struct, IEquatable<T>
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

        /// <summary>
        /// Returns true if this array and another have equal length and content.
        /// </summary>
        /// <typeparam name="T">The type of the source array's elements.</typeparam>
        /// <param name="array">The array to compare for equality.</param>
        /// <param name="other">The other array to compare for equality.</param>
        /// <returns>True if the arrays have equal length and content.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public static bool ArraysEqual<T>(this NativeList<T> array, NativeArray<T> other) where T : unmanaged, IEquatable<T>
        {
            return ArraysEqual(array.AsArray(), other);
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckReinterpretSize<T, U>(ref NativeArray<T> array) where U : struct where T : struct
        {
            var tSize = UnsafeUtility.SizeOf<T>();
            var uSize = UnsafeUtility.SizeOf<U>();

            var byteLen = ((long)array.Length) * tSize;
            var uLen = byteLen / uSize;

            if (uLen * uSize != byteLen)
            {
                throw new InvalidOperationException($"Types {typeof(T)} (array length {array.Length}) and {typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            }
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        internal static void Initialize<T>(ref this NativeArray<T> array,
                                            int length,
                                            AllocatorManager.AllocatorHandle allocator,
                                            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where T : struct
        {
            AllocatorHandle handle = allocator;
            array.m_Buffer = handle.AllocateStruct(default(T), length);
            array.m_Length = length;
            array.m_AllocatorLabel = Allocator.None;
            if (options == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(array.m_Buffer, array.m_Length * UnsafeUtility.SizeOf<T>());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, handle.ToAllocator);
            DisposeSentinel.Clear(ref array.m_DisposeSentinel);

            CollectionHelper.SetStaticSafetyId<NativeArray<T>>(ref array.m_Safety, ref NativeArrayStaticId<T>.s_staticSafetyId.Data);
            handle.AddSafetyHandle(array.m_Safety);
#endif
        }

        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(AllocatorManager.AllocatorHandle) })]
        internal static void Initialize<T, U>(ref this NativeArray<T> array,
                                                int length,
                                                ref U allocator,
                                                NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            where T : struct
            where U : unmanaged, AllocatorManager.IAllocator
        {
            array.m_Buffer = allocator.AllocateStruct(default(T), length);
            array.m_Length = length;
            array.m_AllocatorLabel = Allocator.None;
            if (options == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(array.m_Buffer, array.m_Length * UnsafeUtility.SizeOf<T>());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
            DisposeSentinel.Create(out array.m_Safety, out array.m_DisposeSentinel, 1, allocator.ToAllocator);
            DisposeSentinel.Clear(ref array.m_DisposeSentinel);

            CollectionHelper.SetStaticSafetyId<NativeArray<T>>(ref array.m_Safety, ref NativeArrayStaticId<T>.s_staticSafetyId.Data);
            allocator.Handle.AddSafetyHandle(array.m_Safety);
#endif
        }
    }
}
