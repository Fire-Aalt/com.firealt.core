using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace FireAlt.Core.Extensions
{
    public static class NativeParallelMultiHashMapExtensions
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