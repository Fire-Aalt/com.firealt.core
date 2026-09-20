using FireAlt.Core.Internal;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace FireAlt.Core.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeThreadData<T> : INativeDisposable
        where T : unmanaged
    {
        // Cache line aligned storage avoids false sharing between worker threads.
        public const int PER_THREAD_DATA_SIZE = JobsUtility.CacheLineSize;

        [NativeDisableUnsafePtrRestriction] private byte* perThreadData;

        private int perThreadDataStride;
        private AllocatorManager.AllocatorHandle allocator;

        public bool IsCreated;

        public int Length => JobsUtility.ThreadIndexCount;
        
        public UnsafeThreadData(AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            Initialize(allocator);
        }

        private void Initialize(AllocatorManager.AllocatorHandle allocatorHandle)
        {
            allocator = allocatorHandle;
            perThreadDataStride = CalculatePerThreadDataStride();

            int maxThreadCount = JobsUtility.ThreadIndexCount;
            int align = CalculatePerThreadDataAlignment();
            int allocationSize = perThreadDataStride * maxThreadCount;

            perThreadData = (byte*)UnsafeUtility.Malloc(allocationSize, align, allocatorHandle.ToAllocator);
            UnsafeUtility.MemClear(perThreadData, allocationSize);

            IsCreated = true;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        internal static UnsafeThreadData<T>* Create<TAllocator>(ref TAllocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
            UnsafeThreadData<T>* unsafePerThreadData = CollectionMemory.Allocate<UnsafeThreadData<T>, TAllocator>(ref allocator);
            *unsafePerThreadData = new UnsafeThreadData<T>(allocator.Handle);

            return unsafePerThreadData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculatePerThreadDataStride()
        {
            int dataSize = UnsafeUtility.SizeOf<T>();
            int roundedToCacheLine = ((dataSize + PER_THREAD_DATA_SIZE - 1) / PER_THREAD_DATA_SIZE) * PER_THREAD_DATA_SIZE;
            int minCacheLineSize = roundedToCacheLine < PER_THREAD_DATA_SIZE ? PER_THREAD_DATA_SIZE : roundedToCacheLine;

            int alignment = CalculatePerThreadDataAlignment();
            return ((minCacheLineSize + alignment - 1) / alignment) * alignment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculatePerThreadDataAlignment()
        {
            int alignment = UnsafeUtility.AlignOf<T>();
            return alignment < PER_THREAD_DATA_SIZE ? PER_THREAD_DATA_SIZE : alignment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetUnsafeThreadData(int threadIndex)
        {
            return ref UnsafeUtility.AsRef<T>((T*)(perThreadData + threadIndex * perThreadDataStride));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetPerThreadDataPtr()
        {
            return perThreadData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPerThreadDataStride()
        {
            return perThreadDataStride;
        }

        public void Clear()
        {
            if (!IsCreated)
                return;

            UnsafeUtility.MemClear(perThreadData, perThreadDataStride * JobsUtility.ThreadIndexCount);
        }

        public void Clear(in T value)
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                GetUnsafeThreadData(i) = value;
            }
        }

        public ThreadReader AsThreadReader()
        {
            return new ThreadReader(ref this);
        }

        public ThreadWriter AsThreadWriter()
        {
            return new ThreadWriter(ref this);
        }
        
        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        public static void Destroy(UnsafeThreadData<T>* unsafePerThreadData)
        {
            var allocator = unsafePerThreadData->allocator;
            unsafePerThreadData->Dispose();
            AllocatorManager.Free(allocator, unsafePerThreadData);
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            UnsafeUtility.Free(perThreadData, allocator.ToAllocator);
            perThreadData = null;
            perThreadDataStride = 0;
            allocator = Allocator.None;
            IsCreated = false;
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public UnsafeThreadData<T> Data;

            public void Execute()
            {
                Data.Dispose();
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return new DisposeJob
            {
                Data = this
            }.Schedule(inputDeps);
        }

        public struct ThreadWriter
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadDataPtr;
            [NativeSetThreadIndex] private int threadIndex;

            private readonly int threadDataStride;

            internal ThreadWriter(ref UnsafeThreadData<T> data)
            {
                perThreadDataPtr = data.perThreadData;
                threadDataStride = data.perThreadDataStride;

                threadIndex = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(in T data)
            {
                GetRef() = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T GetRef()
            {
                return ref UnsafeUtility.AsRef<T>(GetThreadDataPtr(threadIndex));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private T* GetThreadDataPtr(int index)
            {
                return (T*)(perThreadDataPtr + index * threadDataStride);
            }
        }

        public struct ThreadReader
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadDataPtr;
            [NativeSetThreadIndex] private int threadIndex;

            private readonly int threadDataStride;

            internal ThreadReader(ref UnsafeThreadData<T> data)
            {
                perThreadDataPtr = data.perThreadData;
                threadDataStride = data.perThreadDataStride;

                threadIndex = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Get()
            {
                return *GetThreadDataPtr(threadIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private T* GetThreadDataPtr(int index)
            {
                return (T*)(perThreadDataPtr + index * threadDataStride);
            }
        }

        public struct Enumerator : IEnumerator
        {
            [NativeDisableUnsafePtrRestriction] private readonly byte* perThreadDataPtr;
            private readonly int threadDataStride;
            private readonly int threadCount;

            private int index;
            private T value;

            internal Enumerator(ref UnsafeThreadData<T> data)
            {
                perThreadDataPtr = data.perThreadData;
                threadDataStride = data.perThreadDataStride;
                threadCount = JobsUtility.ThreadIndexCount;

                index = -1;
                value = default;
            }

            public void Dispose()
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int nextIndex = index + 1;
                if (nextIndex >= threadCount)
                {
                    value = default;
                    return false;
                }

                index = nextIndex;
                value = *(T*)(perThreadDataPtr + index * threadDataStride);
                return true;
            }

            public void Reset()
            {
                index = -1;
                value = default;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => value;
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Current;
            }
        }
    }
}
