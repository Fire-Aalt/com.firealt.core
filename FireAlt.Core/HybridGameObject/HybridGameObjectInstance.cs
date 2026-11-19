using Unity.Entities;

namespace FireAlt.Core
{
    public struct HybridGameObjectInstance : ICleanupComponentData
    {
        public UnityObjectRef<HybridGameObject> Value;
        public UnityObjectRef<HybridGameObject> Source;
        public Hash128 Version;
        public int TransformId;
    }
}
