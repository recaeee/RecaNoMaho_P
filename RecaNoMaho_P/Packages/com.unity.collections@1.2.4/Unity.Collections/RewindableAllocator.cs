using AOT;
using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Collections
{
    struct Spinner
    {
        int m_value;
        public void Lock()
        {
            while (0 != Interlocked.CompareExchange(ref m_value, 1, 0))
            {
            }
            Interlocked.MemoryBarrier();
        }
        public void Unlock()
        {
            Interlocked.MemoryBarrier();
            while (1 != Interlocked.CompareExchange(ref m_value, 0, 1))
            {
            }
        }
    }

    internal struct UnmanagedArray<T> : IDisposable where T : unmanaged
    {
        IntPtr m_pointer;
        int m_length;
        AllocatorManager.AllocatorHandle m_allocator;
        public UnmanagedArray(int length, AllocatorManager.AllocatorHandle allocator)
        {
            unsafe
            {
                m_pointer = (IntPtr)Memory.Unmanaged.Array.Allocate<T>(length, allocator);
            }
            m_length = length;
            m_allocator = allocator;
        }
        public void Dispose()
        {
            unsafe
            {
                Memory.Unmanaged.Free((T*)m_pointer, Allocator.Persistent);
            }
        }
        public unsafe T* GetUnsafePointer()
        {
            return (T*)m_pointer;
        }
        public ref T this[int index]
        {
            get { unsafe { return ref ((T*)m_pointer)[index]; } }
        }
    }

    /// <summary>
    /// An allocator that is fast like a linear allocator, is threadsafe, and automatically invalidates
    /// all allocations made from it, when "rewound" by the user.
    /// </summary>
    [BurstCompile]
    public struct RewindableAllocator : AllocatorManager.IAllocator
    {
        [BurstCompatible]
        internal unsafe struct MemoryBlock : IDisposable
        {
            public const int kMaximumAlignment = 16384; // can't align any coarser than this many bytes
            public byte* m_pointer; // pointer to contiguous memory
            public long m_bytes; // how many bytes of contiguous memory it points to
            public long m_current; // next byte to give out, when people "allocate" from this block
            public long m_allocations; // how many allocations have been made from this block, so far?
            public MemoryBlock(long bytes)
            {
                m_pointer = (byte*)Memory.Unmanaged.Allocate(bytes, kMaximumAlignment, Allocator.Persistent);
                m_bytes = bytes;
                m_current = 0;
                m_allocations = 0;
            }
            public void Rewind()
            {
                m_current = 0;
                m_allocations = 0;
            }
            public void Dispose()
            {
                Memory.Unmanaged.Free(m_pointer, Allocator.Persistent);
                m_pointer = null;
                m_bytes = 0;
                m_current = 0;
                m_allocations = 0;
            }

            public int TryAllocate(ref AllocatorManager.Block block)
            {
                // Make the alignment multiple of cacheline size
                var alignment = math.max(JobsUtility.CacheLineSize, block.Alignment);
                var extra = alignment != JobsUtility.CacheLineSize ? 1 : 0;
                var cachelineMask = JobsUtility.CacheLineSize - 1;
                if (extra == 1)
                {
                    alignment = (alignment + cachelineMask) & ~cachelineMask;
                }

                // Adjust the size to be multiple of alignment, add extra alignment
                // to size if alignment is more than cacheline size
                var mask = alignment - 1L;
                var size = (block.Bytes + extra * alignment + mask) & ~mask;
                var begin = Interlocked.Add(ref m_current, size) - size;
                begin = (begin + mask) & ~mask; // align the offset here
                if (begin + block.Bytes > m_bytes)
                    return AllocatorManager.kErrorBufferOverflow;
                block.Range.Pointer = (IntPtr)(m_pointer + begin);
                block.AllocatedItems = block.Range.Items;
                Interlocked.Increment(ref m_allocations);
                return AllocatorManager.kErrorNone;
            }

            public bool Contains(IntPtr ptr)
            {
                unsafe
                {
                    void* pointer = (void*)ptr;
                    return (pointer >= m_pointer) && (pointer < m_pointer + m_current);
                }
            }
        };

        Spinner m_spinner;
        AllocatorManager.AllocatorHandle m_handle;
        UnmanagedArray<MemoryBlock> m_block;
        int m_best; // block we expect is best to allocate from next
        int m_last; // highest-index block that has memory to allocate from
        int m_used; // highest-index block that we actually allocated from, since last rewind
        bool m_enableBlockFree; // flag indicating if allocator enables individual block free

        /// <summary>
        /// Initializes the allocator. Must be called before first use.
        /// </summary>
        /// <param name="initialSizeInBytes">The initial capacity of the allocator, in bytes</param>
        public void Initialize(int initialSizeInBytes, bool enableBlockFree = false)
        {
            m_spinner = default;
            m_block = new UnmanagedArray<MemoryBlock>(64, Allocator.Persistent);
            m_block[0] = new MemoryBlock(initialSizeInBytes);
            m_last = m_used = m_best = 0;
            m_enableBlockFree = enableBlockFree;
        }

        /// <summary>
        /// Property to get and set enable block free flag, a flag indicating whether allocator enables individual block free.
        /// </summary>
        public bool EnableBlockFree
        {
            get => m_enableBlockFree;
            set => m_enableBlockFree = value;
        }

        /// <summary>
        /// Retrieves the number of memory blocks that the allocator has requested from the system.
        /// </summary>
        public int BlocksAllocated => (int)(m_last + 1);

        /// <summary>
        /// Retrieves the size of the initial memory block, as requested in the Initialize function.
        /// </summary>
        public int InitialSizeInBytes => (int)(m_block[0].m_bytes);

        /// <summary>
        /// Rewind the allocator; invalidate all allocations made from it, and potentially also free memory blocks
        /// it has allocated from the system.
        /// </summary>
        public void Rewind()
        {
            if (JobsUtility.IsExecutingJob)
                throw new InvalidOperationException("You cannot Rewind a RewindableAllocator from a Job.");
            m_handle.Rewind(); // bump the allocator handle version, invalidate all dependents
            while (m_last > m_used) // *delete* all blocks we didn't even allocate from this time around.
                m_block[m_last--].Dispose();
            while (m_used > 0) // simply *rewind* all blocks we used in this update, to avoid allocating again, every update.
                m_block[m_used--].Rewind();
            m_block[0].Rewind();
        }

        /// <summary>
        /// Dispose the allocator. This must be called to free the memory blocks that were allocated from the system.
        /// </summary>
        public void Dispose()
        {
            if (JobsUtility.IsExecutingJob)
                throw new InvalidOperationException("You cannot Dispose a RewindableAllocator from a Job.");
            m_used = 0; // so that we delete all blocks in Rewind() on the next line
            Rewind();
            m_block[0].Dispose();
            m_block.Dispose();
            m_last = m_used = m_best = 0;
        }

        /// <summary>
        /// All allocators must implement this property, in order to be installed in the custom allocator table.
        /// </summary>
        [NotBurstCompatible]
        public AllocatorManager.TryFunction Function => Try;

        /// <summary>
        /// Try to allocate, free, or reallocate a block of memory. This is an internal function, and
        /// is not generally called by the user.
        /// </summary>
        /// <param name="block">The memory block to allocate, free, or reallocate</param>
        public int Try(ref AllocatorManager.Block block)
        {
            if (block.Range.Pointer == IntPtr.Zero)
            {
                // first, try to allocate from the block that succeeded last time, which we expect is likely to succeed again.
                var error = m_block[m_best].TryAllocate(ref block);
                if (error == AllocatorManager.kErrorNone)
                    return error;
                // if that fails, check all the blocks to see if any of them have enough memory
                m_spinner.Lock();
                int best;
                for (best = 0; best <= m_last; ++best)
                {
                    error = m_block[best].TryAllocate(ref block);
                    if (error == AllocatorManager.kErrorNone)
                    {
                        m_used = best > m_used ? best : m_used;
                        m_best = best;
                        m_spinner.Unlock();
                        return error;
                    }
                }
                // if that fails, allocate another block that's guaranteed big enough, and allocate from it.
                var bytes = math.max(m_block[0].m_bytes << best, math.ceilpow2(block.Bytes)); // if user suddenly asks for 1GB, skip smaller sizes
                m_block[best] = new MemoryBlock(bytes);
                error = m_block[best].TryAllocate(ref block);
                m_best = best;
                m_used = best;
                m_last = best;
                m_spinner.Unlock();
                return error;
            }

            // To free memory, no-op unless allocator enables individual block to be freed
            if (block.Range.Items == 0)
            {
                if (m_enableBlockFree)
                {
                    m_spinner.Lock();
                    if (m_block[m_best].Contains(block.Range.Pointer))
                        if (0 == Interlocked.Decrement(ref m_block[m_best].m_allocations))
                            m_block[m_best].Rewind();
                    m_spinner.Unlock();
                }
                return 0; // we could check to see if the pointer belongs to us, if we want to be strict about it.
            }

            return -1;
        }

        [BurstCompile]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(IntPtr state, ref AllocatorManager.Block block)
        {
            unsafe { return ((RewindableAllocator*)state)->Try(ref block); }
        }

        /// <summary>
        /// Retrieve the AllocatorHandle associated with this allocator. The handle is used as an index into a
        /// global table, for times when a reference to the allocator object isn't available.
        /// </summary>
        /// <value>The AllocatorHandle retrieved.</value>
        public AllocatorManager.AllocatorHandle Handle { get { return m_handle; } set { m_handle = value; } }

        /// <summary>
        /// Retrieve the Allocator associated with this allocator.
        /// </summary>
        /// <value>The Allocator retrieved.</value>
        public Allocator ToAllocator { get { return m_handle.ToAllocator; } }

        /// <summary>
        /// Check whether this AllocatorHandle is a custom allocator.
        /// </summary>
        /// <value>True if this AllocatorHandle is a custom allocator.</value>
        public bool IsCustomAllocator { get { return m_handle.IsCustomAllocator; } }

        /// <summary>
        /// Allocate a NativeArray of type T from memory that is guaranteed to remain valid until the end of the
        /// next Update of this World. There is no need to Dispose the NativeArray so allocated. It is not possible
        /// to free the memory by Disposing it - it is automatically freed after the end of the next Update for this
        /// World.
        /// </summary>
        /// <typeparam name="T">The element type of the NativeArray to allocate.</typeparam>
        /// <param name="length">The length of the NativeArray to allocate, measured in elements.</param>
        /// <returns>The NativeArray allocated by this function.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public NativeArray<T> AllocateNativeArray<T>(int length) where T : struct
        {
            var container = new NativeArray<T>();
            unsafe
            {
                container.m_Buffer = this.AllocateStruct(default(T), length);
            }
            container.m_Length = length;
            container.m_AllocatorLabel = Allocator.None;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            container.m_MinIndex = 0;
            container.m_MaxIndex = length - 1;
            container.m_Safety = CollectionHelper.CreateSafetyHandle(ToAllocator);
#if REMOVE_DISPOSE_SENTINEL
#else
            container.m_DisposeSentinel = null;
#endif
            CollectionHelper.SetStaticSafetyId<NativeArray<T>>(ref container.m_Safety, ref NativeArrayExtensions.NativeArrayStaticId<T>.s_staticSafetyId.Data);
            Handle.AddSafetyHandle(container.m_Safety);
#endif
            return container;
        }

        /// <summary>
        /// Allocate a NativeList of type T from memory that is guaranteed to remain valid until the end of the
        /// next Update of this World. There is no need to Dispose the NativeList so allocated. It is not possible
        /// to free the memory by Disposing it - it is automatically freed after the end of the next Update for this
        /// World. The NativeList must be initialized with its maximum capacity; if it were to dynamically resize,
        /// up to 1/2 of the total final capacity would be wasted, because the memory can't be dynamically freed.
        /// </summary>
        /// <typeparam name="T">The element type of the NativeList to allocate.</typeparam>
        /// <param name="capacity">The capacity of the NativeList to allocate, measured in elements.</param>
        /// <returns>The NativeList allocated by this function.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public NativeList<T> AllocateNativeList<T>(int capacity) where T : unmanaged
        {
            var container = new NativeList<T>();
            unsafe
            {
                container.m_ListData = this.Allocate(default(UnsafeList<T>), 1);
                container.m_ListData->Ptr = this.Allocate(default(T), capacity);
                container.m_ListData->m_capacity = capacity;
                container.m_ListData->m_length = 0;
                container.m_ListData->Allocator = Allocator.None;
            }
            container.m_DeprecatedAllocator = Allocator.None;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            container.m_Safety = CollectionHelper.CreateSafetyHandle(ToAllocator);
#if REMOVE_DISPOSE_SENTINEL
#else
            container.m_DisposeSentinel = null;
#endif
            CollectionHelper.SetStaticSafetyId<NativeList<T>>(ref container.m_Safety, ref NativeList<T>.s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(container.m_Safety, true);
            Handle.AddSafetyHandle(container.m_Safety);
#endif
            return container;
        }
    }
}
