using System;
using FireAlt.Core.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace FireAlt.Core.Rendering
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class RegisterRuntimeMaterialSystem : SystemBase
    {
        public struct Singleton : IComponentData, IDisposable
        {
            public NativeHashMap<MaterialLookup, BatchMaterial> Materials;
            public NativeParallelMultiHashMap<MaterialLookup, Entity> ToAssign;
            
            public void Dispose()
            {
                foreach (var kvp in Materials)
                {
                    CoreUtils.Destroy(kvp.Value.Material);
                }
                Materials.Dispose();
                ToAssign.Dispose();
            }
        }

        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(new Singleton
            {
                Materials = new NativeHashMap<MaterialLookup, BatchMaterial>(8, Allocator.Persistent),
                ToAssign = new NativeParallelMultiHashMap<MaterialLookup, Entity>(8, Allocator.Persistent)
            });
        }
        
        protected override void OnDestroy()
        {
            SystemAPI.GetSingleton<Singleton>().Dispose();
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<Singleton>();
            var singleton = SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            
            var newLookups = singleton.ToAssign.GetKeyArray(WorldUpdateAllocator);
            
            foreach (var lookup in newLookups)
            {
                RegisterLookup(lookup, singleton.Materials, entitiesGraphicsSystem);
            }
            
            Dependency = new AssignNewMaterialsJob
            {
                NewLookups = newLookups,
                ToAssign = singleton.ToAssign,
                Materials = singleton.Materials,
                MaterialMeshInfo = SystemAPI.GetComponentLookup<MaterialMeshInfo>(),
                RuntimeMaterial = SystemAPI.GetComponentLookup<RuntimeMaterial>(),
            }.Schedule(newLookups.Length, 1, Dependency);

            Dependency = singleton.ToAssign.Clear(Dependency);
        }
        
        [BurstCompile]
        private struct AssignNewMaterialsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MaterialLookup> NewLookups;
            [ReadOnly] public NativeParallelMultiHashMap<MaterialLookup, Entity> ToAssign;
            [ReadOnly] public NativeHashMap<MaterialLookup, BatchMaterial> Materials;
            
            [NativeDisableParallelForRestriction] public ComponentLookup<RuntimeMaterial> RuntimeMaterial;
            [NativeDisableParallelForRestriction] public ComponentLookup<MaterialMeshInfo> MaterialMeshInfo;
            
            public void Execute(int index)
            {
                var newLookup = NewLookups[index];
                
                foreach (var entity in ToAssign.GetValuesForKey(newLookup))
                {
                    var batchMaterial = Materials[newLookup];
                    batchMaterial.AssignToTargets(entity, RuntimeMaterial, MaterialMeshInfo);
                }
            }
        }
        
        private void RegisterLookup(MaterialLookup lookup, NativeHashMap<MaterialLookup, BatchMaterial> materials,
            EntitiesGraphicsSystem entitiesGraphicsSystem)
        {
            if (!materials.TryGetValue(lookup, out var batchMaterial))
            {
                batchMaterial = CreateAndRegister(lookup, entitiesGraphicsSystem, 0);
                materials[lookup] = batchMaterial;
            }
        }

        internal static BatchMaterial CreateAndRegister(MaterialLookup lookup, EntitiesGraphicsSystem entitiesGraphicsSystem, 
            int srcMaterialVersion)
        {
            var mat = SpriteMaterialUtility.CloneFromLookup(lookup);
            var matId = entitiesGraphicsSystem.RegisterMaterial(mat);
            
            return new BatchMaterial(mat, matId, srcMaterialVersion);
        }
    }
}