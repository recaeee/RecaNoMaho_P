using System;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe
{
    [BurstCompatible]
    internal struct RingControl
    {
        internal RingControl(int capacity)
        {
            Capacity = capacity;
            Current = 0;
            Write = 0;
            Read = 0;
        }

        internal void Reset()
        {
            Current = 0;
            Write = 0;
            Read = 0;
        }

        internal int Distance(int from, int to)
        {
            var diff = to - from;
            return diff < 0 ? Capacity - math.abs(diff) : diff;
        }

        internal int Available()
        {
            return Distance(Read, Current);
        }

        internal int Reserve(int count)
        {
            var dist = Distance(Write, Read) - 1;
            var maxCount = dist < 0 ? Capacity - 1 : dist;
            var absCount = math.abs(count);
            var test = absCount - maxCount;
            count = test < 0 ? count : maxCount;
            Write = (Write + count) % Capacity;

            return count;
        }

        internal int Commit(int count)
        {
            var maxCount = Distance(Current, Write);
            var absCount = math.abs(count);
            var test = absCount - maxCount;
            count = test < 0 ? count : maxCount;
            Current = (Current + count) % Capacity;

            return count;
        }

        internal int Consume(int count)
        {
            var maxCount = Distance(Read, Current);
            var absCount = math.abs(count);
            var test = absCount - maxCount;
            count = test < 0 ? count : maxCount;
            Read = (Read + count) % Capacity;

            return count;
        }

        internal int Length => Distance(Read, Write);

        internal readonly int Capacity;
        internal int Current;
        internal int Write;
        internal int Read;
    }

    /// <summary>
    /// A fixed-size circular buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}, IsEmpty = {IsEmpty}")]
    [DebuggerTypeProxy(typeof(UnsafeRingQueueDebugView<>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
    public unsafe struct UnsafeRingQueue<T>
        : INativeDisposable
        where T : unmanaged
    {
        /// <summary>
        /// The internal buffer where the content is stored.
        /// </summary>
        /// <value>The internal buffer where the content is stored.</value>
        [NativeDisableUnsafePtrRestriction]
        public T* Ptr;

        /// <summary>
        /// The allocator used to create the internal buffer.
        /// </summary>
        /// <value>The allocator used to create the internal buffer.</value>
        public AllocatorManager.AllocatorHandle Allocator;

        internal RingControl Control;

        /// <summary>
        /// Whether the queue is empty.
        /// </summary>
        /// <value>True if the queue is empty or the queue has not been constructed.</value>
        public bool IsEmpty => !IsCreated || Length == 0;

        /// <summary>
        /// The number of elements currently in this queue.
        /// </summary>
        /// <value>The number of elements currently in this queue.</value>
        public int Length => Control.Length;

        /// <summary>
        /// The number of elements that fit in the internal buffer.
        /// </summary>
        /// <value>The number of elements that fit in the internal buffer.</value>
        public int Capacity => Control.Capacity;

        /// <summary>
        /// Initializes and returns an instance of UnsafeRingQueue which aliasing an existing buffer.
        /// </summary>
        /// <param name="ptr">An existing buffer to set as the internal buffer.</param>
        /// <param name="capacity">The capacity.</param>
        public UnsafeRingQueue(T* ptr, int capacity)
        {
            Ptr = ptr;
            Allocator = AllocatorManager.None;
            Control = new RingControl(capacity);
        }

        /// <summary>
        /// Initializes and returns an instance of UnsafeRingQueue.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <param name="allocator">The allocator to use.</param>
        /// <param name="options">Whether newly allocated bytes should be zeroed out.</param>
        public UnsafeRingQueue(int capacity, AllocatorManager.AllocatorHandle allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            capacity += 1;

            Allocator = allocator;
            Control = new RingControl(capacity);
            var sizeInBytes = capacity * UnsafeUtility.SizeOf<T>();
            Ptr = (T*)Memory.Unmanaged.Allocate(sizeInBytes, 16, allocator);

            if (options == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(Ptr, sizeInBytes);
            }
        }

        /// <summary>
        /// Whether this queue has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this queue has been allocated (and not yet deallocated).</value>
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
        }

        /// <summary>
        /// Creates and schedules a job that will dispose this queue.
        /// </summary>
        /// <param name="inputDeps">The handle of a job which the new job will depend upon.</param>
        /// <returns>The handle of a new job that will dispose this queue. The new job depends upon inputDeps.</returns>
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
        /// Adds an element at the front of the queue.
        /// </summary>
        /// <remarks>Does nothing if the queue is full.</remarks>
        /// <param name="value">The value to be added.</param>
        /// <returns>True if the value was added.</returns>
        public bool TryEnqueue(T value)
        {
            if (1 != Control.Reserve(1))
            {
                return false;
            }

            Ptr[Control.Current] = value;
            Control.Commit(1);

            return true;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ThrowQueueFull()
        {
            throw new InvalidOperationException("Trying to enqueue into full queue.");
        }

        /// <summary>
        /// Adds an element at the front of the queue.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        /// <exception cref="InvalidOperationException">Thrown if the queue was full.</exception>
        public void Enqueue(T value)
        {
            if (!TryEnqueue(value))
            {
                ThrowQueueFull();
            }
        }

        /// <summary>
        /// Removes the element from the end of the queue.
        /// </summary>
        /// <remarks>Does nothing if the queue is empty.</remarks>
        /// <param name="item">Outputs the element removed.</param>
        /// <returns>True if an element was removed.</returns>
        public bool TryDequeue(out T item)
        {
            item = Ptr[Control.Read];
            return 1 == Control.Consume(1);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ThrowQueueEmpty()
        {
            throw new InvalidOperationException("Trying to dequeue from an empty queue");
        }

        /// <summary>
        /// Removes the element from the end of the queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the queue was empty.</exception>
        /// <returns>Returns the removed element.</returns>
        public T Dequeue()
        {
            if (!TryDequeue(out T item))
            {
                ThrowQueueEmpty();
            }

            return item;
        }
    }

    internal sealed class UnsafeRingQueueDebugView<T>
        where T : unmanaged
    {
        UnsafeRingQueue<T> Data;

        public UnsafeRingQueueDebugView(UnsafeRingQueue<T> data)
        {
            Data = data;
        }

        public unsafe T[] Items
        {
            get
            {
                T[] result = new T[Data.Length];

                var read = Data.Control.Read;
                var capacity = Data.Control.Capacity;

                for (var i = 0; i < result.Length; ++i)
                {
                    result[i] = Data.Ptr[(read + i) % capacity];
                }

                return result;
            }
        }
    }
}
