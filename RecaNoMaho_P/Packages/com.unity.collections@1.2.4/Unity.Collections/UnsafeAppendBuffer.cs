using System;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// An unmanaged, untyped, heterogeneous buffer.
    /// </summary>
    /// <remarks>
    /// The values written to an individual append buffer can be of different types.
    /// </remarks>
    [BurstCompatible]
    public unsafe struct UnsafeAppendBuffer
        : INativeDisposable
    {
        /// <summary>
        /// The internal buffer where the content is stored.
        /// </summary>
        /// <value>The internal buffer where the content is stored.</value>
        [NativeDisableUnsafePtrRestriction]
        public byte* Ptr;

        /// <summary>
        /// The size in bytes of the currently-used portion of the internal buffer.
        /// </summary>
        /// <value>The size in bytes of the currently-used portion of the internal buffer.</value>
        public int Length;

        /// <summary>
        /// The size in bytes of the internal buffer.
        /// </summary>
        /// <value>The size in bytes of the internal buffer.</value>
        public int Capacity;

        /// <summary>
        /// The allocator used to create the internal buffer.
        /// </summary>
        /// <value>The allocator used to create the internal buffer.</value>
        public AllocatorManager.AllocatorHandle Allocator;

        /// <summary>
        /// The byte alignment used when allocating the internal buffer.
        /// </summary>
        /// <value>The byte alignment used when allocating the internal buffer. Is always a non-zero power of 2.</value>
        public readonly int Alignment;

        /// <summary>
        /// Initializes and returns an instance of UnsafeAppendBuffer.
        /// </summary>
        /// <param name="initialCapacity">The initial allocation size in bytes of the internal buffer.</param>
        /// <param name="alignment">The byte alignment of the allocation. Must be a non-zero power of 2.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeAppendBuffer(int initialCapacity, int alignment, AllocatorManager.AllocatorHandle allocator)
        {
            CheckAlignment(alignment);

            Alignment = alignment;
            Allocator = allocator;
            Ptr = null;
            Length = 0;
            Capacity = 0;

            SetCapacity(initialCapacity);
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafeAppendBuffer that aliases an existing buffer.
        /// </summary>
        /// <remarks>The capacity will be set to `length`, and <see cref="Length"/> will be set to 0.
        /// </remarks>
        /// <param name="ptr">The buffer to alias.</param>
        /// <param name="length">The length in bytes of the buffer.</param>
        public UnsafeAppendBuffer(void* ptr, int length)
        {
            Alignment = 0;
            Allocator = AllocatorManager.None;
            Ptr = (byte*)ptr;
            Length = 0;
            Capacity = length;
        }

        /// <summary>
        /// Whether the append buffer is empty.
        /// </summary>
        /// <value>True if the append buffer is empty.</value>
        public bool IsEmpty => Length == 0;

        /// <summary>
        /// Whether this append buffer has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this append buffer has been allocated (and not yet deallocated).</value>
        public bool IsCreated => Ptr != null;

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            if (CollectionHelper.ShouldDeallocate(Allocator))
            {
                Memory.Unmanaged.Free(Ptr, Allocator);
                Allocator = AllocatorManager.Invalid;
            }

            Ptr = null;
            Length = 0;
            Capacity = 0;
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this append buffer.
        /// </summary>
        /// <param name="inputDeps">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will dispose this append buffer. The new job depends upon inputDeps.</returns>
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
        public void Reset()
        {
            Length = 0;
        }

        /// <summary>
        /// Sets the size in bytes of the internal buffer.
        /// </summary>
        /// <remarks>Does nothing if the new capacity is less than or equal to the current capacity.</remarks>
        /// <param name="capacity">A new capacity in bytes.</param>
        public void SetCapacity(int capacity)
        {
            if (capacity <= Capacity)
            {
                return;
            }

            capacity = math.max(64, math.ceilpow2(capacity));

            var newPtr = (byte*)Memory.Unmanaged.Allocate(capacity, Alignment, Allocator);
            if (Ptr != null)
            {
                UnsafeUtility.MemCpy(newPtr, Ptr, Length);
                Memory.Unmanaged.Free(Ptr, Allocator);
            }

            Ptr = newPtr;
            Capacity = capacity;
        }

        /// <summary>
        /// Sets the length in bytes.
        /// </summary>
        /// <remarks>If the new length exceeds the capacity, capacity is expanded to the new length.</remarks>
        /// <param name="length">The new length.</param>
        public void ResizeUninitialized(int length)
        {
            SetCapacity(length);
            Length = length;
        }

        /// <summary>
        /// Appends an element to the end of this append buffer.
        /// </summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="value">The value to be appended.</param>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public void Add<T>(T value) where T : struct
        {
            var structSize = UnsafeUtility.SizeOf<T>();

            SetCapacity(Length + structSize);
            UnsafeUtility.CopyStructureToPtr(ref value, Ptr + Length);
            Length += structSize;
        }

        /// <summary>
        /// Appends an element to the end of this append buffer.
        /// </summary>
        /// <remarks>The value itself is stored, not the pointer.</remarks>
        /// <param name="ptr">A pointer to the value to be appended.</param>
        /// <param name="structSize">The size in bytes of the value to be appended.</param>
        public void Add(void* ptr, int structSize)
        {
            SetCapacity(Length + structSize);
            UnsafeUtility.MemCpy(Ptr + Length, ptr, structSize);
            Length += structSize;
        }

        /// <summary>
        /// Appends the elements of a buffer to the end of this append buffer.
        /// </summary>
        /// <typeparam name="T">The type of the buffer's elements.</typeparam>
        /// <remarks>The values themselves are stored, not their pointers.</remarks>
        /// <param name="ptr">A pointer to the buffer whose values will be appended.</param>
        /// <param name="length">The number of elements to append.</param>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public void AddArray<T>(void* ptr, int length) where T : struct
        {
            Add(length);

            if (length != 0)
                Add(ptr, length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Appends all elements of an array to the end of this append buffer.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="value">The array whose elements will all be appended.</param>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public void Add<T>(NativeArray<T> value) where T : struct
        {
            Add(value.Length);
            Add(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(value), UnsafeUtility.SizeOf<T>() * value.Length);
        }

        /// <summary>
        /// Appends the content of a string as UTF-16 to the end of this append buffer.
        /// </summary>
        /// <remarks>Because some Unicode characters require two chars in UTF-16, each character is written as one or two chars (two or four bytes).
        ///
        /// The length of the string is itself appended before adding the first character. If the string is null, appends the int `-1` but no character data.
        ///
        /// A null terminator is not appended after the character data.</remarks>
        /// <param name="value">The string to append.</param>
        [NotBurstCompatible /* Deprecated */]
        [Obsolete("Please use `AddNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace instead. (RemovedAfter 2021-06-22)", false)]
        public void Add(string value) => NotBurstCompatible.Extensions.AddNBC(ref this, value);

        /// <summary>
        /// Removes and returns the last element of this append buffer.
        /// </summary>
        /// <typeparam name="T">The type of the element to remove.</typeparam>
        /// <remarks>It is your responsibility to specify the correct type. Do not pop when the append buffer is empty.</remarks>
        /// <returns>The element removed from the end of this append buffer.</returns>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public T Pop<T>() where T : struct
        {
            int structSize = UnsafeUtility.SizeOf<T>();
            long ptr = (long)Ptr;
            long size = Length;
            long addr = ptr + size - structSize;

            var data = UnsafeUtility.ReadArrayElement<T>((void*)addr, 0);
            Length -= structSize;
            return data;
        }

        /// <summary>
        /// Removes and copies the last element of this append buffer.
        /// </summary>
        /// <remarks>It is your responsibility to specify the correct `structSize`. Do not pop when the append buffer is empty.</remarks>
        /// <param name="ptr">The location to which the removed element will be copied.</param>
        /// <param name="structSize">The size of the element to remove and copy.</param>
        public void Pop(void* ptr, int structSize)
        {
            long data = (long)Ptr;
            long size = Length;
            long addr = data + size - structSize;

            UnsafeUtility.MemCpy(ptr, (void*)addr, structSize);
            Length -= structSize;
        }

        /// <summary>
        /// Copies this append buffer to a managed array of bytes.
        /// </summary>
        /// <returns>A managed array of bytes.</returns>
        [NotBurstCompatible /* Deprecated */]
        [Obsolete("Please use `ToBytesNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace instead. (RemovedAfter 2021-06-22)", false)]
        public byte[] ToBytes() => NotBurstCompatible.Extensions.ToBytesNBC(ref this);

        /// <summary>
        /// Returns a reader for this append buffer.
        /// </summary>
        /// <returns>A reader for the append buffer.</returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// A reader for UnsafeAppendBuffer.
        /// </summary>
        [BurstCompatible]
        public unsafe struct Reader
        {
            /// <summary>
            /// The internal buffer where the content is stored.
            /// </summary>
            /// <value>The internal buffer where the content is stored.</value>
            public readonly byte* Ptr;

            /// <summary>
            /// The length in bytes of the append buffer's content.
            /// </summary>
            /// <value>The length in bytes of the append buffer's content.</value>
            public readonly int Size;

            /// <summary>
            /// The location of the next read (expressed as a byte offset from the start).
            /// </summary>
            /// <value>The location of the next read (expressed as a byte offset from the start).</value>
            public int Offset;

            /// <summary>
            /// Initializes and returns an instance of UnsafeAppendBuffer.Reader.
            /// </summary>
            /// <param name="buffer">A reference to the append buffer to read.</param>
            public Reader(ref UnsafeAppendBuffer buffer)
            {
                Ptr = buffer.Ptr;
                Size = buffer.Length;
                Offset = 0;
            }

            /// <summary>
            /// Initializes and returns an instance of UnsafeAppendBuffer.Reader that reads from a buffer.
            /// </summary>
            /// <remarks>The buffer will be read *as if* it is an UnsafeAppendBuffer whether it was originally allocated as one or not.</remarks>
            /// <param name="ptr">The buffer to read as an UnsafeAppendBuffer.</param>
            /// <param name="length">The length in bytes of the </param>
            public Reader(void* ptr, int length)
            {
                Ptr = (byte*)ptr;
                Size = length;
                Offset = 0;
            }

            /// <summary>
            /// Whether the offset has advanced past the last of the append buffer's content.
            /// </summary>
            /// <value>Whether the offset has advanced past the last of the append buffer's content.</value>
            public bool EndOfBuffer => Offset == Size;

            /// <summary>
            /// Reads an element from the append buffer.
            /// </summary>
            /// <remarks>Advances the reader's offset by the size of T.</remarks>
            /// <typeparam name="T">The type of element to read.</typeparam>
            /// <param name="value">Output for the element read.</param>
            [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
            public void ReadNext<T>(out T value) where T : struct
            {
                var structSize = UnsafeUtility.SizeOf<T>();
                CheckBounds(structSize);

                UnsafeUtility.CopyPtrToStructure<T>(Ptr + Offset, out value);
                Offset += structSize;
            }

            /// <summary>
            /// Reads an element from the append buffer.
            /// </summary>
            /// <remarks>Advances the reader's offset by the size of T.</remarks>
            /// <typeparam name="T">The type of element to read.</typeparam>
            /// <returns>The element read.</returns>
            [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
            public T ReadNext<T>() where T : struct
            {
                var structSize = UnsafeUtility.SizeOf<T>();
                CheckBounds(structSize);

                T value = UnsafeUtility.ReadArrayElement<T>(Ptr + Offset, 0);
                Offset += structSize;
                return value;
            }

            /// <summary>
            /// Reads an element from the append buffer.
            /// </summary>
            /// <remarks>Advances the reader's offset by `structSize`.</remarks>
            /// <param name="structSize">The size of the element to read.</param>
            /// <returns>A pointer to where the read element resides in the append buffer.</returns>
            public void* ReadNext(int structSize)
            {
                CheckBounds(structSize);

                var value = (void*)((IntPtr)Ptr + Offset);
                Offset += structSize;
                return value;
            }

            /// <summary>
            /// Reads an element from the append buffer.
            /// </summary>
            /// <remarks>Advances the reader's offset by the size of T.</remarks>
            /// <typeparam name="T">The type of element to read.</typeparam>
            /// <param name="value">Outputs a new array with length of 1. The read element is copied to the single index of this array.</param>
            /// <param name="allocator">The allocator to use.</param>
            [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
            public void ReadNext<T>(out NativeArray<T> value, AllocatorManager.AllocatorHandle allocator) where T : struct
            {
                var length = ReadNext<int>();
                value = CollectionHelper.CreateNativeArray<T>(length, allocator);
                var size = length * UnsafeUtility.SizeOf<T>();
                if (size > 0)
                {
                    var ptr = ReadNext(size);
                    UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(value), ptr, size);
                }
            }

            /// <summary>
            /// Reads an array from the append buffer.
            /// </summary>
            /// <remarks>An array stored in the append buffer starts with an int specifying the number of values in the array.
            /// The first element of an array immediately follows this int.
            ///
            /// Advances the reader's offset by the size of the array (plus an int).</remarks>
            /// <typeparam name="T">The type of elements in the array to read.</typeparam>
            /// <param name="length">Output which is the number of elements in the read array.</param>
            /// <returns>A pointer to where the first element of the read array resides in the append buffer.</returns>
            [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
            public void* ReadNextArray<T>(out int length) where T : struct
            {
                length = ReadNext<int>();
                return (length == 0) ? null : ReadNext(length * UnsafeUtility.SizeOf<T>());
            }

#if !NET_DOTS
            /// <summary>
            /// Reads a UTF-16 string from the append buffer.
            /// </summary>
            /// <remarks>Because some Unicode characters require two chars in UTF-16, each character is either one or two chars (two or four bytes).
            ///
            /// Assumes the string does not have a null terminator.
            ///
            /// Advances the reader's offset by the size of the string (in bytes).</remarks>
            /// <param name="value">Outputs the string read from the append buffer.</param>
            [NotBurstCompatible /* Deprecated */]
            [Obsolete("Please use `ReadNextNBC` from `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible` namespace instead. (RemovedAfter 2021-06-22)", false)]
            public void ReadNext(out string value) => NotBurstCompatible.Extensions.ReadNextNBC(ref this, out value);
#endif

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckBounds(int structSize)
            {
                if (Offset + structSize > Size)
                {
                    throw new ArgumentException($"Requested value outside bounds of UnsafeAppendOnlyBuffer. Remaining bytes: {Size - Offset} Requested: {structSize}");
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckAlignment(int alignment)
        {
            var zeroAlignment = alignment == 0;
            var powTwoAlignment = ((alignment - 1) & alignment) == 0;
            var validAlignment = (!zeroAlignment) && powTwoAlignment;

            if (!validAlignment)
            {
                throw new ArgumentException($"Specified alignment must be non-zero positive power of two. Requested: {alignment}");
            }
        }
    }
}
