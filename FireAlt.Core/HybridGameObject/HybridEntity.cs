using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor;
using Hash128 = Unity.Entities.Hash128;
#endif

namespace FireAlt.Core
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class HybridEntity : MonoBehaviour
    {
        public HybridGameObject prefab;

#if UNITY_EDITOR
        [System.NonSerialized] public HybridGameObject EditorLink;

        private HybridGameObject _preview;
        private HybridGameObject _previewSource;
        private Hash128 _previewVersion;

        private static Hash128 GetVersion(HybridGameObject prefab)
        {
            return AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab));
        }

        private void Update()
        {
            if (!HybridEntityUtils.InPrefabStage(this))
            {
                return;
            }

            var version = prefab != null ? GetVersion(prefab) : default;
            if (_previewSource != prefab || _previewVersion != version)
            {
                DestroyPreview();
                CreatePreview(version);
            }
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void CreatePreview(Hash128 version)
        {
            _previewSource = prefab;
            _previewVersion = version;
            if (prefab == null)
            {
                return;
            }

            _preview = Instantiate(prefab, transform, false);
            _preview.name = $"{prefab.name} ({name})";
            HybridGameObjectUtils.SetHideFlags(_preview.gameObject, HybridGameObjectUtils.ReadOnlyFlags);
        }

        private void DestroyPreview()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview.gameObject);
            }

            _preview = null;
            _previewSource = null;
            _previewVersion = default;
        }

        private sealed class HybridEntityBaker : Baker<HybridEntity>
        {
            public override void Bake(HybridEntity authoring)
            {
                DependsOn(authoring.prefab);
                if (authoring.prefab == null)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new HybridGameObjectPrefab
                {
                    Prefab = authoring.prefab,
                    Version = GetVersion(authoring.prefab),
                });
            }
        }
#endif
    }
}
