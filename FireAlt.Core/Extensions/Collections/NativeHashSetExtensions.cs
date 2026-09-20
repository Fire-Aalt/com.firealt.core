using System;
using FireAlt.Core.Internal;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Extensions
{
    public static class NativeHashSetExtensions
    {
        public static unsafe void ToNativeList<T>(this NativeHashSet<T> set, ref NativeList<T> list) where T : unmanaged, IEquatable<T>
        {
            var data = set.GetData();
            data->ConvertToList(ref list);
        }
        
        private static unsafe void ConvertToList<T>(this HashMapHelper<T> set, ref NativeList<T> list) where T : unmanaged, IEquatable<T>
        {
            list.Clear();
            if (list.Capacity < set.Count)
                list.Capacity = set.Count;
            
            for (int i = 0, count = 0, max = set.Count, capacity = set.BucketCapacity
                 ; i < capacity && count < max
                 ; ++i
                )
            {
                int bucket = set.Buckets[i];

                while (bucket != -1)
                {
                    list.Add(UnsafeUtility.ReadArrayElement<T>(set.Keys, bucket));
                    count++;
                    bucket = set.Next[bucket];
                }
            }
        }
    }
}
