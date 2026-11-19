using Unity.Entities;

namespace FireAlt.Core
{
    public struct HybridGameObjectPrefab : IComponentData
    {
        public UnityObjectRef<HybridGameObject> Prefab;
        public Hash128 Version;
    }
}
