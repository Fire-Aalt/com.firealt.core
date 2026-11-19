using System;
using FireAlt.Core.Collections;
using Unity.Collections;
using Unity.Entities;

namespace FireAlt.Core
{
    public struct HybridGameObjectTransforms : IComponentData, IDisposable
    {
        public ReusableTransformAccessArray<Entity> Value;

        public HybridGameObjectTransforms(int initialCapacity, Allocator allocator)
        {
            Value = new ReusableTransformAccessArray<Entity>(initialCapacity, allocator);
        }

        public void Dispose()
        {
            Value.Dispose();
        }
    }
}
