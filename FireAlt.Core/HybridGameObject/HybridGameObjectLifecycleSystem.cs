using FireAlt.Core.Collections;
using FireAlt.Core.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireAlt.Core
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(InstantiationSystemGroup))]
    public partial class HybridGameObjectLifecycleSystem : SystemBase
    {
        private EntityQuery _orphanQuery;
        private EntityQuery _newQuery;
        private EntityQuery _changedQuery;
        private Transform _staging;

        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(new HybridGameObjectTransforms(8, Allocator.Persistent));

            _orphanQuery = SystemAPI.QueryBuilder()
                .WithAll<HybridGameObjectInstance>()
                .WithNone<HybridGameObjectPrefab>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build();
            _newQuery = SystemAPI.QueryBuilder()
                .WithAll<HybridGameObjectPrefab, LocalToWorld>()
                .WithNone<HybridGameObjectInstance>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build();
            _changedQuery = SystemAPI.QueryBuilder()
                .WithAll<HybridGameObjectPrefab, HybridGameObjectInstance, LocalToWorld>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build();
            _changedQuery.SetChangedVersionFilter(ComponentType.ReadOnly<HybridGameObjectPrefab>());

            RequireAnyForUpdate(_orphanQuery, _newQuery, _changedQuery);
        }

        protected override void OnDestroy()
        {
            EntityManager.CompleteAllTrackedJobs();

            var instances = SystemAPI.QueryBuilder()
                .WithAll<HybridGameObjectInstance>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build()
                .ToComponentDataArray<HybridGameObjectInstance>(Allocator.Temp);
            foreach (var instance in instances)
            {
                DestroyInstance(instance);
            }

            SystemAPI.GetSingleton<HybridGameObjectTransforms>().Dispose();

            if (_staging != null)
            {
                HybridGameObjectUtils.Destroy(_staging.gameObject);
            }
        }

        protected override void OnUpdate()
        {
            var transforms = SystemAPI.GetSingletonRW<HybridGameObjectTransforms>().ValueRO.Value;

            UnlinkOrphans(transforms);
            LinkNew(transforms);
            RelinkChanged(transforms);
        }

        private void UnlinkOrphans(ReusableTransformAccessArray<Entity> transforms)
        {
            if (_orphanQuery.IsEmpty)
            {
                return;
            }

            foreach (var instance in _orphanQuery.ToComponentDataArray<HybridGameObjectInstance>(Allocator.Temp))
            {
                Unlink(instance, transforms);
            }

            EntityManager.RemoveComponent<HybridGameObjectInstance>(_orphanQuery);
        }

        private void LinkNew(ReusableTransformAccessArray<Entity> transforms)
        {
            if (_newQuery.IsEmpty)
            {
                return;
            }

            foreach (var entity in _newQuery.ToEntityArray(Allocator.Temp))
            {
                var instance = Link(entity, transforms);
                EntityManager.AddComponentData(entity, instance);
            }
        }

        private void RelinkChanged(ReusableTransformAccessArray<Entity> transforms)
        {
            if (_changedQuery.IsEmpty)
            {
                return;
            }

            foreach (var entity in _changedQuery.ToEntityArray(Allocator.Temp))
            {
                var prefab = EntityManager.GetComponentData<HybridGameObjectPrefab>(entity);
                var instance = EntityManager.GetComponentData<HybridGameObjectInstance>(entity);
                if (instance.Source == prefab.Prefab && instance.Version == prefab.Version)
                {
                    continue;
                }

                Unlink(instance, transforms);
                EntityManager.SetComponentData(entity, Link(entity, transforms));
            }
        }

        private HybridGameObjectInstance Link(Entity entity, ReusableTransformAccessArray<Entity> transforms)
        {
            var prefab = EntityManager.GetComponentData<HybridGameObjectPrefab>(entity);
            var link = Instantiate(prefab.Prefab.Value, entity);
            LinkToAuthoring(link, entity);
            Place(link, EntityManager.GetComponentData<LocalToWorld>(entity).Value);

            return new HybridGameObjectInstance
            {
                Value = link,
                Source = prefab.Prefab,
                Version = prefab.Version,
                TransformId = transforms.AddTransformHandle(link.transformHandle, entity),
            };
        }

        // Instantiating under an inactive root defers Awake/OnEnable until Entity, World and placement are set.
        private HybridGameObject Instantiate(HybridGameObject prefab, Entity entity)
        {
            var link = Object.Instantiate(prefab, GetStaging());
            link.Entity = entity;
            link.World = World;
            return link;
        }

        private void LinkToAuthoring(HybridGameObject link, Entity entity)
        {
#if UNITY_EDITOR
            var authoring = EntityManager.Debug.GetAuthoringObjectForEntity(entity) as GameObject;
            if (authoring == null || UnityEditor.EditorUtility.IsPersistent(authoring) || !authoring.TryGetComponent<HybridEntity>(out var host))
            {
                return;
            }

            link.name = $"{link.name.Replace("(Clone)", string.Empty)} ({authoring.name})";
            host.EditorLink = link;
#endif
        }

        // Edit-time links are read-only and unsaved, and live outside subscenes so they are never baked.
        private static void Place(HybridGameObject link, in float4x4 pose)
        {
            var editing = !Application.isPlaying;
            if (editing)
            {
                HybridGameObjectUtils.SetHideFlags(link.gameObject, HybridGameObjectUtils.ReadOnlyFlags);
            }

            HybridGameObjectUtils.ApplyPose(link.transform, pose);
            link.transform.SetParent(null, true);
            if (editing)
            {
                SceneManager.MoveGameObjectToScene(link.gameObject, FindUnbakedScene());
            }
        }

        private static Scene FindUnbakedScene()
        {
            var scene = SceneManager.GetActiveScene();
            for (var i = 0; scene.isSubScene && i < SceneManager.sceneCount; i++)
            {
                scene = SceneManager.GetSceneAt(i);
            }

            return scene;
        }

        private Transform GetStaging()
        {
            if (_staging != null)
            {
                return _staging;
            }

            var staging = new GameObject($"{World.Name} HybridGameObject Staging") { hideFlags = HideFlags.HideAndDontSave };
            staging.SetActive(false);
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(staging);
            }

            _staging = staging.transform;
            return _staging;
        }

        private static void Unlink(in HybridGameObjectInstance instance, ReusableTransformAccessArray<Entity> transforms)
        {
            transforms.ReleaseTransform(instance.TransformId);
            DestroyInstance(instance);
        }

        private static void DestroyInstance(in HybridGameObjectInstance instance)
        {
            var link = instance.Value.Value;
            if (link != null)
            {
                HybridGameObjectUtils.Destroy(link.gameObject);
            }
        }
    }
}
