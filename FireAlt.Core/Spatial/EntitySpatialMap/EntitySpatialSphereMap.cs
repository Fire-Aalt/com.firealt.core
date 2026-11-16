using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FireAlt.Core
{
    /// <summary> Sphere an entity occupies in a <see cref="EntitySpatialSphereMap" />, in its local space. </summary>
    public struct SpatialSphereShape : IComponentData
    {
        public float3 Center;
        public float Radius;
    }

    /// <summary> A <see cref="SpatialSphereMap" /> plus the entities, centers and radii it was built from. </summary>
    public struct EntitySpatialSphereMap : IDisposable
    {
        public SpatialSphereMap Spatial;
        public NativeList<float3> Positions;
        public NativeList<float> Radii;
        public NativeList<Entity> Entities;

        public EntitySpatialSphereMap(SpatialSphereMapConfig config, Allocator allocator = Allocator.Persistent)
        {
            Spatial = new SpatialSphereMap(config, allocator);
            Positions = new NativeList<float3>(64, allocator);
            Radii = new NativeList<float>(64, allocator);
            Entities = new NativeList<Entity>(64, allocator);
        }

        public bool IsCreated => Spatial.IsCreated;

        public void Dispose()
        {
            Spatial.Dispose();
            Positions.Dispose();
            Radii.Dispose();
            Entities.Dispose();
        }

        public ReadOnly AsReadOnly()
        {
            return new ReadOnly
            {
                Spatial = Spatial.AsReadOnly(),
                Entities = Entities,
            };
        }

        public struct ReadOnly
        {
            public SpatialSphereMap.ReadOnly Spatial;
            [ReadOnly] public NativeList<Entity> Entities;

            public int Count => Spatial.Count;
        }
    }

    /// <summary>
    /// Fills an <see cref="EntitySpatialSphereMap" /> from every entity of a query that has
    /// <see cref="LocalTransform" /> and <see cref="SpatialSphereShape" />. Indices follow the query order, so data
    /// gathered with EntityIndexInQuery over the same query lines up with the map.
    /// </summary>
    public struct EntitySpatialSphereMapBuilder
    {
        private EntityQuery _query;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private ComponentTypeHandle<SpatialSphereShape> _shapeHandle;
        private EntityTypeHandle _entityHandle;

        public EntitySpatialSphereMapBuilder(ref SystemState state, EntityQuery query)
        {
            _query = query;
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _shapeHandle = state.GetComponentTypeHandle<SpatialSphereShape>(true);
            _entityHandle = state.GetEntityTypeHandle();
        }

        public JobHandle GatherAndBuild(ref SystemState state, ref EntitySpatialSphereMap map, JobHandle dependency)
        {
            _transformHandle.Update(ref state);
            _shapeHandle.Update(ref state);
            _entityHandle.Update(ref state);

            var count = _query.CalculateEntityCount();
            map.Positions.ResizeUninitialized(count);
            map.Radii.ResizeUninitialized(count);
            map.Entities.ResizeUninitialized(count);

            var firstEntityIndices = _query.CalculateBaseEntityIndexArrayAsync(state.WorldUpdateAllocator,
                dependency, out var indicesDependency);
            dependency = new GatherJob
            {
                TransformHandle = _transformHandle,
                ShapeHandle = _shapeHandle,
                EntityHandle = _entityHandle,
                FirstEntityIndices = firstEntityIndices,
                Positions = map.Positions.AsArray(),
                Radii = map.Radii.AsArray(),
                Entities = map.Entities.AsArray(),
            }.ScheduleParallel(_query, indicesDependency);

            return map.Spatial.Build(map.Positions.AsArray(), map.Radii.AsArray(), dependency);
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<SpatialSphereShape> ShapeHandle;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public NativeArray<int> FirstEntityIndices;
            [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
            [NativeDisableParallelForRestriction] public NativeArray<float> Radii;
            [NativeDisableParallelForRestriction] public NativeArray<Entity> Entities;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var shapes = chunk.GetNativeArray(ref ShapeHandle);
                var entities = chunk.GetNativeArray(EntityHandle);
                var index = FirstEntityIndices[unfilteredChunkIndex];
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var transform = transforms[i];
                    var shape = shapes[i];
                    Positions[index] = transform.TransformPoint(shape.Center);
                    Radii[index] = shape.Radius * transform.Scale;
                    Entities[index] = entities[i];
                    index++;
                }
            }
        }
    }
}
