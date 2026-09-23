using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Scripting.LifecycleManagement;

namespace FireAlt.Core.Collections
{
    [NoAutoStaticsCleanup]
    internal static partial class ListPool
    {
        // Retains at most about 1 MiB of pooled list buffers per worker thread.
        private const int MAX_POOL_SIZE_PER_THREAD = 32;
        private const int MAX_BYTES_PER_LIST = 64 * 1024;

        internal static readonly SharedStatic<Data> Pool = SharedStatic<Data>.GetOrCreate<Data>();

        internal static unsafe Ref<UnsafeList<T>> RentUnsafeList<T>(int minCapacity)
            where T : unmanaged
        {
            ref var data = ref Pool.Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!data.IsCreated)
            {
                throw new InvalidOperationException("ListPool is not initialized.");
            }
#endif

            ref var lp = ref data.GetThreadList();
            if (lp.Length == 0)
            {
                var listData = AllocatorManager.Allocate<UnsafeList<T>>(Pool.Data.Allocator);
                *listData = new UnsafeList<T>(minCapacity, data.Allocator);
                return new Ref<UnsafeList<T>>(listData);
            }

            var byteList = lp[^1];
            lp.RemoveAt(lp.Length - 1);
            
            var list = UnsafeUtility.As<Ref<UnsafeList<byte>>, Ref<UnsafeList<T>>>(ref byteList);
            var capacity = byteList.Value.Capacity / UnsafeUtility.SizeOf<T>();
            if (capacity < minCapacity)
            {
                list.Value.Capacity = minCapacity;
            }
            else
            {
                list.Value.m_capacity = capacity;
            }

            list.Value.m_length = 0;
            return list;
        }
        
        internal static unsafe void ReturnUnsafeList<T>(Ref<UnsafeList<T>> list)
            where T : unmanaged
        {
            ref var refByteList = ref UnsafeUtility.As<Ref<UnsafeList<T>>, Ref<UnsafeList<byte>>>(ref list);
            ref var byteList = ref refByteList.Value;
            byteList.Clear();

            byteList.m_capacity = list.Value.Capacity * UnsafeUtility.SizeOf<T>();
            byteList.m_length = 0;

            ref var lp = ref Pool.Data.GetThreadList();
            if (lp.Length < MAX_POOL_SIZE_PER_THREAD && byteList.Capacity < MAX_BYTES_PER_LIST)
            {
                lp.Add(refByteList);
            }
            else
            {
                byteList.Dispose();
                AllocatorManager.Free(Pool.Data.Allocator, refByteList.GetUnsafePtr());
            }
        }

        /// <summary>
        /// Initializes the global shared list-pool storage.
        /// </summary>
        /// <remarks>
        /// The pool keeps one byte-list stack per Unity worker thread. Both native and unsafe pooled wrappers rent
        /// from these stacks so all element types can reuse compatible backing allocations.
        /// </remarks>
        [OnCodeLoaded]
        public static void Initialize()
        {
            if (Pool.Data.IsCreated)
            {
                return;
            }

            Pool.Data = new Data(Allocator.Domain);
        }

        internal struct Data
        {
            public AllocatorManager.AllocatorHandle Allocator;
            private NativeThreadData<ThreadData> _buffer;

            public Data(AllocatorManager.AllocatorHandle allocator)
            {
                Allocator = allocator;
                _buffer = new NativeThreadData<ThreadData>(allocator);
                
                for (var i = 0; i < _buffer.Length; i++)
                {
                    _buffer.GetThreadDataRef(i).ThreadList = new UnsafeList<Ref<UnsafeList<byte>>>(0, allocator);
                }
            }

            public readonly bool IsCreated => _buffer.IsCreated;

            public ref UnsafeList<Ref<UnsafeList<byte>>> GetThreadList()
            {
#if UNITY_EDITOR
                UnityEngine.Debug.Assert(JobsUtility.IsExecutingJob || UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread(),
                    "Can only be used on main or worker threads");
#endif
                return ref _buffer.GetThreadDataRef(JobsUtility.ThreadIndex).ThreadList;
            }

            public unsafe void Dispose()
            {
                if (!IsCreated)
                {
                    return;
                }

                foreach (var threadData in _buffer)
                {
                    foreach (var list in threadData.ThreadList)
                    {
                        list.Value.Dispose();
                        AllocatorManager.Free(Pool.Data.Allocator, list.GetUnsafePtr());
                    }
                    threadData.ThreadList.Dispose();
                }
                _buffer = default;
            }
        }

        private struct ThreadData
        {
            public UnsafeList<Ref<UnsafeList<byte>>> ThreadList;
        }
    }
}
