using FireAlt.Core.Collections;
using FireAlt.Core.Groups;
using FireAlt.Core.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace FireAlt.Core
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(BeforeTransformSystemGroup))]
    [UpdateAfter(typeof(SyncHybridEntityManagedSystem))]
    public partial struct SyncHybridEntitySystem : ISystem
    {
        private static readonly ProfilerMarker CleanupMarker = new("Cleanup Old Entities");
        private static readonly ProfilerMarker SyncMarker = new("Schedule Sync Job");
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<SyncTransformToEntityContainer>().ValueRW;
            
            CleanupMarker.Begin();
            var cleanupQuery = SystemAPI.QueryBuilder().WithAll<SyncTransformToEntity>().WithAbsent<HybridEntitySync>().Build();
            if (!cleanupQuery.IsEmpty)
            {
                foreach (var link in SystemAPI.Query<RefRO<SyncTransformToEntity>>()
                             .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
                {
                    singleton.ReusableTransformAccessArray.ReleaseTransform(link.ValueRO.TransformId);
                }
                state.EntityManager.RemoveComponent<SyncTransformToEntity>(cleanupQuery);
            }
            CleanupMarker.End();
            
            SyncMarker.Begin();
            state.Dependency = new SyncTransformsJob
            {
                Entities = singleton.ReusableTransformAccessArray.AlignedData.AsDeferredJobArray(),
                LocalToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(),
                LocalTransform = SystemAPI.GetComponentLookup<LocalTransform>(),
                PostTransformMatrix = SystemAPI.GetComponentLookup<PostTransformMatrix>(),
            }.ScheduleReadOnly(singleton.ReusableTransformAccessArray.Array, 64, state.Dependency);
            SyncMarker.End();
        }

        [BurstCompile]
        private struct SyncTransformsJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;
            
            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalToWorld> LocalToWorld;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalTransform> LocalTransform;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<PostTransformMatrix> PostTransformMatrix;
            
            public void Execute(int index, [ReadOnly] TransformAccess transform)
            {
                var entity = Entities[index];
                
                var hasLocalTransform = LocalTransform.TryGetRefRW(entity, out var localTransformRW);
                var hasPostTransformMatrix = PostTransformMatrix.TryGetRefRW(entity, out var postTransformMatrixRW);

                if (!hasLocalTransform && !hasPostTransformMatrix && LocalToWorld.TryGetRefRW(entity, out var ltwRW))
                {
                    ltwRW.ValueRW.Value = float4x4.TRS(transform.position, transform.rotation, transform.localScale);
                }
                
                if (hasLocalTransform)
                {
                    localTransformRW.ValueRW.Position = transform.position;
                    localTransformRW.ValueRW.Rotation = transform.rotation;
                
                    localTransformRW.ValueRW.Scale = HybridEntityUtils.IsNonUniformScale(transform) 
                        ? 1f 
                        : transform.localScale.x;
                }

                if (hasPostTransformMatrix)
                {
                    postTransformMatrixRW.ValueRW = new PostTransformMatrix { Value = float4x4.Scale(transform.localScale) };
                }
            }
        }
    }
}