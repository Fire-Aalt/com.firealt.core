using System;
using FireAlt.Core.Collections;
using Unity.Collections;
using Unity.Entities;

namespace FireAlt.Core
{
    public struct SyncTransformToEntityContainer : IComponentData, IDisposable
    {
        public ReusableTransformAccessArray<Entity> ReusableTransformAccessArray;
        
        public SyncTransformToEntityContainer(int initialCapacity, Allocator allocator)
        {
            ReusableTransformAccessArray = new ReusableTransformAccessArray<Entity>(initialCapacity, allocator);
        }
        
        public void Dispose()
        {
            ReusableTransformAccessArray.Dispose();
        }
    }
}