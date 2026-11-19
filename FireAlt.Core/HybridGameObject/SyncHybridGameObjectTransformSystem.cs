using FireAlt.Core.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace FireAlt.Core
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(AfterTransformSystemGroup))]
    public partial struct SyncHybridGameObjectTransformSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HybridGameObjectInstance>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var transforms = SystemAPI.GetSingleton<HybridGameObjectTransforms>().Value;
            state.Dependency = new SyncTransformsJob
            {
                Entities = transforms.AlignedData.AsArray(),
                LocalToWorld = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            }.Schedule(transforms.Array, state.Dependency);
        }

        [BurstCompile]
        private struct SyncTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorld;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid || !LocalToWorld.TryGetComponent(Entities[index], out var localToWorld))
                {
                    return;
                }

                HybridGameObjectUtils.Decompose(localToWorld.Value, out var position, out var rotation, out var scale);
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = scale;
            }
        }
    }
}
