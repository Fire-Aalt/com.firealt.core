using FireAlt.Core.Internal;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// A job-safe per-thread storage container that keeps one <typeparamref name="T"/> value per worker thread.
    /// </summary>
    /// <remarks>
    /// Use this when parallel jobs need thread-local mutable state without contention, followed by an optional combine/reduction phase.
    /// Common patterns include:
    /// <list type="bullet">
    /// <item><description>Write thread-local accumulators with <see cref="AsThreadWriter"/> and <see cref="ThreadWriter.GetRef"/> or <see cref="ThreadWriter.Set"/>.</description></item>
    /// <item><description>Initialize per-thread defaults using <see cref="Clear"/> overloads before scheduling producer jobs.</description></item>
    /// <item><description>Read combined thread slots through <see cref="AsThreadReader"/> or <see cref="GetEnumerator"/> in a follow-up aggregation step.</description></item>
    /// </list>
    /// </remarks>
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    public unsafe struct NativeThreadData<T> : IDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeThreadData<T>* _unsafePerThreadData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_SafetyIndexHint;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeThreadData<T>>();
#endif

        public bool IsCreated => _unsafePerThreadData != null && _unsafePerThreadData->IsCreated;

        public int Length => _unsafePerThreadData->Length;
        
        public NativeThreadData(AllocatorManager.AllocatorHandle allocator)
        {
            this = default;
            AllocatorManager.AllocatorHandle temp = allocator;
            Initialize(ref temp);
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(AllocatorManager.AllocatorHandle) })]
        private void Initialize<TAllocator>(ref TAllocator allocator)
            where TAllocator : unmanaged, AllocatorManager.IAllocator
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionChecks.CheckAllocator(allocator.Handle);
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            CollectionChecks.InitNativeContainer<T>(m_Safety);
            CollectionHelper.SetStaticSafetyId<NativeThreadData<T>>(ref m_Safety, ref s_staticSafetyId.Data);
            m_SafetyIndexHint = allocator.Handle.AddSafetyHandle(m_Safety);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif

            _unsafePerThreadData = UnsafeThreadData<T>.Create(ref allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetThreadDataRef(int threadIndex)
        {
            CheckWrite();
            return ref _unsafePerThreadData->GetUnsafeThreadData(threadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            _unsafePerThreadData->Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(in T value)
        {
            CheckWrite();
            _unsafePerThreadData->Clear(in value);
        }

        public ThreadReader AsThreadReader()
        {
            CheckRead();
            return new ThreadReader(ref this);
        }

        public ThreadWriter AsThreadWriter()
        {
            CheckWrite();
            return new ThreadWriter(ref this);
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
            UnsafeThreadData<T>.Destroy(_unsafePerThreadData);
            _unsafePerThreadData = null;
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

        [NativeContainer]
        [GenerateTestsForBurstCompatibility]
        [NativeContainerIsAtomicWriteOnly]
        public struct ThreadWriter
        {
            private UnsafeThreadData<T>.ThreadWriter threadWriter;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadWriter>();
#endif

            internal ThreadWriter(ref NativeThreadData<T> data)
            {
                threadWriter = data._unsafePerThreadData->AsThreadWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = data.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.PerThreadData.ThreadWriter");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(in T data)
            {
                CheckWrite();
                threadWriter.Set(in data);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref T GetRef()
            {
                CheckWrite();
                return ref threadWriter.GetRef();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CheckWrite()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        [GenerateTestsForBurstCompatibility]
        public struct ThreadReader
        {
            private UnsafeThreadData<T>.ThreadReader threadReader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
            private static readonly SharedStatic<int> staticSafetyId = SharedStatic<int>.GetOrCreate<ThreadReader>();
#endif

            internal ThreadReader(ref NativeThreadData<T> data)
            {
                threadReader = data._unsafePerThreadData->AsThreadReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = data.m_Safety;
                CollectionAccess.SetStaticSafetyId(ref m_Safety, ref staticSafetyId.Data, "NZCore.PerThreadData.ThreadReader");
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Get()
            {
                CheckRead();
                return threadReader.Get();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CheckRead()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            }
        }

        public struct Enumerator : IEnumerator
        {
            private UnsafeThreadData<T>.Enumerator enumerator;

            internal Enumerator(ref NativeThreadData<T> data)
            {
                enumerator = data._unsafePerThreadData->GetEnumerator();
            }

            public void Dispose()
            {
                enumerator.Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return enumerator.MoveNext();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                enumerator.Reset();
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => enumerator.Current;
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Current;
            }
        }
    }
}
