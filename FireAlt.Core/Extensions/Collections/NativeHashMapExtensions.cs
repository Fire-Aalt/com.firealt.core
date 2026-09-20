using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FireAlt.Core.Internal;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;

namespace FireAlt.Core.Extensions
{
    public static unsafe class NativeHashMapExtensions
    {
        public static ref TValue GetValueAsRef<TKey, TValue>(
            this NativeHashMap<TKey, TValue> hashMap,
            in TKey key)
            where TValue : unmanaged
            where TKey : unmanaged, IEquatable<TKey>
        {
            var data = hashMap.GetData();
            int idx = data->Find(key);

            if (-1 != idx)
            {
                return ref UnsafeUtility.ArrayElementAsRef<TValue>(data->Ptr, idx);
            }
            
            throw new KeyNotFoundException($"Key '{key}' not found in the NativeHashMap.");
        }
        
        public static TValue* GetValueAsPointer<TKey, TValue>(
            this NativeHashMap<TKey, TValue> hashMap,
            in TKey key)
            where TValue : unmanaged
            where TKey : unmanaged, IEquatable<TKey>
        {
            var data = hashMap.GetData();
            int idx = data->Find(key);

            if (-1 != idx)
            {
                return (TValue*)UnsafeUtility.AddressOf(ref UnsafeUtility.ArrayElementAsRef<TValue>(data->Ptr, idx));
            }
            
            throw new KeyNotFoundException($"Key '{key}' not found in the NativeHashMap.");
        }
        
        internal static int AddNoFind<TKey>(this ref HashMapHelper<TKey> hashMapHelper, in TKey key)
            where TKey : unmanaged, IEquatable<TKey>
        {
            // Allocate an entry from the free list
            if (hashMapHelper.AllocatedIndex >= hashMapHelper.Capacity && hashMapHelper.FirstFreeIdx < 0)
            {
                var newCap = hashMapHelper.CalcCapacityCeilPow2(hashMapHelper.Capacity + (1 << hashMapHelper.Log2MinGrowth));
                hashMapHelper.ResizeMulti(newCap);
            }

            var idx = hashMapHelper.FirstFreeIdx;

            if (idx >= 0)
            {
                hashMapHelper.FirstFreeIdx = hashMapHelper.Next[idx];
            }
            else
            {
                idx = hashMapHelper.AllocatedIndex++;
            }

            CheckIndexOutOfBounds(idx, hashMapHelper.Capacity);

            UnsafeUtility.WriteArrayElement(hashMapHelper.Keys, idx, key);
            var bucket = hashMapHelper.GetBucket(key);

            // Add the index to the hash-map
            var next = hashMapHelper.Next;
            next[idx] = hashMapHelper.Buckets[bucket];
            hashMapHelper.Buckets[bucket] = idx;
            hashMapHelper.Count++;

            return idx;
        }
        
        private static void ResizeMulti<TKey>(this ref HashMapHelper<TKey> hashMapHelper, int newCapacity)
            where TKey : unmanaged, IEquatable<TKey>
        {
            newCapacity = math.max(newCapacity, hashMapHelper.Count);
            var newBucketCapacity = math.ceilpow2(HashMapHelper<TKey>.GetBucketSize(newCapacity));

            if (hashMapHelper.Capacity == newCapacity && hashMapHelper.BucketCapacity == newBucketCapacity)
            {
                return;
            }

            hashMapHelper.ResizeExactMulti(newCapacity, newBucketCapacity);
        }
        
        private static void ResizeExactMulti<TKey>(this ref HashMapHelper<TKey> hashMapHelper, int newCapacity, int newBucketCapacity)
            where TKey : unmanaged, IEquatable<TKey>
        {
            var totalSize = HashMapHelper<TKey>.CalculateDataSize(newCapacity, newBucketCapacity, hashMapHelper.SizeOfTValue, out var keyOffset,
                out var nextOffset, out var bucketOffset);

            var oldPtr = hashMapHelper.Ptr;
            var oldKeys = hashMapHelper.Keys;
            var oldNext = hashMapHelper.Next;
            var oldBuckets = hashMapHelper.Buckets;
            var oldBucketCapacity = hashMapHelper.BucketCapacity;

            hashMapHelper.Ptr = (byte*)CollectionMemory.Allocate(totalSize, JobsUtility.CacheLineSize, hashMapHelper.Allocator);
            hashMapHelper.Keys = (TKey*)(hashMapHelper.Ptr + keyOffset);
            hashMapHelper.Next = (int*)(hashMapHelper.Ptr + nextOffset);
            hashMapHelper.Buckets = (int*)(hashMapHelper.Ptr + bucketOffset);
            hashMapHelper.Capacity = newCapacity;
            hashMapHelper.BucketCapacity = newBucketCapacity;

            hashMapHelper.Clear();

            for (int i = 0, num = oldBucketCapacity; i < num; ++i)
            {
                for (var idx = oldBuckets[i]; idx != -1; idx = oldNext[idx])
                {
                    var newIdx = AddNoFindNoResize(ref hashMapHelper, oldKeys[idx]);
                    UnsafeUtility.MemCpy(hashMapHelper.Ptr + (hashMapHelper.SizeOfTValue * newIdx), oldPtr + (hashMapHelper.SizeOfTValue * idx),
                        hashMapHelper.SizeOfTValue);
                }
            }

            CollectionMemory.Free(oldPtr, hashMapHelper.Allocator);
        }
        
        internal static int AddNoFindNoResize<TKey>(this ref HashMapHelper<TKey> hashMapHelper, in TKey key)
            where TKey : unmanaged, IEquatable<TKey>
        {
            CheckHasCapacity(ref hashMapHelper);

            var idx = hashMapHelper.FirstFreeIdx;

            if (idx >= 0)
            {
                hashMapHelper.FirstFreeIdx = hashMapHelper.Next[idx];
            }
            else
            {
                idx = hashMapHelper.AllocatedIndex++;
            }

            CheckIndexOutOfBounds(idx, hashMapHelper.Capacity);

            UnsafeUtility.WriteArrayElement(hashMapHelper.Keys, idx, key);
            var bucket = hashMapHelper.GetBucket(key);

            // Add the index to the hash-map
            var next = hashMapHelper.Next;
            next[idx] = hashMapHelper.Buckets[bucket];
            hashMapHelper.Buckets[bucket] = idx;
            hashMapHelper.Count++;

            return idx;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetBucket<TKey>(this in HashMapHelper<TKey> hashMapHelper, in TKey key)
            where TKey : unmanaged, IEquatable<TKey>
        {
            return (int)((uint)key.GetHashCode() & (hashMapHelper.BucketCapacity - 1));
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckHasCapacity<TKey>(this ref HashMapHelper<TKey> hashMapHelper)
            where TKey : unmanaged, IEquatable<TKey>
        {
            // Allocate an entry from the free list
            if (hashMapHelper.AllocatedIndex >= hashMapHelper.Capacity && hashMapHelper.FirstFreeIdx < 0)
            {
                throw new InvalidOperationException("Capacity reached");
            }
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckIndexOutOfBounds(int idx, int capacity)
        {
            if ((uint)idx >= (uint)capacity)
            {
                throw new InvalidOperationException($"Internal HashMap error. idx {idx}");
            }
        }
    }
}