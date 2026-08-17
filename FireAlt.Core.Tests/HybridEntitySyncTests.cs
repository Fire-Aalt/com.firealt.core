using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FireAlt.Core.Tests
{
    public class HybridEntitySyncTests
    {
        [Test]
        public void CleanupOfOldEntity_PreservesLiveTransformHandle()
        {
            using var world = new World("HybridEntitySyncTests");
            var managedSystem = world.GetOrCreateSystemManaged<SyncHybridEntityManagedSystem>();
            var syncSystem = world.GetOrCreateSystem<SyncHybridEntitySystem>();
            var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(managedSystem);
            group.AddSystemToUpdateList(syncSystem);
            group.SortSystems();

            var staleObject = new GameObject("Stale", typeof(HybridEntitySyncTestBehaviour));
            var liveObject = new GameObject("Live", typeof(HybridEntitySyncTestBehaviour));
            try
            {
                staleObject.transform.position = new Vector3(1f, 0f, 0f);
                liveObject.transform.position = new Vector3(2f, 0f, 0f);

                var staleEntity = CreateEntity(world.EntityManager,
                    staleObject.GetComponent<HybridEntitySyncTestBehaviour>());
                var liveEntity = CreateEntity(world.EntityManager,
                    liveObject.GetComponent<HybridEntitySyncTestBehaviour>());

                group.Update();
                world.EntityManager.CompleteAllTrackedJobs();
                Assert.That(world.EntityManager.GetComponentData<LocalToWorld>(liveEntity).Position.x,
                    Is.EqualTo(2f));

                liveObject.transform.position = new Vector3(7f, 0f, 0f);
                world.EntityManager.SetEnabled(staleEntity, false);
                world.EntityManager.DestroyEntity(staleEntity);
                group.Update();
                world.EntityManager.CompleteAllTrackedJobs();

                Assert.That(world.EntityManager.GetComponentData<LocalToWorld>(liveEntity).Position.x,
                    Is.EqualTo(7f));
                Assert.That(world.EntityManager.HasComponent<SyncTransformToEntity>(liveEntity), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(staleObject);
                Object.DestroyImmediate(liveObject);
            }
        }

        private static Entity CreateEntity(EntityManager entityManager, MonoBehaviour behaviour)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            entityManager.AddComponentData(entity, new HybridEntitySync(behaviour));
            return entity;
        }
    }

    public sealed class HybridEntitySyncTestBehaviour : MonoBehaviour
    {
    }
}
