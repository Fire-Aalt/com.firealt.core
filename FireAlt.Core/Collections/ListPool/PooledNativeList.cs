using FireAlt.Core.Internal;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// Entry point for renting pooled <see cref="NativeList{T}" /> wrappers.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    public struct NativeListPool<T>
        where T : unmanaged
    {
        /// <summary>
        /// Rents a pooled <see cref="NativeList{T}" />.
        /// </summary>
        /// <param name="minCapacity">The minimum element capacity required by the caller.</param>
        /// <returns>A pooled native-list wrapper that must be disposed to return its backing allocation.</returns>
        /// <remarks>
        /// The wrapper owns a temporary <see cref="NativeList{T}" /> safety handle.
        /// </remarks>
        public static PooledNativeList<T> Rent(int minCapacity = 0)
        {
            return default(PooledNativeList<T>).Create(minCapacity);
        }
    }
    
    /// <summary>
    /// Disposable owner for a rented <see cref="NativeList{T}" />.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <remarks>
    /// The native list is a temporary safe wrapper around a pooled unsafe-list header. Disposing the wrapper releases
    /// its safety handle and returns the unsafe-list header and backing buffer to the shared pool.
    /// </remarks>
    public unsafe struct PooledNativeList<T> : IDisposable
        where T : unmanaged
    {
        private NativeList<T> _list;

        /// <summary>
        /// Gets the rented native list.
        /// </summary>
        /// <value>The native-list wrapper for the pooled unsafe-list data.</value>
        public NativeList<T> List => _list;
        
        internal PooledNativeList<T> Create(int minCapacity)
        {
            _list = CollectionAccess.CreateNativeList<T>(ListPool.RentUnsafeList<T>(minCapacity), ListPool.Pool.Data.Allocator);

            return this;
        }

        /// <summary>
        /// Resizes the rented list and returns a native array view over its current buffer.
        /// </summary>
        /// <param name="length">The desired list length.</param>
        /// <param name="options">Whether newly exposed memory should be cleared.</param>
        /// <returns>A native array aliasing the rented list buffer.</returns>
        public NativeArray<T> AsArray(int length, NativeArrayOptions options = NativeArrayOptions.ClearMemory) 
        {
            _list.Resize(length, options);
            return _list.AsArray();
        }
        
        /// <summary>
        /// Returns the rented native list to the thread-local pool.
        /// </summary>
        public void Dispose()
        {
            if (!_list.IsCreated)
            {
                return;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(_list.GetSafety());
            AtomicSafetyHandle.Release(_list.GetSafety());
#endif

            ListPool.ReturnUnsafeList(new Ref<UnsafeList<T>>(_list.GetListData()));
            _list = default;
        }
    }
}