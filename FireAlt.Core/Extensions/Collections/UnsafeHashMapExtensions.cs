using FireAlt.Core.Internal;
using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Extensions
{
    public static unsafe class UnsafeHashMapExtensions
    {
        /// <summary>
        /// Gets the value for a key or adds <paramref name="defaultValue" /> and returns it by reference.
        /// </summary>
        /// <remarks>
        /// Unsafe because the returned ref points directly into the hash map storage. Consume it immediately and do not keep or use it after any later
        /// write to the same hash map, such as add, get-or-add, remove, clear, or capacity-changing operations.
        /// </remarks>
        /// <param name="hashMap"> The hash map to read or add into. </param>
        /// <param name="key"> The key to look up. </param>
        /// <param name="defaultValue"> Value to add if the key is not present. </param>
        /// <typeparam name="TKey"> The key type. </typeparam>
        /// <typeparam name="TValue"> The value type. </typeparam>
        /// <returns> A reference to the value stored in the hash map. </returns>
        public static ref TValue GetOrAddRefUnsafe<TKey, TValue>(ref this UnsafeHashMap<TKey, TValue> hashMap, TKey key, TValue defaultValue = default)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var idx = hashMap.GetData().Find(key);

            if (idx == -1)
            {
                idx = hashMap.GetData().AddNoFind(key);
                UnsafeUtility.WriteArrayElement(hashMap.GetData().Ptr, idx, defaultValue);
            }

            return ref UnsafeUtility.ArrayElementAsRef<TValue>(hashMap.GetData().Ptr, idx);
        }
        
        public static ref TValue GetValueAsRef<TKey, TValue>(ref this UnsafeHashMap<TKey, TValue> hashMap, TKey key)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var idx = hashMap.GetData().Find(key);

            if (idx != -1)
            {
                return ref UnsafeUtility.ArrayElementAsRef<TValue>(hashMap.GetData().Ptr, idx);
            }

            throw new KeyNotFoundException($"Key '{key}' not found in the NativeHashMap.");
        }
    }
}