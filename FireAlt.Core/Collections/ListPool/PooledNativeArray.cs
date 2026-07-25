using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FireAlt.Core.Collections
{
    /// <summary>
    /// Entry point for renting pooled <see cref="NativeArray{T}" /> views.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    public struct NativeArrayPool<T>
        where T : unmanaged
    {
        /// <summary>
        /// Rents a pooled <see cref="NativeArray{T}" />.
        /// </summary>
        /// <param name="length">The array length required by the caller.</param>
        /// <param name="options">Whether newly exposed memory should be cleared.</param>
        /// <returns>A pooled native-array wrapper that must be disposed to return its backing allocation.</returns>
        /// <remarks>
        /// The wrapper owns the safety handle for the returned native array view.
        /// </remarks>
        public static PooledNativeArray<T> Rent(int length, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            return default(PooledNativeArray<T>).Create(length, options);
        }
    }
    
    /// <summary>
    /// Disposable owner for a rented <see cref="NativeArray{T}" /> view.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <remarks>
    /// The native array aliases a pooled unsafe-list buffer. Disposing the wrapper releases the array safety handle
    /// and returns the unsafe-list header and backing buffer to the shared pool.
    /// </remarks>
    public unsafe struct PooledNativeArray<T> : IDisposable
        where T : unmanaged
    {
        private Ref<UnsafeList<T>> _list;
        private NativeArray<T> _array;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle _safety;
#endif

        /// <summary>
        /// Gets the rented native array.
        /// </summary>
        /// <value>The native-array view over the pooled unsafe-list data.</value>
        public NativeArray<T> Array => _array;
        
        internal PooledNativeArray<T> Create(int length, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            _array = default;
            _list = ListPool.RentUnsafeList<T>(length);
            _list.Value.Resize(length, options);

            _array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(_list.Value.Ptr, _list.Value.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _safety = CollectionHelper.CreateSafetyHandle(ListPool.Pool.Data.Allocator);
            CollectionHelper.SetStaticSafetyId<NativeArray<T>>(ref _safety, ref NativeArrayExtensions.NativeArrayStaticId<T>.s_staticSafetyId.Data);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref _array, _safety);
#endif
            return this;
        }
        
        /// <summary>
        /// Returns the rented native array backing storage to the thread-local pool.
        /// </summary>
        public void Dispose()
        {
            if (!_array.IsCreated)
            {
                return;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(_safety);
            AtomicSafetyHandle.Release(_safety);
            _safety = default;
#endif

            ListPool.ReturnUnsafeList(_list);
            _array = default;
        }
    }
}
