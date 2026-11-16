using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FireAlt.Core.Extensions;
using FireAlt.Core.Internal;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FireAlt.Core
{
    public struct SpatialMap<T> : IDisposable
        where T : unmanaged, ISpatialPosition
    {
        private NativeParallelMultiHashMap<uint, int> _map;
        private SpatialMapConfig _config;
        
        public SpatialMapConfig Config => _config;
        
        public SpatialMap(float quantizeStep, Allocator allocator = Allocator.Persistent)
        {
            _config = new SpatialMapConfig(quantizeStep);
            _map = new NativeParallelMultiHashMap<uint, int>(64, allocator);
        }
        
        public bool IsCreated => _map.IsCreated;

        /// <inheritdoc />
        public void Dispose()
        {
            _map.Dispose();
        }

        /// <summary> Queue disposing after a dependency. </summary>
        /// <param name="dependency"> The dependency. </param>
        public void Dispose(JobHandle dependency)
        {
            _map.Dispose(dependency);
        }

        public JobHandle Build(NativeList<T> positions, float2 worldOrigin, JobHandle dependency, ResizeNativeParallelHashMapJob resizeStub = default, QuantizeJob quantizeStub = default)
        {
            return Build(positions.AsDeferredJobArray(), worldOrigin, dependency, resizeStub, quantizeStub);
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Global", Justification = "Sneaky way to allow this to run in bursted ISystem")]
        public JobHandle Build(NativeArray<T> positions, float2 worldOrigin, JobHandle dependency, ResizeNativeParallelHashMapJob resizeStub = default, QuantizeJob quantizeStub = default)
        {
            _config.OriginCell = _config.WorldToCell(worldOrigin);
            
            // Deferred native arrays are supported so we must part it into the job to get the length
            dependency = new ResizeNativeParallelHashMapJob
            {
                Length = positions,
                Map = _map,
            }
            .Schedule(dependency);

            var workers = math.max(1, JobsUtility.JobWorkerCount);
            dependency = new QuantizeJob
            {
                Positions = positions,
                Map = _map,
                Config = _config,
                Workers = workers,
            }
            .ScheduleParallel(workers, 1, dependency);

            dependency = new SpatialMap.CalculateMap
            {
                SpatialHashMap = _map,
            }
            .Schedule(dependency);

            return dependency;
        }

        /// <summary> Gets a readonly copy of the struct that can be used to query the spatial hash map. Also includes methods to quantize and hash. </summary>
        /// <returns> A readonly container. </returns>
        public SpatialMap.ReadOnly AsReadOnly()
        {
            return new SpatialMap.ReadOnly(_config, _map);
        }

        // Jobs outside to avoid generic issues
        [BurstCompile]
        public struct ResizeNativeParallelHashMapJob : IJob
        {
            public NativeParallelMultiHashMap<uint, int> Map;

            [ReadOnly]
            public NativeArray<T> Length;

            public void Execute()
            {
                if (Map.Capacity < Length.Length)
                {
                    Map.Capacity = Length.Length;
                }

                Map.Clear();
                Map.SetAllocatedIndexLength(Length.Length);
            }
        }

        [BurstCompile]
        [NoAlias]
        public unsafe struct QuantizeJob : IJobFor
        {
            [ReadOnly]
            public NativeArray<T> Positions;

            [NativeDisableParallelForRestriction]
            public NativeParallelMultiHashMap<uint, int> Map;

            public SpatialMapConfig Config;

            public int Workers;

            public void Execute(int index)
            {
                var length = Positions.Length / Workers;
                var start = index * length;
                var end = start + length;
                if (index == Workers - 1)
                {
                    // Last thread handles remainder
                    end += Positions.Length % Workers;
                }

                var data = Map.GetMultiHashMapStorage().GetBuffer();
                var keys = (uint*)data->keys;
                var values = (int*)data->values;
                
                for (var entityInQueryIndex = start; entityInQueryIndex < end; entityInQueryIndex++)
                {
                    var position = Positions[entityInQueryIndex].Position;
                    var quantized = Config.Quantized(position);
                    
                    ValidatePosition(position, quantized);
                    
                    var hashed = SpatialMap.Hash(quantized);
                    keys[entityInQueryIndex] = hashed;
                    values[entityInQueryIndex] = entityInQueryIndex;
                }
            }
            
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [Conditional("UNITY_DOTS_DEBUG")]
            private void ValidatePosition(float2 position, uint2 quantized)
            {
                if (math.any(quantized >= SpatialMap.Size))
                {
                    var min = new int2(-SpatialMap.HalfSize) + Config.OriginCell;
                    var max = new int2(SpatialMap.HalfSize - 1) + Config.OriginCell;

                    UnityEngine.Debug.LogError($"Position {position} is outside the size of the world, min={min} max={max}");
                    throw new ArgumentException($"Position {position} is outside the size of the world, min={min} max={max}");
                }
            }
        }
    }

    public struct SpatialMapConfig
    {
        public readonly float QuantizeStep;
        public readonly int CellOffset;
        public int2 OriginCell;
        
        public SpatialMapConfig(float quantizeStep)
        {
            QuantizeStep = quantizeStep;
            OriginCell = int2.zero;
            CellOffset = (int)math.floor(SpatialMap.HalfSize / QuantizeStep);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly uint2 Quantized(float2 position)
        {
            return SpatialMap.Quantized(position, QuantizeStep, OriginCell);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly uint Hash(uint2 quantized)
        {
            return SpatialMap.Hash(quantized);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int2 QuantizedToCell(uint2 quantized)
        {
            return new int2((int)quantized.x, (int)quantized.y) - CellOffset + OriginCell;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 QuantizedToWorld(uint2 quantized)
        {
            return CellToWorld(QuantizedToCell(quantized)) + QuantizeStep * 0.5f;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 Snap(float2 position)
        {
            return SpatialMap.Snap(position, QuantizeStep);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int2 WorldToCell(float2 world)
        {
            return SpatialMap.WorldToCell(world, QuantizeStep);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float2 CellToWorld(int2 cell)
        {
            return SpatialMap.CellToWorld(cell, QuantizeStep);
        }
    }

    public static class SpatialMap
    {
        public const int Size = ushort.MaxValue;
        public const int HalfSize = Size / 2;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 Quantized(float2 position, float step, int2 originCell)
        {
            return new uint2(math.floor((position + HalfSize) / step - originCell));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint2 quantized)
        {
            return quantized.x + quantized.y * Size;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 QuantizedToCell(uint2 quantized, float step, int2 originCell)
        {
            var offset = (int)math.floor(HalfSize / step);
            return new int2((int)quantized.x, (int)quantized.y) - offset + originCell;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 WorldToCell(float2 world, float step)
        {
            return (int2)math.floor(world / step);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 CellToWorld(int2 cell, float step)
        {
            return new float2(cell.x, cell.y) * step;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Snap(float2 position, float step)
        {
            return math.floor(position / step) * step;
        }
        
        /// <summary> Readonly copy for querying the map. </summary>
        public readonly struct ReadOnly
        {
            private readonly SpatialMapConfig _config;

            public ReadOnly(SpatialMapConfig config, NativeParallelMultiHashMap<uint, int> indexMap)
            {
                _config = config;
                IndexMap = indexMap;
            }

            [field: ReadOnly]
            public NativeParallelMultiHashMap<uint, int> IndexMap { get; }
            
            public SpatialMapConfig Config => _config;
            public float QuantizeStep => _config.QuantizeStep;
            public int2 OriginCell => _config.OriginCell;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint2 Quantized(float2 position)
            {
                return _config.Quantized(position);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Hash(uint2 quantized)
            {
                return SpatialMap.Hash(quantized);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int2 QuantizedToCell(uint2 quantized)
            {
                return _config.QuantizedToCell(quantized);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 Snap(float2 position)
            {
                return _config.Snap(position);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int2 WorldToCell(float2 world)
            {
                return _config.WorldToCell(world);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 CellToWorld(int2 cell)
            {
                return _config.CellToWorld(cell);
            }
        }
        
        [BurstCompile]
        internal struct CalculateMap : IJob
        {
            public NativeParallelMultiHashMap<uint, int> SpatialHashMap;

            public void Execute()
            {
                SpatialHashMap.RecalculateBuckets();
            }
        }
    }
}
