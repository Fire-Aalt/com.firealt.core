using System;
using System.Collections.Generic;
using FireAlt.Core.Internal;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core.Extensions
{
    public static unsafe class ParallelHashMapExtensions
    {
        public static ref TValue GetValueByRef<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map, TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return ref map.GetHashMapStorage().GetBuffer()->GetValueByRef<TKey, TValue>(key);
        }
        
        internal static ref TValue GetValueByRef<TKey, TValue>(this ref UnsafeParallelHashMapData data, TKey key)
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (data.allocatedIndexLength <= 0)
            {
                throw new KeyNotFoundException();
            }
#endif

            // First find the slot based on the hash
            var buckets = (int*)data.buckets;
            var bucket = key.GetHashCode() & data.bucketCapacityMask;
            var entryIdx = buckets[bucket];

            var nextPtrs = (int*)data.next;
            while (!UnsafeUtility.ReadArrayElement<TKey>(data.keys, entryIdx).Equals(key))
            {
                entryIdx = nextPtrs[entryIdx];
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (entryIdx < 0 || entryIdx >= data.keyCapacity)
                {
                    throw new KeyNotFoundException();
                }
#endif
            }

            // Read the value
            return ref UnsafeUtility.ArrayElementAsRef<TValue>(data.values, entryIdx);
        }
        
        public static UnsafeParallelMultiHashMap<TKey, TValue>.ParallelWriter AsParallelWriter<TKey, TValue>(this UnsafeParallelMultiHashMap<TKey, TValue> map, int threadIndex)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var writer = map.AsParallelWriter();

            writer.GetThreadIndex() = threadIndex;


            return writer;
        }
        
        public static void EnsureMinCapacity<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map, int minCapacity)
            where TKey : unmanaged,
            IEquatable<TKey> where TValue : unmanaged
        {
            var newCapacity = math.max(map.Capacity, minCapacity);
        
            while (map.Capacity < newCapacity)
            {
                map.Capacity *= 2;
            }
        }
        
        public static void EnsureCapacity<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map, int newDataCount)
            where TKey : unmanaged,
            IEquatable<TKey> where TValue : unmanaged
        {
            int newCount = map.Count() + newDataCount;

            while (map.Capacity < newCount)
            {
                map.Capacity *= 2;
            }
        }
        
        public static void EnsureCapacity<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> map, int newDataCount) where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
        {
            int newCount = map.Count() + newDataCount;

            while (map.Capacity < newCount)
            {
                map.Capacity *= 2;
            }
        }
        
        public static void ToNativeLists<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map,
            ref NativeList<TKey> keyList, ref NativeList<TValue> valueList, int offset = 0) 
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            map.GetHashMapStorage().ConvertToList(ref keyList, ref valueList, offset);
        }
        
        private static unsafe void ConvertToList<TKey, TValue>(this UnsafeParallelHashMap<TKey, TValue> data, 
            ref NativeList<TKey> keyList, ref NativeList<TValue> valueList, int offset)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var dataCount = data.Count();
            
            if (keyList.Capacity < dataCount)
            {
                keyList.Capacity = dataCount;
                valueList.Capacity = dataCount;
            }
            GetKeyValueArrays(data.GetBuffer(), dataCount, keyList.AsArray(), valueList.AsArray(), offset);
        }
        
        public static void ToNativeArrays<TKey, TValue>(this NativeParallelHashMap<TKey, TValue> map,
            ref NativeArray<TKey> keyArray, ref NativeArray<TValue> valueArray, int offset = 0) 
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            map.GetHashMapStorage().ConvertToArray(ref keyArray, ref valueArray, offset);
        }
        
        private static unsafe void ConvertToArray<TKey, TValue>(this UnsafeParallelHashMap<TKey, TValue> data, 
            ref NativeArray<TKey> keyList, ref NativeArray<TValue> valueList, int offset)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            GetKeyValueArrays(data.GetBuffer(), data.Count(), keyList, valueList, offset);
        }
        
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        private static unsafe void GetKeyValueArrays<TKey, TValue>(UnsafeParallelHashMapData* data, int dataCount,
            NativeArray<TKey> keyList, NativeArray<TValue> valueList, int offset)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            var bucketArray = (int*)data->buckets;
            var bucketNext = (int*)data->next;

            for (int i = 0, count = 0, max = dataCount, capacityMask = data->bucketCapacityMask
                 ; i <= capacityMask && count < max
                 ; ++i
                )
            {
                int bucket = bucketArray[i];

                while (bucket != -1)
                {
                    keyList[offset + count] = UnsafeUtility.ReadArrayElement<TKey>(data->keys, bucket);
                    valueList[offset + count] = UnsafeUtility.ReadArrayElement<TValue>(data->values, bucket);
                    count++;
                    bucket = bucketNext[bucket];
                }
            }
        }
    }
}