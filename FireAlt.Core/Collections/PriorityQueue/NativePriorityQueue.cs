using FireAlt.Core.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// A job-safe priority queue that stores payload values and explicit integer priorities.
    /// </summary>
    /// <remarks>
    /// Use this when payload data should be ordered by a separate priority value rather than by <typeparamref name="T"/> itself.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Insert work items with <see cref="Enqueue"/> using computed score or urgency as the priority.</description></item>
    /// <item><description>Inspect the next item with <see cref="TryPeek"/> or <see cref="Peek"/> for look-ahead decisions.</description></item>
    /// <item><description>Drain items in priority order with repeated <see cref="TryDequeue"/> calls, typically in a processing loop.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    public struct NativePriorityQueue<T> : IDisposable
        where T : unmanaged
    {
        private UnsafePriorityQueue<T> _queue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityQueue<T>>();
#endif

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _queue.Count;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _queue.Capacity;
            }
        }

        public bool IsCreated => _queue.IsCreated;

        public NativePriorityQueue(AllocatorManager.AllocatorHandle allocator)
            : this(0, allocator)
        {
        }

        public NativePriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            _queue = new UnsafePriorityQueue<T>(initialCapacity, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _queue.Dispose();
                throw;
            }
        }

        public NativePriorityQueue(T[] items, int[] priorities, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            _queue = new UnsafePriorityQueue<T>(items, priorities, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _queue.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item, int priority)
        {
            CheckWrite();
            _queue.Enqueue(item, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item, out int priority)
        {
            CheckWrite();
            return _queue.TryDequeue(out item, out priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T item, out int priority)
        {
            CheckRead();
            return _queue.TryPeek(out item, out priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            CheckRead();
            return _queue.Peek();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _queue.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnsureCapacity(int capacity)
        {
            CheckWrite();
            return _queue.EnsureCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimExcess()
        {
            CheckWrite();
            _queue.TrimExcess();
        }

        public Enumerator GetEnumerator()
        {
            CheckRead();
            return new Enumerator(ref this);
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            _queue.Dispose();
            _queue = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSafety(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionChecks.InitNativeContainer<T>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativePriorityQueue<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        public struct Enumerator : IEnumerator<T>
        {
            private UnsafePriorityQueue<T>.Enumerator _enumerator;

            internal Enumerator(ref NativePriorityQueue<T> queue)
            {
                _enumerator = queue._queue.GetEnumerator();
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _enumerator.Current;
            }

            public int CurrentPriority
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _enumerator.CurrentPriority;
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _enumerator.Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }

    /// <summary>
    /// A job-safe priority queue that stores payload values and explicit integer priorities using a custom comparer.
    /// </summary>
    /// <remarks>
    /// Use this variant when default priority ordering is not enough and priority comparison must be customized (for example reversed order or tie-specific behavior).
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Construct with <typeparamref name="TComparer"/> to define domain-specific priority semantics.</description></item>
    /// <item><description>Schedule work by calling <see cref="Enqueue"/> with payload and priority pairs.</description></item>
    /// <item><description>Process or inspect ordered items through <see cref="TryDequeue"/> and <see cref="TryPeek"/> in systems and jobs.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    public struct NativePriorityQueue<T, TComparer> : IDisposable
        where T : unmanaged
        where TComparer : unmanaged, IComparer<int>
    {
        private UnsafePriorityQueue<T, TComparer> _queue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityQueue<T, TComparer>>();
#endif

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _queue.Count;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _queue.Capacity;
            }
        }

        public bool IsCreated => _queue.IsCreated;

        public NativePriorityQueue(AllocatorManager.AllocatorHandle allocator)
            : this(0, allocator, default)
        {
        }

        public NativePriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
            : this(initialCapacity, allocator, default)
        {
        }

        public NativePriorityQueue(int initialCapacity, AllocatorManager.AllocatorHandle allocator, TComparer comparer)
        {
            this = default;
            _queue = new UnsafePriorityQueue<T, TComparer>(initialCapacity, allocator, comparer);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _queue.Dispose();
                throw;
            }
        }

        public NativePriorityQueue(T[] items, int[] priorities, AllocatorManager.AllocatorHandle allocator)
            : this(items, priorities, allocator, default)
        {
        }

        public NativePriorityQueue(T[] items, int[] priorities, AllocatorManager.AllocatorHandle allocator, TComparer comparer)
        {
            this = default;
            _queue = new UnsafePriorityQueue<T, TComparer>(items, priorities, allocator, comparer);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _queue.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item, int priority)
        {
            CheckWrite();
            _queue.Enqueue(item, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item, out int priority)
        {
            CheckWrite();
            return _queue.TryDequeue(out item, out priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T item, out int priority)
        {
            CheckRead();
            return _queue.TryPeek(out item, out priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            CheckRead();
            return _queue.Peek();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _queue.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnsureCapacity(int capacity)
        {
            CheckWrite();
            return _queue.EnsureCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimExcess()
        {
            CheckWrite();
            _queue.TrimExcess();
        }

        public Enumerator GetEnumerator()
        {
            CheckRead();
            return new Enumerator(ref this);
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            _queue.Dispose();
            _queue = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSafety(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionChecks.InitNativeContainer<T>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativePriorityQueue<T, TComparer>>(ref m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        public struct Enumerator : IEnumerator<T>
        {
            private UnsafePriorityQueue<T, TComparer>.Enumerator _enumerator;

            internal Enumerator(ref NativePriorityQueue<T, TComparer> queue)
            {
                _enumerator = queue._queue.GetEnumerator();
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _enumerator.Current;
            }

            public int CurrentPriority
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _enumerator.CurrentPriority;
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _enumerator.Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }
}
