using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace FireAlt.Core.Collections
{
    public struct ReusableTransformAccessArray<T> : IDisposable 
        where T : unmanaged
    {
        public TransformAccessArray Array => _array;
        public NativeList<T> AlignedData => _alignedData;

        private TransformAccessArray _array;
        private NativeList<T> _alignedData;
        private NativeHashSet<int> _freeIds;
        
        public ReusableTransformAccessArray(int initialCapacity, Allocator allocator)
        {
            _array = new TransformAccessArray(initialCapacity);
            _alignedData = new NativeList<T>(initialCapacity, allocator);
            _freeIds = new NativeHashSet<int>(initialCapacity, allocator);
        }
        
        public bool IsEmpty => _freeIds.Count == _alignedData.Length;
        
        /// <summary>
        /// Adds transform to the transform container,
        /// and assigns an id for the referencing
        /// </summary>
        public int AddTransformHandle(TransformHandle transformHandle, T data)
        {
            int refId;

            // If there's a free id -> use it
            if (!_freeIds.IsEmpty) 
            {
                var enumerator = _freeIds.GetEnumerator();
            
                enumerator.MoveNext();
                refId = enumerator.Current;
                enumerator.Dispose();

                _freeIds.Remove(refId);
            
                _array.SetTransformHandle(refId, transformHandle);
                _alignedData[refId] = data;
            
                return refId;
            }

            // Otherwise generate id / add transform and return new refId
            refId = _array.length;
         
            _array.Add(transformHandle);
            _alignedData.Add(data);

            return refId;
        }
        
        /// <summary>
        /// Releases transform reference from the transform container. 
        /// <remarks>Cannot be used in Burst context.</remarks>
        /// </summary>
        public Transform ReleaseTransformManaged(int id)
        {
            var transform = _array[id];
            
            _freeIds.Add(id);
            _array.SetTransformHandle(id, default);
            _alignedData[id] = default;
            
            return transform;
        }

        /// <summary>
        /// Releases transform reference from the transform container. 
        /// <remarks>Can be used in Burst context.</remarks>
        /// </summary>
        public void ReleaseTransform(int id)
        {
            _freeIds.Add(id);
            _array.SetTransformHandle(id, default);
            _alignedData[id] = default;
        }
        
        public void Dispose()
        {
            _array.Dispose();
            _alignedData.Dispose();
            _freeIds.Dispose();
        }
    }
}