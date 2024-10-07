using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Collections.LowLevel.Unsafe
{
    internal static class UnsafeTextExtensions
    {
        public static ref UnsafeList<byte> AsUnsafeListOfBytes( this ref UnsafeText text )
        {
            return ref UnsafeUtility.As<UntypedUnsafeList, UnsafeList<byte>>(ref text.m_UntypedListData);
        }
    }

    /// <summary>
    /// An unmanaged, mutable, resizable UTF-8 string.
    /// </summary>
    /// <remarks>
    /// The string is always null-terminated, meaning a zero byte always immediately follows the last character.
    /// </remarks>
    [BurstCompatible]
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}, IsEmpty = {IsEmpty}")]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeText : INativeDisposable, IUTF8Bytes, INativeList<byte>
    {
        // NOTE! This Length is always > 0, because we have a null terminating byte.
        // We hide this byte from UnsafeText users.
        internal UntypedUnsafeList m_UntypedListData;

        /// <summary>
        /// Initializes and returns an instance of UnsafeText.
        /// </summary>
        /// <param name="capacity">The initial capacity, in bytes.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeText(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_UntypedListData = default;

            this.AsUnsafeListOfBytes() = new UnsafeList<byte>(capacity + 1, allocator);
            Length = 0;
        }

        /// <summary>
        /// Whether this string's character buffer has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>Whether this string's character buffer has been allocated (and not yet deallocated).</value>
        public bool IsCreated => this.AsUnsafeListOfBytes().IsCreated;


        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            this.AsUnsafeListOfBytes().Dispose();
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this string.
        /// </summary>
        /// <param name="inputDeps">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will dispose this string. The new job depends upon inputDeps.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return this.AsUnsafeListOfBytes().Dispose(inputDeps);
        }

        /// <summary>
        /// Reports whether container is empty.
        /// </summary>
        /// <value>True if the string is empty or the string has not been constructed.</value>
        public bool IsEmpty => !IsCreated || Length == 0;

        /// <summary>
        /// The byte at an index.
        /// </summary>
        /// <param name="index">A zero-based byte index.</param>
        /// <value>The byte at the index.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of bounds.</exception>
        public byte this[int index]
        {
            get
            {
                CheckIndexInRange(index);
                return UnsafeUtility.ReadArrayElement<byte>(m_UntypedListData.Ptr, index);
            }
            set
            {
                CheckIndexInRange(index);
                UnsafeUtility.WriteArrayElement(m_UntypedListData.Ptr, index, value);
            }
        }

        /// <summary>
        /// Returns a reference to the byte (not character) at an index.
        /// </summary>
        /// <remarks>
        /// Deallocating or reallocating this string's character buffer makes the reference invalid.
        /// </remarks>
        /// <param name="index">A byte index.</param>
        /// <returns>A reference to the byte at the index.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of bounds.</exception>
        public ref byte ElementAt(int index)
        {
            CheckIndexInRange(index);
            return ref UnsafeUtility.ArrayElementAsRef<byte>(m_UntypedListData.Ptr, index);
        }

        /// <summary>
        /// Sets the length to 0.
        /// </summary>
        public void Clear()
        {
            Length = 0;
        }

        /// <summary>
        /// Returns a pointer to this string's character buffer.
        /// </summary>
        /// <remarks>
        /// The pointer is made invalid by operations that reallocate the character buffer, such as setting <see cref="`Capacity`"/>.
        /// </remarks>
        /// <returns>A pointer to this string's character buffer.</returns>
        public byte* GetUnsafePtr()
        {
            return (byte*)m_UntypedListData.Ptr;
        }

        /// <summary>
        /// Attempt to set the length in bytes of this string.
        /// </summary>
        /// <param name="newLength">The new length in bytes of the string.</param>
        /// <param name="clearOptions">Whether any bytes added should be zeroed out.</param>
        /// <returns>Always true.</returns>
        public bool TryResize(int newLength, NativeArrayOptions clearOptions = NativeArrayOptions.ClearMemory)
        {
            // this can't ever fail, because if we can't resize malloc will abort
            this.AsUnsafeListOfBytes().Resize(newLength + 1, clearOptions);
            this.AsUnsafeListOfBytes()[newLength] = 0;
            return true;
        }

        /// <summary>
        /// The current capacity in bytes of this string.
        /// </summary>
        /// <remarks>
        /// The null-terminator byte is not included in the capacity, so the string's character buffer is `Capacity + 1` in size.
        /// </remarks>
        /// <value>The current capacity in bytes of the string.</value>
        public int Capacity
        {
            get => this.AsUnsafeListOfBytes().Capacity - 1;
            set
            {
                CheckCapacityInRange(value + 1, this.AsUnsafeListOfBytes().Length);
                this.AsUnsafeListOfBytes().SetCapacity(value + 1);
            }
        }

        /// <summary>
        /// The current length in bytes of this string.
        /// </summary>
        /// <remarks>
        /// The length does not include the null terminator byte.
        /// </remarks>
        /// <value>The current length in bytes of the UTF-8 encoded string.</value>
        public int Length
        {
            get => this.AsUnsafeListOfBytes().Length - 1;
            set
            {
                this.AsUnsafeListOfBytes().Resize(value + 1);
                this.AsUnsafeListOfBytes()[value] = 0;
            }
        }

        /// <summary>
        /// Returns a managed string copy of this string.
        /// </summary>
        /// <returns>A managed string copy of this string.</returns>
        [NotBurstCompatible]
        public override string ToString()
        {
            if (!IsCreated)
                return "";
            return this.ConvertToString();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckIndexInRange(int index)
        {
            if (index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be positive.");
            if (index >= Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range in UnsafeText of {Length} length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ThrowCopyError(CopyError error, string source)
        {
            throw new ArgumentException($"UnsafeText: {error} while copying \"{source}\"");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckCapacityInRange(int value, int length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value < (uint)length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }
    }
}
