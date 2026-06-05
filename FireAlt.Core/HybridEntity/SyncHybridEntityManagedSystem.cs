using FireAlt.Core.Groups;
using FireAlt.Core.Utility;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Profiling;

namespace FireAlt.Core
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(BeforeTransformSystemGroup))]
    public partial class SyncHybridEntityManagedSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(new SyncTransformToEntityContainer(8, Allocator.Persistent));
        }

        protected override void OnDestroy()
        {
            SystemAPI.GetSingleton<SyncTransformToEntityContainer>().Dispose();
        }
        
        protected override void OnUpdate()
        {
            var singleton = SystemAPI.GetSingletonRW<SyncTransformToEntityContainer>().ValueRW;
            
            Profiler.BeginSample("Initialize New Entities");
            var initializeQuery = SystemAPI.QueryBuilder().WithAll<HybridEntitySync>()
                .WithAbsent<SyncTransformToEntity>().Build();
            if (!BurstUtils.IsEmpty(ref initializeQuery))
            {
                var initEcb = new EntityCommandBuffer(Allocator.Temp);
                
                foreach (var (link, self) in SystemAPI.Query<RefRO<HybridEntitySync>>()
                             .WithEntityAccess()
                             .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
                {
                    initEcb.AddComponent(self, new SyncTransformToEntity
                    {
                        TransformId = singleton.ReusableTransformAccessArray.AddTransformHandle(link.ValueRO.MonoBehaviour.Value.transformHandle, self)
                    });
                }
                
                initEcb.Playback(EntityManager);
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("SetEnabled");
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (link, self) in SystemAPI.Query<RefRO<HybridEntitySync>>()
                         .WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                var mb = link.ValueRO.MonoBehaviour;

                var enabled = HybridEntityUtils.IsEntityEnabled(mb);
                if (enabled != EntityManager.IsEnabled(self))
                {
                    ecb.SetEnabled(self, enabled);
                }
            }

            if (!ecb.IsEmpty)
            {
                ecb.Playback(EntityManager);
            }
            Profiler.EndSample();
        }
    }
}