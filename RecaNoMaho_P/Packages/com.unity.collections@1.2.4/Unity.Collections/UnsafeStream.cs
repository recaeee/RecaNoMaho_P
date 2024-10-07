using System;
using Unity.Burst;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Collections.LowLevel.Unsafe
{
    [BurstCompatible]
    internal unsafe struct UnsafeStreamBlock
    {
        internal UnsafeStreamBlock* Next;
        internal fixed byte Data[1];
    }

    [BurstCompatible]
    internal unsafe struct UnsafeStreamRange
    {
        internal UnsafeStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        /// One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;
    }

    [BurstCompatible]
    internal unsafe struct UnsafeStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal AllocatorManager.AllocatorHandle Allocator;

        internal UnsafeStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeStreamBlock* Free;

        internal UnsafeStreamRange* Ranges;
        internal int RangeCount;

        internal UnsafeStreamBlock* Allocate(UnsafeStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < BlockCount && threadIndex >= 0);

            UnsafeStreamBlock* block = (UnsafeStreamBlock*)Memory.Unmanaged.Allocate(AllocationSize, 16, Allocator);
            block->Next = null;

            if (oldBlock == null)
            {
                // Append our new block in front of the previous head.
                block->Next = Blocks[threadIndex];
                Blocks[threadIndex] = block;
            }
            else
            {
                oldBlock->Next = block;
            }

            return block;
        }
    }

    /// <summary>
    /// A set of untyped, append-only buffers. Allows for concurrent reading and concurrent writing without synchronization.
    /// </summary>
    /// <remarks>
    /// As long as each individual buffer is written in one thread and read in one thread, multiple
    /// threads can read and write the stream concurrently, *e.g.*
    /// while thread *A* reads from buffer *X* of a stream, thread *B* can read from
    /// buffer *Y* of the same stream.
    ///
    /// Each buffer is stored as a chain of blocks. When a write exceeds a buffer's current capacity, another block
    /// is allocated and added to the end of the chain. Effectively, expanding the buffer never requires copying the existing
    /// data (unlike, for example, with <see cref="NativeList{T}"/>).
    ///
    /// **All writing to a stream should be completed before the stream is first read. Do not write to a stream after the first read.**
    ///
    /// Writing is done with <see cref="NativeStream.Writer"/>, and reading is done with <see cref="NativeStream.Reader"/>.
    /// An individual reader or writer cannot be used concurrently across threads. Each thread must use its own.
    ///
    /// The data written to an individual buffer can be heterogeneous in type, and the data written
    /// to different buffers of a stream can be entirely different in type, number, and order. Just make sure
    /// that the code reading from a particular buffer knows what to expect to read from it.
    /// </remarks>
    [BurstCompatible]
    public unsafe struct UnsafeStream
        : INativeDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeStreamBlockData* m_Block;
        internal AllocatorManager.AllocatorHandle m_Allocator;

        /// <summary>
        /// Initializes and returns an instance of UnsafeStream.
        /// </summary>
        /// <param name="bufferCount">The number of buffers to give the stream. You usually want
        /// one buffer for each thread that will read or write the stream.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeStream(int bufferCount, AllocatorManager.AllocatorHandle allocator)
        {
            AllocateBlock(out this, allocator);
            AllocateForEach(bufferCount);
        }

        /// <summary>
        /// Creates and schedules a job to allocate a new stream.
        /// </summary>
        /// <remarks>The stream can be used on the main thread after completing the returned job or used in other jobs that depend upon the returned job.
        ///
        /// Using a job to allocate the buffers can be more efficient, particularly for a stream with many buffers.
        /// </remarks>
        /// <typeparam name="T">Ignored.</typeparam>
        /// <param name="stream">Outputs the new stream.</param>
        /// <param name="bufferCount">A list whose length determines the number of buffers in the stream.</param>
        /// <param name="dependency">A job handle. The new job will depend upon this handle.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>The handle of the new job.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public static JobHandle ScheduleConstruct<T>(out UnsafeStream stream, NativeList<T> bufferCount, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
            where T : unmanaged
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJobList { List = (UntypedUnsafeList*)bufferCount.GetUnsafeList(), Container = stream };
            return jobData.Schedule(dependency);
        }

        /// <summary>
        /// Creates and schedules a job to allocate a new stream.
        /// </summary>
        /// <remarks>The stream can be used on the main thread after completing the returned job or used in other jobs that depend upon the returned job.
        ///
        /// Allocating the buffers in a job can be more efficient, particularly for a stream with many buffers.
        /// </remarks>
        /// <param name="stream">Outputs the new stream.</param>
        /// <param name="bufferCount">An array whose value at index 0 determines the number of buffers in the stream.</param>
        /// <param name="dependency">A job handle. The new job will depend upon this handle.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>The handle of the new job.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public static JobHandle ScheduleConstruct(out UnsafeStream stream, NativeArray<int> bufferCount, JobHandle dependency, AllocatorManager.AllocatorHandle allocator)
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJob { Length = bufferCount, Container = stream };
            return jobData.Schedule(dependency);
        }

        internal static void AllocateBlock(out UnsafeStream stream, AllocatorManager.AllocatorHandle allocator)
        {
            int blockCount = JobsUtility.MaxJobThreadCount;

            int allocationSize = sizeof(UnsafeStreamBlockData) + sizeof(UnsafeStreamBlock*) * blockCount;
            byte* buffer = (byte*)Memory.Unmanaged.Allocate(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeStreamBlockData*)buffer;

            stream.m_Block = block;
            stream.m_Allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = blockCount;
            block->Blocks = (UnsafeStreamBlock**)(buffer + sizeof(UnsafeStreamBlockData));

            block->Ranges = null;
            block->RangeCount = 0;
        }

        internal void AllocateForEach(int forEachCount)
        {
            long allocationSize = sizeof(UnsafeStreamRange) * forEachCount;
            m_Block->Ranges = (UnsafeStreamRange*)Memory.Unmanaged.Allocate(allocationSize, 16, m_Allocator);
            m_Block->RangeCount = forEachCount;
            UnsafeUtility.MemClear(m_Block->Ranges, allocationSize);
        }

        /// <summary>
        /// Returns true if this stream is empty.
        /// </summary>
        /// <returns>True if this stream is empty or the stream has not been constructed.</returns>
        public bool IsEmpty()
        {
            if (!IsCreated)
            {
                return true;
            }

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                if (m_Block->Ranges[i].ElementCount > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether this stream has been allocated (and not yet deallocated).
        /// </summary>
        /// <remarks>Does not necessarily reflect whether the buffers of the stream have themselves been allocated.</remarks>
        /// <value>True if this stream has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_Block != null;

        /// <summary>
        /// The number of buffers in this stream.
        /// </summary>
        /// <value>The number of buffers in this stream.</value>
        public int ForEachCount => m_Block->RangeCount;

        /// <summary>
        /// Returns a reader of this stream.
        /// </summary>
        /// <returns>A reader of this stream.</returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Returns a writer of this stream.
        /// </summary>
        /// <returns>A writer of this stream.</returns>
        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        /// <summary>
        /// Returns the total number of items in the buffers of this stream.
        /// </summary>
        /// <remarks>Each <see cref="Writer.Write{T}"/> and <see cref="Writer.Allocate"/> call increments this number.</remarks>
        /// <returns>The total number of items in the buffers of this stream.</returns>
        public int Count()
        {
            int itemCount = 0;

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                itemCount += m_Block->Ranges[i].ElementCount;
            }

            return itemCount;
        }

        /// <summary>
        /// Returns a new NativeArray copy of this stream's data.
        /// </summary>
        /// <remarks>The length of the array will equal the count of this stream.
        ///
        /// Each buffer of this stream is copied to the array, one after the other.
        /// </remarks>
        /// <typeparam name="T">The type of values in the array.</typeparam>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>A new NativeArray copy of this stream's data.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public NativeArray<T> ToNativeArray<T>(AllocatorManager.AllocatorHandle allocator) where T : struct
        {
            var array = CollectionHelper.CreateNativeArray<T>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = AsReader();

            int offset = 0;
            for (int i = 0; i != reader.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                int rangeItemCount = reader.RemainingItemCount;
                for (int j = 0; j < rangeItemCount; ++j)
                {
                    array[offset] = reader.Read<T>();
                    offset++;
                }
                reader.EndForEachIndex();
            }

            return array;
        }

        void Deallocate()
        {
            if (m_Block == null)
            {
                return;
            }

            for (int i = 0; i != m_Block->BlockCount; i++)
            {
                UnsafeStreamBlock* block = m_Block->Blocks[i];
                while (block != null)
                {
                    UnsafeStreamBlock* next = block->Next;
                    Memory.Unmanaged.Free(block, m_Allocator);
                    block = next;
                }
            }

            Memory.Unmanaged.Free(m_Block->Ranges, m_Allocator);
            Memory.Unmanaged.Free(m_Block, m_Allocator);
            m_Block = null;
            m_Allocator = Allocator.None;
        }

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose()
        {
            Deallocate();
        }

        /// <summary>
        /// Creates and schedules a job that will release all resources (memory and safety handles) of this stream.
        /// </summary>
        /// <param name="inputDeps">A job handle which the newly scheduled job will depend upon.</param>
        /// <returns>The handle of a new job that will release all resources (memory and safety handles) of this stream.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

            m_Block = null;

            return jobHandle;
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            public UnsafeStream Container;

            public void Execute()
            {
                Container.Deallocate();
            }
        }

        [BurstCompile]
        struct ConstructJobList : IJob
        {
            public UnsafeStream Container;

            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public UntypedUnsafeList* List;

            public void Execute()
            {
                Container.AllocateForEach(List->m_length);
            }
        }

        [BurstCompile]
        struct ConstructJob : IJob
        {
            public UnsafeStream Container;

            [ReadOnly]
            public NativeArray<int> Length;

            public void Execute()
            {
                Container.AllocateForEach(Length[0]);
            }
        }

        /// <summary>
        /// Writes data into a buffer of an <see cref="UnsafeStream"/>.
        /// </summary>
        /// <remarks>An individual writer can only be used for one buffer of one stream.
        /// Do not create more than one writer for an individual buffer.</remarks>
        [BurstCompatible]
        public unsafe struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentBlockEnd;

            internal int m_ForeachIndex;
            int m_ElementCount;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_FirstBlock;

            int m_FirstOffset;
            int m_NumberOfBlocks;

            [NativeSetThreadIndex]
            int m_ThreadIndex;

            internal Writer(ref UnsafeStream stream)
            {
                m_BlockStream = stream.m_Block;
                m_ForeachIndex = int.MinValue;
                m_ElementCount = -1;
                m_CurrentBlock = null;
                m_CurrentBlockEnd = null;
                m_CurrentPtr = null;
                m_FirstBlock = null;
                m_NumberOfBlocks = 0;
                m_FirstOffset = 0;
                m_ThreadIndex = 0;
            }

            /// <summary>
            /// The number of buffers in the stream of this writer.
            /// </summary>
            /// <value>The number of buffers in the stream of this writer.</value>
            public int ForEachCount => m_BlockStream->RangeCount;

            /// <summary>
            /// Readies this writer to write to a particular buffer of the stream.
            /// </summary>
            /// <remarks>Must be called before using this writer. For an individual writer, call this method only once.
            ///
            /// When done using this writer, you must call <see cref="EndForEachIndex"/>.</remarks>
            /// <param name="foreachIndex">The index of the buffer to write.</param>
            public void BeginForEachIndex(int foreachIndex)
            {
                m_ForeachIndex = foreachIndex;
                m_ElementCount = 0;
                m_NumberOfBlocks = 0;
                m_FirstBlock = m_CurrentBlock;
                m_FirstOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
            }

            /// <summary>
            /// Readies the buffer written by this writer for reading.
            /// </summary>
            /// <remarks>Must be called before reading the buffer written by this writer.</remarks>
            public void EndForEachIndex()
            {
                m_BlockStream->Ranges[m_ForeachIndex].ElementCount = m_ElementCount;
                m_BlockStream->Ranges[m_ForeachIndex].OffsetInFirstBlock = m_FirstOffset;
                m_BlockStream->Ranges[m_ForeachIndex].Block = m_FirstBlock;

                m_BlockStream->Ranges[m_ForeachIndex].LastOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
                m_BlockStream->Ranges[m_ForeachIndex].NumberOfBlocks = m_NumberOfBlocks;
            }

            /// <summary>
            /// Write a value to a buffer.
            /// </summary>
            /// <remarks>The value is written to the buffer which was specified
            /// with <see cref="BeginForEachIndex"/>.</remarks>
            /// <typeparam name="T">The type of value to write.</typeparam>
            /// <param name="value">The value to write.</param>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void Write<T>(T value) where T : struct
            {
                ref T dst = ref Allocate<T>();
                dst = value;
            }

            /// <summary>
            /// Allocate space in a buffer.
            /// </summary>
            /// <remarks>The space is allocated in the buffer which was specified
            /// with <see cref="BeginForEachIndex"/>.</remarks>
            /// <typeparam name="T">The type of value to allocate space for.</typeparam>
            /// <returns>A reference to the allocation.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Allocate<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(Allocate(size));
            }

            /// <summary>
            /// Allocate space in a buffer.
            /// </summary>
            /// <remarks>The space is allocated in the buffer which was specified
            /// with <see cref="BeginForEachIndex"/>.</remarks>
            /// <param name="size">The number of bytes to allocate.</param>
            /// <returns>The allocation.</returns>
            public byte* Allocate(int size)
            {
                byte* ptr = m_CurrentPtr;
                m_CurrentPtr += size;

                if (m_CurrentPtr > m_CurrentBlockEnd)
                {
                    UnsafeStreamBlock* oldBlock = m_CurrentBlock;

                    m_CurrentBlock = m_BlockStream->Allocate(oldBlock, m_ThreadIndex);
                    m_CurrentPtr = m_CurrentBlock->Data;

                    if (m_FirstBlock == null)
                    {
                        m_FirstOffset = (int)(m_CurrentPtr - (byte*)m_CurrentBlock);
                        m_FirstBlock = m_CurrentBlock;
                    }
                    else
                    {
                        m_NumberOfBlocks++;
                    }

                    m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;
                    ptr = m_CurrentPtr;
                    m_CurrentPtr += size;
                }

                m_ElementCount++;

                return ptr;
            }
        }

        /// <summary>
        /// Reads data from a buffer of an <see cref="UnsafeStream"/>.
        /// </summary>
        /// <remarks>An individual reader can only be used for one buffer of one stream.
        /// Do not create more than one reader for an individual buffer.</remarks>
        [BurstCompatible]
        public unsafe struct Reader
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentBlockEnd;

            internal int m_RemainingItemCount;
            internal int m_LastBlockSize;

            internal Reader(ref UnsafeStream stream)
            {
                m_BlockStream = stream.m_Block;
                m_CurrentBlock = null;
                m_CurrentPtr = null;
                m_CurrentBlockEnd = null;
                m_RemainingItemCount = 0;
                m_LastBlockSize = 0;
            }

            /// <summary>
            /// Readies this reader to read a particular buffer of the stream.
            /// </summary>
            /// <remarks>Must be called before using this reader. For an individual reader, call this method only once.
            ///
            /// When done using this reader, you must call <see cref="EndForEachIndex"/>.</remarks>
            /// <param name="foreachIndex">The index of the buffer to read.</param>
            /// <returns>The number of remaining elements to read from the buffer.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                m_RemainingItemCount = m_BlockStream->Ranges[foreachIndex].ElementCount;
                m_LastBlockSize = m_BlockStream->Ranges[foreachIndex].LastOffset;

                m_CurrentBlock = m_BlockStream->Ranges[foreachIndex].Block;
                m_CurrentPtr = (byte*)m_CurrentBlock + m_BlockStream->Ranges[foreachIndex].OffsetInFirstBlock;
                m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                return m_RemainingItemCount;
            }

            /// <summary>
            /// Does nothing.
            /// </summary>
            /// <remarks>Included only for consistency with <see cref="NativeStream"/>.</remarks>
            public void EndForEachIndex()
            {
            }

            /// <summary>
            /// The number of buffers in the stream of this reader.
            /// </summary>
            /// <value>The number of buffers in the stream of this reader.</value>
            public int ForEachCount => m_BlockStream->RangeCount;

            /// <summary>
            /// The number of items not yet read from the buffer.
            /// </summary>
            /// <value>The number of items not yet read from the buffer.</value>
            public int RemainingItemCount => m_RemainingItemCount;

            /// <summary>
            /// Returns a pointer to the next position to read from the buffer. Advances the reader some number of bytes.
            /// </summary>
            /// <param name="size">The number of bytes to advance the reader.</param>
            /// <returns>A pointer to the next position to read from the buffer.</returns>
            /// <exception cref="System.ArgumentException">Thrown if the reader has been advanced past the end of the buffer.</exception>
            public byte* ReadUnsafePtr(int size)
            {
                m_RemainingItemCount--;

                byte* ptr = m_CurrentPtr;
                m_CurrentPtr += size;

                if (m_CurrentPtr > m_CurrentBlockEnd)
                {
                    m_CurrentBlock = m_CurrentBlock->Next;
                    m_CurrentPtr = m_CurrentBlock->Data;

                    m_CurrentBlockEnd = (byte*)m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                    ptr = m_CurrentPtr;
                    m_CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary>
            /// Reads the next value from the buffer.
            /// </summary>
            /// <remarks>Each read advances the reader to the next item in the buffer.</remarks>
            /// <typeparam name="T">The type of value to read.</typeparam>
            /// <returns>A reference to the next value from the buffer.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Read<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(ReadUnsafePtr(size));
            }

            /// <summary>
            /// Reads the next value from the buffer. Does not advance the reader.
            /// </summary>
            /// <typeparam name="T">The type of value to read.</typeparam>
            /// <returns>A reference to the next value from the buffer.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Peek<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();

                byte* ptr = m_CurrentPtr;
                if (ptr + size > m_CurrentBlockEnd)
                {
                    ptr = m_CurrentBlock->Next->Data;
                }

                return ref UnsafeUtility.AsRef<T>(ptr);
            }

            /// <summary>
            /// Returns the total number of items in the buffers of the stream.
            /// </summary>
            /// <returns>The total number of items in the buffers of the stream.</returns>
            public int Count()
            {
                int itemCount = 0;
                for (int i = 0; i != m_BlockStream->RangeCount; i++)
                {
                    itemCount += m_BlockStream->Ranges[i].ElementCount;
                }

                return itemCount;
            }
        }
    }
}
