using System;
using FireAlt.Core.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace FireAlt.Core.Extensions
{
    public static unsafe class NativeParallelMultiHashMapExtensions
    {
        public static JobHandle Clear<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> multiHashMap, JobHandle dependency, 
            ClearNativeParallelMultiHashMapJob<TKey, TValue> _ = default)
            where TValue : unmanaged
            where TKey : unmanaged, IEquatable<TKey>
        {
            return new ClearNativeParallelMultiHashMapJob<TKey, TValue>
            {
                MultiHashMap = multiHashMap,
            }.Schedule(dependency);
        }

        /// <summary> Marks the first <paramref name="length" /> key/value slots as used without linking them into buckets. </summary>
        internal static void SetAllocatedIndexLength<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap, int length)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            hashMap.GetMultiHashMapStorage().GetBuffer()->allocatedIndexLength = length;
        }

        /// <summary> Links every used slot into its bucket. Expects cleared buckets and directly written keys. </summary>
        internal static void RecalculateBuckets<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashMap)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(hashMap.GetSafety());
#endif
            var data = hashMap.GetMultiHashMapStorage().GetBuffer();
            var buckets = (int*)data->buckets;
            var nextPtrs = (int*)data->next;
            var keys = (TKey*)data->keys;

            for (var idx = 0; idx < data->allocatedIndexLength; idx++)
            {
                var bucket = keys[idx].GetHashCode() & data->bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }
        }
        
        [BurstCompile]
        public struct ClearNativeParallelMultiHashMapJob<TKey, TValue> : IJob
            where TValue : unmanaged
            where TKey : unmanaged, IEquatable<TKey>
        {
            public NativeParallelMultiHashMap<TKey, TValue> MultiHashMap;
            
            public void Execute()
            {
                MultiHashMap.Clear();
            }
        }
    }
}