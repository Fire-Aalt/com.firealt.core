using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FireAlt.Core
{
    public struct SpatialPosition : ISpatialPosition
    {
        public float3 Position;

        float2 ISpatialPosition.Position => Position.xz;
    }

    public struct EntitySpatialMapBuilder
    {
        private EntityQuery _query;
        private ComponentTypeHandle<LocalTransform> _transformHandle;

        /// <summary> Initializes a new instance of the <see cref="EntitySpatialMapBuilder" /> struct. </summary>
        /// <param name="state"> The owning state. </param>
        /// <param name="query"> Query of entities to use. </param>
        public EntitySpatialMapBuilder(ref SystemState state, EntityQuery query)
        {
            _query = query;
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
        }

        public JobHandle GatherAndBuild(ref SystemState state, float3 worldOrigin, ref EntitySpatialMap entitySpatialMap, JobHandle dependency)
        {
            var count = _query.CalculateEntityCount();
            entitySpatialMap.Count = count;
            dependency = Gather(ref state, count, dependency, ref entitySpatialMap.Positions);
            
            var mapDependency = entitySpatialMap.Spatial.Build(entitySpatialMap.Positions, worldOrigin.xz, dependency);
            var asyncEntities = _query.ToEntityListAsync(state.WorldUpdateAllocator,
                dependency, out var entitiesDependency);
            
            entitiesDependency = new CopyJob
            {
                AsyncEntities = asyncEntities,
                SpatialMapEntities = entitySpatialMap.Entities
            }.Schedule(entitiesDependency);
            
            return JobHandle.CombineDependencies(mapDependency, entitiesDependency);
        }

        private JobHandle Gather(ref SystemState state, int entitiesCount, JobHandle dependency, ref NativeList<SpatialPosition> positions)
        {
            _transformHandle.Update(ref state);

            positions.Resize(entitiesCount, NativeArrayOptions.UninitializedMemory);

            var firstEntityIndices = _query.CalculateBaseEntityIndexArrayAsync(state.WorldUpdateAllocator, dependency, out var dependency1);
            dependency = JobHandle.CombineDependencies(dependency, dependency1);

            dependency = new GatherPositionsJob
            {
                TransformHandle = _transformHandle,
                Positions = positions,
                FirstEntityIndices = firstEntityIndices,
            }
            .ScheduleParallel(_query, dependency);

            return dependency;
        }

        [BurstCompile]
        private unsafe struct GatherPositionsJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<LocalTransform> TransformHandle;

            [NativeDisableParallelForRestriction]
            public NativeList<SpatialPosition> Positions;

            [ReadOnly]
            public NativeArray<int> FirstEntityIndices;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var ptr = (float3*)Positions.GetUnsafePtr();
                var dst = ptr + FirstEntityIndices[unfilteredChunkIndex];

                var size = UnsafeUtility.SizeOf<float3>();
                var positions = chunk.GetNativeArray(ref TransformHandle).Slice().SliceWithStride<float3>(0);

                UnsafeUtility.MemCpyStride(dst, size, positions.GetUnsafeReadOnlyPtr(), positions.Stride, size, positions.Length);
            }
        }
        
        [BurstCompile]
        private struct CopyJob : IJob
        {
            [ReadOnly]
            public NativeList<Entity> AsyncEntities;
            
            public NativeList<Entity> SpatialMapEntities;

            public void Execute()
            {
                SpatialMapEntities.CopyFrom(AsyncEntities);
            }
        }
    }
}
