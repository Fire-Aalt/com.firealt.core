using FireAlt.Core.Internal;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// A job-safe heap-style allocator backed by contiguous native memory that returns <see cref="MemoryPtr"/> handles.
    /// </summary>
    /// <remarks>
    /// Use this when you need variable-length allocations from one native backing store, with explicit free/reuse behavior and direct pointer/array access.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Allocate blocks with <c>Allocate</c> overloads for element counts or source arrays.</description></item>
    /// <item><description>Read or mutate allocated regions through <see cref="ElementAt{T}"/>, <see cref="ArrayAt{T}"/>, or <see cref="GetUnsafePtr"/>.</description></item>
    /// <item><description>Recycle storage with <see cref="Free"/>, validate handles with <see cref="Contains"/>, and pre-grow with <see cref="EnsureCapacity"/> to reduce reallocations.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Stride = {Stride}, Allocations = {AllocationCount}, UsedLength = {UsedLength}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    public unsafe struct NativeHeapMemory : IDisposable
    {
        private UnsafeHeapMemory _memory;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeHeapMemory>();
#endif

        public bool IsCreated => _memory.IsCreated;

        public int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.Stride;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.Capacity;
            }
        }

        public int CapacityBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.CapacityBytes;
            }
        }

        public int UsedLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.UsedLength;
            }
        }

        public int UsedLengthBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.UsedLengthBytes;
            }
        }

        public int AllocationCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return _memory.AllocationCount;
            }
        }

        public UnsafeList<byte> DataList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckWrite();
                return _memory.DataList;
            }
        }

        public NativeHeapMemory(int stride, Allocator allocator)
            : this(stride, 0, allocator)
        {
        }

        public NativeHeapMemory(int stride, int initialCapacity, Allocator allocator)
        {
            this = default;
            _memory = new UnsafeHeapMemory(stride, initialCapacity, allocator);

            try
            {
                InitializeSafety(allocator);
            }
            catch
            {
                _memory.Dispose();
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryPtr Allocate(int count)
        {
            CheckWrite();
            return _memory.Allocate(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryPtr Allocate<T>(UnsafeArray<T> source)
            where T : unmanaged
        {
            CheckWrite();
            return _memory.Allocate(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryPtr Allocate<T>(NativeArray<T> source)
            where T : unmanaged
        {
            CheckWrite();
            return _memory.Allocate(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryPtr Allocate(UnsafeArray<byte> source)
        {
            CheckWrite();
            return _memory.Allocate(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(MemoryPtr ptr)
        {
            CheckWrite();
            _memory.Free(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(MemoryPtr ptr)
        {
            CheckRead();
            return _memory.Contains(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValidRange(out int2 range)
        {
            CheckRead();
            return _memory.TryGetValidRange(out range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt<T>(MemoryPtr ptr, int index)
            where T : unmanaged
        {
            CheckWrite();
            return ref _memory.ElementAt<T>(ptr, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeArray<T> ArrayAt<T>(MemoryPtr ptr)
            where T : unmanaged
        {
            CheckWrite();
            return _memory.ArrayAt<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnsafeArray<byte> ArrayAtUnsafe(MemoryPtr ptr)
        {
            CheckWrite();
            return _memory.ArrayAtUnsafe(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetUnsafePtr(MemoryPtr ptr)
        {
            CheckWrite();
            return _memory.GetUnsafePtr(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr<T>(MemoryPtr ptr)
            where T : unmanaged
        {
            CheckWrite();
            return _memory.GetUnsafePtr<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnsureCapacity(int capacity)
        {
            CheckWrite();
            return _memory.EnsureCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _memory.Clear();
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            _memory.Dispose();
            _memory = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeSafety(Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionChecks.InitNativeContainer<byte>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativeHeapMemory>(ref m_Safety, ref s_staticSafetyId.Data);
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
    }
}
