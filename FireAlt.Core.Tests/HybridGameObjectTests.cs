using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace FireAlt.Core.Tests
{
    public class HybridGameObjectTests
    {
        private World _world;
        private HybridGameObjectLifecycleSystem _lifecycle;
        private SystemHandle _sync;
        private HybridGameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _world = new World(nameof(HybridGameObjectTests));
            _lifecycle = _world.GetOrCreateSystemManaged<HybridGameObjectLifecycleSystem>();
            _sync = _world.GetOrCreateSystem<SyncHybridGameObjectTransformSystem>();
            _prefab = new GameObject("Prefab", typeof(HybridGameObject), typeof(AwakeEntityProbe))
                .GetComponent<HybridGameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }

            Object.DestroyImmediate(_prefab.gameObject);
        }

        [Test]
        public void Link_SetsEntityBeforeAwake_AndAppliesPose()
        {
            var entity = CreateEntity(float4x4.TRS(new float3(1f, 2f, 3f), quaternion.RotateY(1f), new float3(2f)));

            UpdateWorld();

            var link = GetLink(entity);
            Assert.That(link.Entity, Is.EqualTo(entity));
            Assert.That(link.World, Is.SameAs(_world));
            Assert.That(link.GetComponent<AwakeEntityProbe>().AwakeEntity, Is.EqualTo(entity));
            Assert.That(link.gameObject.hideFlags, Is.EqualTo(HybridGameObjectUtils.ReadOnlyFlags));
            Assert.That(link.gameObject.scene.isSubScene, Is.False);
            AssertApproximately(link.transform.position, new Vector3(1f, 2f, 3f));
            AssertApproximately(link.transform.localScale, new Vector3(2f, 2f, 2f));
            Assert.That(Quaternion.Angle(link.transform.rotation, quaternion.RotateY(1f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Sync_FollowsLocalToWorld()
        {
            var entity = CreateEntity(float4x4.identity);
            UpdateWorld();

            _world.EntityManager.SetComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(new float3(5f, 0f, -1f), quaternion.identity, new float3(1f, 3f, 1f)),
            });
            UpdateWorld();

            var link = GetLink(entity);
            AssertApproximately(link.transform.position, new Vector3(5f, 0f, -1f));
            AssertApproximately(link.transform.localScale, new Vector3(1f, 3f, 1f));
        }

        [Test]
        public void VersionChange_ReplacesInstance()
        {
            var entity = CreateEntity(float4x4.identity);
            UpdateWorld();
            var oldLink = GetLink(entity);

            var prefab = _world.EntityManager.GetComponentData<HybridGameObjectPrefab>(entity);
            prefab.Version = new Hash128(1, 2, 3, 4);
            _world.EntityManager.SetComponentData(entity, prefab);
            UpdateWorld();

            Assert.That(oldLink == null, Is.True);
            Assert.That(GetLink(entity), Is.Not.Null);
        }

        [Test]
        public void DestroyEntity_DestroysInstance()
        {
            var entity = CreateEntity(float4x4.identity);
            UpdateWorld();
            var link = GetLink(entity);

            _world.EntityManager.DestroyEntity(entity);
            UpdateWorld();

            Assert.That(link == null, Is.True);
            Assert.That(_world.EntityManager.Exists(entity), Is.False);
        }

        [Test]
        public void WorldDispose_DestroysInstances()
        {
            var entity = CreateEntity(float4x4.identity);
            UpdateWorld();
            var link = GetLink(entity);

            _world.Dispose();

            Assert.That(link == null, Is.True);
        }

        private Entity CreateEntity(float4x4 pose)
        {
            var entity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(entity, new LocalToWorld { Value = pose });
            _world.EntityManager.AddComponentData(entity, new HybridGameObjectPrefab { Prefab = _prefab });
            return entity;
        }

        private HybridGameObject GetLink(Entity entity)
        {
            return _world.EntityManager.GetComponentData<HybridGameObjectInstance>(entity).Value.Value;
        }

        private void UpdateWorld()
        {
            _lifecycle.Update();
            _sync.Update(_world.Unmanaged);
            _world.EntityManager.CompleteAllTrackedJobs();
        }

        private static void AssertApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} != {expected}");
        }
    }

    [ExecuteAlways]
    public sealed class AwakeEntityProbe : MonoBehaviour
    {
        public Entity AwakeEntity { get; private set; }

        private void Awake()
        {
            AwakeEntity = GetComponent<HybridGameObject>().Entity;
        }
    }
}
