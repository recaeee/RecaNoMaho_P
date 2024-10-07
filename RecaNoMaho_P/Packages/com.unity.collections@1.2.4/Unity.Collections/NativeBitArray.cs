using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Collections
{
    /// <summary>
    /// An arbitrarily-sized array of bits.
    /// </summary>
    /// <remarks>
    /// The number of allocated bytes is always a multiple of 8. For example, a 65-bit array could fit in 9 bytes, but its allocation is actually 16 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}, IsCreated = {IsCreated}")]
    [BurstCompatible]
    public unsafe struct NativeBitArray
        : INativeDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeBitArray>();

#if REMOVE_DISPOSE_SENTINEL
#else
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif

#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeBitArray m_BitArray;

        /// <summary>
        /// Initializes and returns an instance of NativeBitArray.
        /// </summary>
        /// <param name="numBits">The number of bits.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public NativeBitArray(int numBits, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            : this(numBits, allocator, options, 2)
        {
        }

        NativeBitArray(int numBits, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options, int disposeSentinelStackDepth)
        {
            CollectionHelper.CheckAllocator(allocator);
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

            CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Collections.NativeBitArray");
#endif
            m_BitArray = new UnsafeBitArray(numBits, allocator, options);
        }

        /// <summary>
        /// Whether this array has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this array has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_BitArray.IsCreated;

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

            m_BitArray.Dispose();
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this array.
        /// </summary>
        /// <param name="inputDeps">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will dispose this array. The new job depends upon inputDeps.</returns>
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
#endif
            var jobHandle = m_BitArray.Dispose(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif

            return jobHandle;
        }

        /// <summary>
        /// Returns the number of bits.
        /// </summary>
        /// <value>The number of bits.</value>
        public int Length
        {
            get
            {
                CheckRead();
                return CollectionHelper.AssumePositive(m_BitArray.Length);
            }
        }

        /// <summary>
        /// Sets all the bits to 0.
        /// </summary>
        public void Clear()
        {
            CheckWrite();
            m_BitArray.Clear();
        }

        /// <summary>
        /// Returns a native array that aliases the content of this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in the aliased array.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if the number of bits in this array
        /// is not evenly divisible by the size of T in bits (`sizeof(T) * 8`).</exception>
        /// <returns>A native array that aliases the content of this array.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public NativeArray<T> AsNativeArray<T>() where T : unmanaged
        {
            CheckReadBounds<T>();

            var bitsPerElement = UnsafeUtility.SizeOf<T>() * 8;
            var length = m_BitArray.Length / bitsPerElement;

            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_BitArray.Ptr, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.UseSecondaryVersion(ref m_Safety);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif
            return array;
        }

        /// <summary>
        /// Sets the bit at an index to 0 or 1.
        /// </summary>
        /// <param name="pos">Index of the bit to set.</param>
        /// <param name="value">True for 1, false for 0.</param>
        public void Set(int pos, bool value)
        {
            CheckWrite();
            m_BitArray.Set(pos, value);
        }

        /// <summary>
        /// Sets a range of bits to 0 or 1.
        /// </summary>
        /// <remarks>
        /// The range runs from index `pos` up to (but not including) `pos + numBits`.
        /// No exception is thrown if `pos + numBits` exceeds the length.
        /// </remarks>
        /// <param name="pos">Index of the first bit to set.</param>
        /// <param name="value">True for 1, false for 0.</param>
        /// <param name="numBits">Number of bits to set.</param>
        /// <exception cref="ArgumentException">Thrown if pos is out of bounds or if numBits is less than 1.</exception>
        public void SetBits(int pos, bool value, int numBits)
        {
            CheckWrite();
            m_BitArray.SetBits(pos, value, numBits);
        }

        /// <summary>
        /// Copies bits of a ulong to bits in this array.
        /// </summary>
        /// <remarks>
        /// The destination bits in this array run from index pos up to (but not including) `pos + numBits`.
        /// No exception is thrown if `pos + numBits` exceeds the length.
        ///
        /// The lowest bit of the ulong is copied to the first destination bit; the second-lowest bit of the ulong is
        /// copied to the second destination bit; and so forth.
        /// </remarks>
        /// <param name="pos">Index of the first bit to set.</param>
        /// <param name="value">Unsigned long from which to copy bits.</param>
        /// <param name="numBits">Number of bits to set (must be between 1 and 64).</param>
        /// <exception cref="ArgumentException">Thrown if pos is out of bounds or if numBits is not between 1 and 64.</exception>
        public void SetBits(int pos, ulong value, int numBits = 1)
        {
            CheckWrite();
            m_BitArray.SetBits(pos, value, numBits);
        }

        /// <summary>
        /// Returns a ulong which has bits copied from this array.
        /// </summary>
        /// <remarks>
        /// The source bits in this array run from index pos up to (but not including) `pos + numBits`.
        /// No exception is thrown if `pos + numBits` exceeds the length.
        ///
        /// The first source bit is copied to the lowest bit of the ulong; the second source bit is copied to the second-lowest bit of the ulong; and so forth. Any remaining bits in the ulong will be 0.
        /// </remarks>
        /// <param name="pos">Index of the first bit to get.</param>
        /// <param name="numBits">Number of bits to get (must be between 1 and 64).</param>
        /// <exception cref="ArgumentException">Thrown if pos is out of bounds or if numBits is not between 1 and 64.</exception>
        /// <returns>A ulong which has bits copied from this array.</returns>
        public ulong GetBits(int pos, int numBits = 1)
        {
            CheckRead();
            return m_BitArray.GetBits(pos, numBits);
        }

        /// <summary>
        /// Returns true if the bit at an index is 1.
        /// </summary>
        /// <param name="pos">Index of the bit to test.</param>
        /// <returns>True if the bit at the index is 1.</returns>
        /// <exception cref="ArgumentException">Thrown if `pos` is out of bounds.</exception>
        public bool IsSet(int pos)
        {
            CheckRead();
            return m_BitArray.IsSet(pos);
        }

        /// <summary>
        /// Copies a range of bits from this array to another range in this array.
        /// </summary>
        /// <remarks>
        /// The bits to copy run from index `srcPos` up to (but not including) `srcPos + numBits`.
        /// The bits to set run from index `dstPos` up to (but not including) `dstPos + numBits`.
        ///
        /// The ranges may overlap, but the result in the overlapping region is undefined.
        /// </remarks>
        /// <param name="dstPos">Index of the first bit to set.</param>
        /// <param name="srcPos">Index of the first bit to copy.</param>
        /// <param name="numBits">Number of bits to copy.</param>
        /// <exception cref="ArgumentException">Thrown if either `dstPos + numBits` or `srcPos + numBits` exceed the length of this array.</exception>
        public void Copy(int dstPos, int srcPos, int numBits)
        {
            CheckWrite();
            m_BitArray.Copy(dstPos, srcPos, numBits);
        }

        /// <summary>
        /// Copies a range of bits from an array to a range of bits in this array.
        /// </summary>
        /// <remarks>
        /// The bits to copy in the source array run from index srcPos up to (but not including) `srcPos + numBits`.
        /// The bits to set in the destination array run from index dstPos up to (but not including) `dstPos + numBits`.
        ///
        /// When the source and destination are the same array, the ranges may still overlap, but the result in the overlapping region is undefined.
        /// </remarks>
        /// <param name="dstPos">Index of the first bit to set.</param>
        /// <param name="srcBitArray">The source array.</param>
        /// <param name="srcPos">Index of the first bit to copy.</param>
        /// <param name="numBits">The number of bits to copy.</param>
        /// <exception cref="ArgumentException">Thrown if either `dstPos + numBits` or `srcBitArray + numBits` exceed the length of this array.</exception>
        public void Copy(int dstPos, ref NativeBitArray srcBitArray, int srcPos, int numBits)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(srcBitArray.m_Safety);
#endif
            CheckWrite();
            m_BitArray.Copy(dstPos, ref srcBitArray.m_BitArray, srcPos, numBits);
        }

        /// <summary>
        /// Finds the first length-*N* contiguous sequence of 0 bits in this bit array.
        /// </summary>
        /// <param name="pos">Index at which to start searching.</param>
        /// <param name="numBits">Number of contiguous 0 bits to look for.</param>
        /// <returns>The index in this array where the sequence is found. The index will be greater than or equal to `pos`.
        /// Returns -1 if no occurrence is found.</returns>
        public int Find(int pos, int numBits)
        {
            CheckRead();
            return m_BitArray.Find(pos, numBits);
        }

        /// <summary>
        /// Finds the first length-*N* contiguous sequence of 0 bits in this bit array. Searches only a subsection.
        /// </summary>
        /// <param name="pos">Index at which to start searching.</param>
        /// <param name="numBits">Number of contiguous 0 bits to look for.</param>
        /// <param name="count">Number of bits to search.</param>
        /// <returns>The index in this array where the sequence is found. The index will be greater than or equal to `pos` but less than `pos + count`.
        /// Returns -1 if no occurrence is found.</returns>
        public int Find(int pos, int count, int numBits)
        {
            CheckRead();
            return m_BitArray.Find(pos, count, numBits);
        }

        /// <summary>
        /// Returns true if none of the bits in a range are 1 (*i.e.* all bits in the range are 0).
        /// </summary>
        /// <param name="pos">Index of the bit at which to start searching.</param>
        /// <param name="numBits">Number of bits to test. Defaults to 1.</param>
        /// <returns>Returns true if none of the bits in range `pos` up to (but not including) `pos + numBits` are 1.</returns>
        /// <exception cref="ArgumentException">Thrown if `pos` is out of bounds or `numBits` is less than 1.</exception>
        public bool TestNone(int pos, int numBits = 1)
        {
            CheckRead();
            return m_BitArray.TestNone(pos, numBits);
        }

        /// <summary>
        /// Returns true if at least one of the bits in a range is 1.
        /// </summary>
        /// <param name="pos">Index of the bit at which to start searching.</param>
        /// <param name="numBits">Number of bits to test. Defaults to 1.</param>
        /// <returns>True if one ore more of the bits in range `pos` up to (but not including) `pos + numBits` are 1.</returns>
        /// <exception cref="ArgumentException">Thrown if `pos` is out of bounds or `numBits` is less than 1.</exception>
        public bool TestAny(int pos, int numBits = 1)
        {
            CheckRead();
            return m_BitArray.TestAny(pos, numBits);
        }

        /// <summary>
        /// Returns true if all of the bits in a range are 1.
        /// </summary>
        /// <param name="pos">Index of the bit at which to start searching.</param>
        /// <param name="numBits">Number of bits to test. Defaults to 1.</param>
        /// <returns>True if all of the bits in range `pos` up to (but not including) `pos + numBits` are 1.</returns>
        /// <exception cref="ArgumentException">Thrown if `pos` is out of bounds or `numBits` is less than 1.</exception>
        public bool TestAll(int pos, int numBits = 1)
        {
            CheckRead();
            return m_BitArray.TestAll(pos, numBits);
        }

        /// <summary>
        /// Returns the number of bits in a range that are 1.
        /// </summary>
        /// <param name="pos">Index of the bit at which to start searching.</param>
        /// <param name="numBits">Number of bits to test. Defaults to 1.</param>
        /// <returns>The number of bits in a range of bits that are 1.</returns>
        /// <exception cref="ArgumentException">Thrown if `pos` is out of bounds or `numBits` is less than 1.</exception>
        public int CountBits(int pos, int numBits = 1)
        {
            CheckRead();
            return m_BitArray.CountBits(pos, numBits);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckReadBounds<T>() where T : unmanaged
        {
            CheckRead();

            var bitsPerElement = UnsafeUtility.SizeOf<T>() * 8;
            var length = m_BitArray.Length / bitsPerElement;

            if (length == 0)
            {
                throw new InvalidOperationException($"Number of bits in the NativeBitArray {m_BitArray.Length} is not sufficient to cast to NativeArray<T> {UnsafeUtility.SizeOf<T>() * 8}.");
            }
            else if (m_BitArray.Length != bitsPerElement* length)
            {
                throw new InvalidOperationException($"Number of bits in the NativeBitArray {m_BitArray.Length} couldn't hold multiple of T {UnsafeUtility.SizeOf<T>()}. Output array would be truncated.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// Unsafe helper methods for NativeBitArray.
    /// </summary>
    [BurstCompatible]
    public static class NativeBitArrayUnsafeUtility
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Returns an array's atomic safety handle.
        /// </summary>
        /// <param name="container">Array from which to get an AtomicSafetyHandle.</param>
        /// <returns>This array's atomic safety handle.</returns>
        [BurstCompatible(RequiredUnityDefine = "ENABLE_UNITY_COLLECTIONS_CHECKS", CompileTarget = BurstCompatibleAttribute.BurstCompatibleCompileTarget.Editor)]
        public static AtomicSafetyHandle GetAtomicSafetyHandle(in NativeBitArray container)
        {
            return container.m_Safety;
        }

        /// <summary>
        /// Sets an array's atomic safety handle.
        /// </summary>
        /// <param name="container">Array which the AtomicSafetyHandle is for.</param>
        /// <param name="safety">Atomic safety handle for this array.</param>
        [BurstCompatible(RequiredUnityDefine = "ENABLE_UNITY_COLLECTIONS_CHECKS", CompileTarget = BurstCompatibleAttribute.BurstCompatibleCompileTarget.Editor)]
        public static void SetAtomicSafetyHandle(ref NativeBitArray container, AtomicSafetyHandle safety)
        {
            container.m_Safety = safety;
        }
#endif

        /// <summary>
        /// Returns a bit array with content aliasing a buffer.
        /// </summary>
        /// <param name="ptr">A buffer.</param>
        /// <param name="sizeInBytes">Size of the buffer in bytes. Must be a multiple of 8.</param>
        /// <param name="allocator">The allocator that was used to create the buffer.</param>
        /// <returns>A bit array with content aliasing a buffer.</returns>
        public static unsafe NativeBitArray ConvertExistingDataToNativeBitArray(void* ptr, int sizeInBytes, AllocatorManager.AllocatorHandle allocator)
        {
            return new NativeBitArray
            {
                m_BitArray = new UnsafeBitArray(ptr, sizeInBytes, allocator),
            };
        }
    }
}
