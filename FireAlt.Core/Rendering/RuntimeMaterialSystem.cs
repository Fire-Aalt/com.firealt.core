using System.Collections.Generic;
using FireAlt.Core.Groups;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireAlt.Core.Rendering
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(RuntimeBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class RuntimeMaterialSystem : SystemBase 
    {
        private readonly struct BatchMaterial
        {
            public readonly Material Material;
            public readonly BatchMaterialID MaterialID;
            public readonly int SrcMaterialVersion;
            
            public BatchMaterial(Material material, BatchMaterialID materialID, int srcMaterialDirtyCount)
            {
                Material = material;
                MaterialID = materialID;
                SrcMaterialVersion = srcMaterialDirtyCount;
            }
        }
        
        private readonly Dictionary<MaterialLookup, BatchMaterial> _materials = new();
        
        protected override void OnUpdate()
        {
            var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            
            foreach (var (materialMeshInfoRW, lookupRO, enabled) in SystemAPI.Query<RefRW<MaterialMeshInfo>, RefRO<RuntimeMaterialLookup>, EnabledRefRW<RuntimeMaterialLookup>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab))
            {
                var batchMaterial = GetBatchMaterial(lookupRO.ValueRO.Value, entitiesGraphicsSystem);
                
                materialMeshInfoRW.ValueRW.MaterialID = batchMaterial.MaterialID;
                enabled.ValueRW = false;
            }
            
            foreach (var (materialRW, lookupRO, enabled) in SystemAPI.Query<RefRW<RuntimeMaterial>, RefRO<RuntimeMaterialLookup>, EnabledRefRW<RuntimeMaterialLookup>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab))
            {
                var batchMaterial = GetBatchMaterial(lookupRO.ValueRO.Value, entitiesGraphicsSystem);

                materialRW.ValueRW.Value = batchMaterial.Material;
                enabled.ValueRW = false;
            }
        }

        // Ideally this method should be called only once for every RuntimeMaterialLookup rebake or reenable,
        // but due to a bug in live subscene baking, this method has to be called each frame on each entity in the Edit mode
        private BatchMaterial GetBatchMaterial(MaterialLookup lookup, EntitiesGraphicsSystem entitiesGraphicsSystem)
        {
            var srcMaterialVersion = UnityEditor.EditorUtility.GetDirtyCount(lookup.SrcMaterial.Value);
            
            if (!_materials.TryGetValue(lookup, out var batchMaterial))
            {
                batchMaterial = CreateAndRegister(lookup, srcMaterialVersion, entitiesGraphicsSystem);
                _materials.Add(lookup, batchMaterial);
            }
#if UNITY_EDITOR
            else if (batchMaterial.Material == null)
            {
                batchMaterial = CreateAndRegister(lookup, srcMaterialVersion, entitiesGraphicsSystem);
                _materials[lookup] = batchMaterial;
            }
#endif

            if (srcMaterialVersion != batchMaterial.SrcMaterialVersion)
            {
                entitiesGraphicsSystem.UnregisterMaterial(batchMaterial.MaterialID);
                CoreUtils.Destroy(batchMaterial.Material);
                
                batchMaterial = CreateAndRegister(lookup, srcMaterialVersion, entitiesGraphicsSystem);
                _materials[lookup] = batchMaterial;
            }

            return batchMaterial;
        }

        private static BatchMaterial CreateAndRegister(MaterialLookup lookup, int srcMaterialVersion,
            EntitiesGraphicsSystem entitiesGraphicsSystem)
        {
            var mat = SpriteMaterialUtility.CloneFromLookup(lookup);
            var matId = entitiesGraphicsSystem.RegisterMaterial(mat);
            
            return new BatchMaterial(mat, matId, srcMaterialVersion);
        }

        protected override void OnDestroy()
        {
            foreach (var kvp in _materials)
            {
                CoreUtils.Destroy(kvp.Value.Material);
            }
        }
    }
}