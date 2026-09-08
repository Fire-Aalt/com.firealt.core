using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireAlt.Core.Rendering
{
    public readonly struct BatchMaterial
    {
        public readonly UnityObjectRef<Material> Material;
        public readonly BatchMaterialID MaterialID;

#if UNITY_EDITOR
        public readonly int SrcMaterialVersion;
#endif
        
        public BatchMaterial(Material material, BatchMaterialID materialID, int srcMaterialDirtyCount)
        {
            Material = material;
            MaterialID = materialID;
#if UNITY_EDITOR
            SrcMaterialVersion = srcMaterialDirtyCount;
#endif
        }
        
        public void AssignToTargets(Entity entity, ComponentLookup<RuntimeMaterial> runtimeMaterialLookup, 
            ComponentLookup<MaterialMeshInfo> materialMeshInfoLookup)
        {
            if (runtimeMaterialLookup.TryGetRefRW(entity, out var runtimeMaterial))
            {
                runtimeMaterial.ValueRW.Value = Material;
            }
            if (materialMeshInfoLookup.TryGetRefRW(entity, out var materialMeshInfo))
            {
                materialMeshInfo.ValueRW.MaterialID = MaterialID;
            }
        }
    }
}