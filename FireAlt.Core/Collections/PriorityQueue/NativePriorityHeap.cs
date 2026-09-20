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
    /// A job-safe binary heap where item ordering is defined by <see cref="IComparable{T}"/> on <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Use this when the value itself determines ordering, and you need repeated "next best item" retrieval in native memory.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Push candidates with <see cref="Enqueue"/> during simulation or search expansion.</description></item>
    /// <item><description>Inspect the current best element with <see cref="TryPeek"/> or <see cref="Peek"/> without removal.</description></item>
    /// <item><description>Consume in priority order with repeated <see cref="TryDequeue"/> calls until the heap is empty.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Count = {Count}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    public struct NativePriorityHeap<T> : IDisposable
        where T : unmanaged, IComparable<T>
    {
        private UnsafePriorityHeap<T> _heap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityHeap<T>>();
#endif

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _heap.Count;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _heap.Capacity;
            }
        }

        public bool IsCreated => _heap.IsCreated;

        public NativePriorityHeap(AllocatorManager.AllocatorHandle allocator)
            : this(0, allocator)
        {
        }

        public NativePriorityHeap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            _heap = new UnsafePriorityHeap<T>(initialCapacity, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _heap.Dispose();
                throw;
            }
        }

        public NativePriorityHeap(NativeArray<T> items, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            _heap = new UnsafePriorityHeap<T>(items, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _heap.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item)
        {
            CheckWrite();
            _heap.Enqueue(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            CheckWrite();
            return _heap.TryDequeue(out item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T item)
        {
            CheckRead();
            return _heap.TryPeek(out item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            CheckRead();
            return _heap.Peek();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _heap.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnsureCapacity(int capacity)
        {
            CheckWrite();
            return _heap.EnsureCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimExcess()
        {
            CheckWrite();
            _heap.TrimExcess();
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
            _heap.Dispose();
            _heap = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSafety(AllocatorManager.AllocatorHandle allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionChecks.InitNativeContainer<T>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativePriorityHeap<T>>(ref m_Safety, ref s_staticSafetyId.Data);
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
            private UnsafePriorityHeap<T>.Enumerator _enumerator;

            internal Enumerator(ref NativePriorityHeap<T> heap)
            {
                _enumerator = heap._heap.GetEnumerator();
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _enumerator.Current;
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
