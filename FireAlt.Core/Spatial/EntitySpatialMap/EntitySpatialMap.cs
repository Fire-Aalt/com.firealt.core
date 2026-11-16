using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct EntitySpatialMap : IDisposable
    {
        public SpatialMap<SpatialPosition> Spatial;
        public NativeList<SpatialPosition> Positions;
        public NativeList<Entity> Entities;

        public int Count { get; internal set; }
        
        public bool IsCreated => Spatial.IsCreated;
        
        public EntitySpatialMap(float quantizeStep, Allocator allocator = Allocator.Persistent)
        {
            Spatial = new SpatialMap<SpatialPosition>(quantizeStep, allocator);
            Positions = new NativeList<SpatialPosition>(64, allocator);
            Entities = new NativeList<Entity>(64, allocator);
            Count = 0;
        }
        
        public void Dispose()
        {
            Spatial.Dispose();
        }
        
        /// <summary> Gets a readonly copy of the struct that can be used to query the spatial hash map. Also includes methods to quantize and hash. </summary>
        /// <returns> A readonly container. </returns>
        public ReadOnly AsReadOnly()
        {
            return new ReadOnly(this);
        }
        
        /// <summary> Readonly copy for querying the map. </summary>
        public readonly struct ReadOnly
        {
            public ReadOnly(EntitySpatialMap entitySpatialMap)
            {
                Spatial = entitySpatialMap.Spatial.AsReadOnly();
                Positions = entitySpatialMap.Positions;
                Entities = entitySpatialMap.Entities;
            }

            public int Count => Positions.Length;
            public SpatialMapConfig Config => Spatial.Config;
            public float QuantizeStep => Spatial.QuantizeStep;
            public int2 OriginCell => Spatial.OriginCell;
            public NativeParallelMultiHashMap<uint, int> IndexMap => Spatial.IndexMap;
            
            public SpatialMap.ReadOnly Spatial { get; }
            [field: ReadOnly] public NativeList<SpatialPosition> Positions { get; }
            [field: ReadOnly] public NativeList<Entity> Entities { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint2 Quantized(float2 position)
            {
                return Spatial.Quantized(position);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Hash(uint2 quantized)
            {
                return Spatial.Hash(quantized);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int2 QuantizedToCell(uint2 quantized)
            {
                return Spatial.QuantizedToCell(quantized);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 Snap(float2 position)
            {
                return Spatial.Snap(position);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int2 WorldToCell(float2 world)
            {
                return Spatial.WorldToCell(world);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 CellToWorld(int2 cell)
            {
                return Spatial.CellToWorld(cell);
            }
        }
    }
}