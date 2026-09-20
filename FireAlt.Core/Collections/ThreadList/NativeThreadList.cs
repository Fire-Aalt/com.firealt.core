using FireAlt.Core.Internal;
// <copyright project="NZCore" file="ParallelList.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// A job-safe per-thread append container that stores values in thread-local lists and supports later merged or segmented reads.
    /// </summary>
    /// <remarks>
    /// Use this when many threads produce variable amounts of data in parallel, and you want no contention during writes before a controlled readback phase.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Write per-thread outputs via <see cref="AsThreadWriter"/> for general producer jobs.</description></item>
    /// <item><description>Use <see cref="AsChunkWriter"/> and <see cref="SetChunkCount"/> for chunk-indexed producer/consumer pipelines.</description></item>
    /// <item><description>Consume results with <see cref="AsThreadReader"/>, <see cref="AsChunkReader"/>, or flatten to a contiguous <see cref="NativeList{T}"/> using <see cref="CopyToList"/>.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct NativeThreadList<T> : IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeThreadList<T>* _unsafeParallelList;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_SafetyIndexHint;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeThreadList<T>>();
#endif

        public int Length => _unsafeParallelList->Count();

        public NativeThreadList(AllocatorManager.AllocatorHandle allocator)
            : this(1, allocator)
        {
        }

        public NativeThreadList(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(initialCapacity, ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(int initialCapacity, ref TAllocator allocator) where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var totalSize = sizeof(T) * (long)initialCapacity;
            CollectionChecks.CheckAllocator(allocator.Handle);
            CheckInitialCapacity(initialCapacity);
            CheckTotalSize(initialCapacity, totalSize);

            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            CollectionChecks.InitNativeContainer<T>(m_Safety);

            CollectionHelper.SetStaticSafetyId<NativeThreadList<T>>(ref m_Safety, ref s_staticSafetyId.Data);

            m_SafetyIndexHint = (allocator.Handle).AddSafetyHandle(m_Safety);

            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafeParallelList = UnsafeThreadList<T>.Create(initialCapacity, ref allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChunkCount()
        {
            return _unsafeParallelList->GetChunkCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChunkCount(int chunkCount)
        {
            _unsafeParallelList->SetChunkCount(chunkCount);
        }

        public byte* GetPerThreadListPtr()
        {
            return _unsafeParallelList->GetPerThreadListPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref UnsafeList<T> GetUnsafeList(int threadIndex)
        {
            return ref _unsafeParallelList->GetUnsafeList(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockCount(int threadIndex)
        {
            return _unsafeParallelList->BlockCount(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBlockCountToIndex(int threadIndex)
        {
            return _unsafeParallelList->BlockCountToIndex(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetStartIndexArray(ref NativeArray<int> lengths)
        {
            return _unsafeParallelList->GetStartIndexArray(ref lengths);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _unsafeParallelList->Clear();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckInitialCapacity(int initialCapacity)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Capacity must be >= 0");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckTotalSize(int initialCapacity, long totalSize)
        {
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), $"Capacity * sizeof(T) cannot exceed {int.MaxValue} bytes");
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
            UnsafeThreadList<T>.Destroy(_unsafeParallelList);
            _unsafeParallelList = null;
        }

        public ChunkReader AsChunkReader()
        {
            return new ChunkReader(ref this);
        }

        public ChunkWriter AsChunkWriter()
        {
            return new ChunkWriter(ref this);
        }

        public ThreadReader AsThreadReader()
        {
            return new ThreadReader(ref this);
        }

        public ThreadWriter AsThreadWriter()
        {
            return new ThreadWriter(ref this);
        }

        public void Report()
        {
            for (int i = 0; i < JobsUtility.ThreadIndexCount; i++)
            {
                ref UnsafeList<T> parallelList = ref _unsafeParallelList->GetUnsafeList(i);

                Debug.Log($"Thread {i} has {parallelList.Length} elements.");
            }
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ChunkWriter
        {
            private UnsafeThreadList<T>.ChunkWriter chunkWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private readonly AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkWriter>();
#endif

            internal ChunkWriter(ref NativeThreadList<T> nativeThreadList)
            {
                chunkWriter = nativeThreadList._unsafeParallelList->AsChunkWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = nativeThreadList.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ChunkWriter");

                if (nativeThreadList._unsafeParallelList->CheckRangesForNull())
                    Debug.LogError($"Ranges have not been allocated. SetChunkCount(int chunkCount) before writing something."); // {Environment.StackTrace}");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void BeginForEachChunk(int chunkIndex)
            {
                chunkWriter.BeginForEachChunk(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(in T value)
            {
                chunkWriter.Add(in value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMemCpy(ref T value)
            {
                chunkWriter.AddMemCpy(ref value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EndForEachChunk()
            {
                chunkWriter.EndForEachChunk();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetManualThreadIndex(int threadIndex)
            {
                chunkWriter.SetManualThreadIndex(threadIndex);
            }

            public int GetThreadIndex()
            {
                return chunkWriter.GetThreadIndex();
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ChunkReader
        {
            private UnsafeThreadList<T>.ChunkReader chunkReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ChunkReader>();
#endif

            internal ChunkReader(ref NativeThreadList<T> stream)
            {
                chunkReader = stream._unsafeParallelList->AsChunkReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ChunkReader");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int BeginForEachChunk(int chunkIndex)
            {
                return chunkReader.BeginForEachChunk(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Read()
            {
                return ref chunkReader.Read();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* GetPtr()
            {
                return chunkReader.GetPtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(int chunkIndex)
            {
                chunkReader.Reset(chunkIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetListIndex(int chunkIndex)
            {
                return chunkReader.GetListIndex(chunkIndex);
            }
        }

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ThreadWriter
        {
            private UnsafeThreadList<T>.ThreadWriter threadWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadWriter>();
#endif

            internal ThreadWriter(ref NativeThreadList<T> nativeThreadList)
            {
                threadWriter = nativeThreadList._unsafeParallelList->AsThreadWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = nativeThreadList.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ThreadWriter");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Begin()
            {
                threadWriter.Begin();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(in T value)
            {
                threadWriter.Add(in value);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddNoBegin(in T value)
            {
                threadWriter.AddNoBegin(in value);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(in T value, int threadIndex)
            {
                threadWriter.Add(in value, threadIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddMemCpy(ref T value)
            {
                threadWriter.AddMemCpy(ref value);
            }

            public int GetThreadIndex()
            {
                return threadWriter.GetThreadIndex();
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public UnsafeList<T>* GetUnsafeThreadList()
            {
                return threadWriter.GetUnsafeThreadList();
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ThreadReader
        {
            private UnsafeThreadList<T>.ThreadReader threadReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadReader>();
#endif

            internal ThreadReader(ref NativeThreadList<T> stream)
            {
                threadReader = stream._unsafeParallelList->AsThreadReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.ThreadReader");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Begin()
            {
                return threadReader.Begin();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Begin(int threadIndex)
            {
                return threadReader.Begin(threadIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T Read()
            {
                return ref threadReader.Read();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T* GetPtr()
            {
                return threadReader.GetPtr();
            }
        }

        // helper jobs

        public JobHandle CopyToListSingle(ref NativeList<T> nativeList, JobHandle dependency, UnsafeThreadList<T>.UnsafeParallelListToArraySingleThreaded jobStud = default)
        {
            return new UnsafeThreadList<T>.UnsafeParallelListToArraySingleThreaded
            {
                ThreadList = *_unsafeParallelList,
                List = nativeList.GetListData()
            }.Schedule(dependency);
        }
        
        public void CopyToList(ref NativeList<T> nativeList)
        {
            _unsafeParallelList->CopyToList(nativeList.GetUnsafeList());
        }
        
        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }
        
        public struct Enumerator : IEnumerator<T>
        {
            private UnsafeThreadList<T>.Enumerator _enumerator;

            public void Dispose()
            {
                _enumerator.Dispose();
            }

            public Enumerator(ref NativeThreadList<T> list) 
            {
                _enumerator = list._unsafeParallelList->GetEnumerator();
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

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _enumerator.Current;
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Current;
            }
        }
    }
}
