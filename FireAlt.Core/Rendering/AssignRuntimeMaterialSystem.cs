using FireAlt.Core.Collections;
using FireAlt.Core.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireAlt.Core.Rendering
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(RuntimeBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct AssignRuntimeMaterialSystem : ISystem 
    {
        private struct MaterialRequest
        {
            public MaterialLookup MaterialLookup;
            public Entity Entity;
        }
        
        private NativeThreadList<MaterialRequest> _requests;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _requests = new NativeThreadList<MaterialRequest>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _requests.Dispose();
        }

#if !UNITY_EDITOR
        [BurstCompile]
#endif
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRW<RegisterRuntimeMaterialSystem.Singleton>();
            var singleton = SystemAPI.GetSingletonRW<RegisterRuntimeMaterialSystem.Singleton>().ValueRW;
            
#if UNITY_EDITOR
            var entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            
            if (!Application.isPlaying)
            {
                var arrays = singleton.Materials.GetKeyValueArrays(Allocator.Temp);
                for (int i = 0; i < arrays.Length; i++)
                {
                    var lookup = arrays.Keys[i];
                    var batchMaterial = arrays.Values[i];

                    var srcMaterialVersion = UnityEditor.EditorUtility.GetDirtyCount(lookup.SrcMaterial.Value);

                    if (srcMaterialVersion != batchMaterial.SrcMaterialVersion || batchMaterial.Material.Value == null)
                    {
                        entitiesGraphicsSystem.UnregisterMaterial(batchMaterial.MaterialID);
                        CoreUtils.Destroy(batchMaterial.Material);
                    
                        batchMaterial = RegisterRuntimeMaterialSystem.CreateAndRegister(lookup, entitiesGraphicsSystem, srcMaterialVersion);
                        singleton.Materials[lookup] = batchMaterial;
                    }
                }
            }
#endif
            
            state.Dependency = new AssignJob
            {
                MaterialMeshInfo = SystemAPI.GetComponentLookup<MaterialMeshInfo>(),
                RuntimeMaterial = SystemAPI.GetComponentLookup<RuntimeMaterial>(),
                Materials = singleton.Materials,
                Requests = _requests.AsThreadWriter()
            }.ScheduleParallel(state.Dependency);
            
            state.Dependency = new ProcessRequestsJob
            {
                Requests = _requests,
                ToAssign = singleton.ToAssign,
            }.Schedule(state.Dependency);
        }
        
        [BurstCompile]
        [WithAny(typeof(MaterialMeshInfo), typeof(RuntimeMaterial))]
        [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)]
        private partial struct AssignJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<MaterialLookup, BatchMaterial> Materials;
            
            [NativeDisableParallelForRestriction] public ComponentLookup<RuntimeMaterial> RuntimeMaterial;
            [NativeDisableParallelForRestriction] public ComponentLookup<MaterialMeshInfo> MaterialMeshInfo;

            public NativeThreadList<MaterialRequest>.ThreadWriter Requests;
            
            private void Execute(ref RuntimeMaterialLookup lookup, EnabledRefRW<RuntimeMaterialLookup> enabledRefRW, Entity entity)
            {
                if (Materials.TryGetValue(lookup.Value, out var batchMaterial))
                {
                    batchMaterial.AssignToTargets(entity, RuntimeMaterial, MaterialMeshInfo);
                }
                else
                {
                    Requests.Add(new MaterialRequest { MaterialLookup = lookup.Value, Entity = entity });
                }
                
                enabledRefRW.ValueRW = false;
            }
        }

        [BurstCompile]
        private struct ProcessRequestsJob : IJob
        {
            public NativeThreadList<MaterialRequest> Requests;
            public NativeParallelMultiHashMap<MaterialLookup, Entity> ToAssign;
            
            public void Execute()
            {
                foreach (var request in Requests)
                {
                    ToAssign.Add(request.MaterialLookup, request.Entity);
                }
                Requests.Clear();
            }
        }
    }
}