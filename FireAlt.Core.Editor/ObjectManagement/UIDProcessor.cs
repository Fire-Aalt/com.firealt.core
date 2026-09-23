// <copyright file="UIDProcessor.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using FireAlt.Core.ObjectManagement;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FireAlt.Core.Editor
{
    /// <summary> An <see cref="AssetPostprocessor" /> that ensures <see cref="IUID" /> types always have a unique ID even if 2 branches merge. </summary>
    [NoAutoStaticsCleanup]
    public class UIDProcessor : AssetPostprocessor
    {
        private static readonly HashSet<string> AlreadyProcessedAssets = new();
        private static readonly Dictionary<Type, Processor> Processors = new();
        private static readonly HashSet<string> Delayed = new();

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            foreach (var assetPath in deletedAssets)
            {
                AlreadyProcessedAssets.Remove(assetPath);
            }

            var runDelayed = false;

            foreach (var assetPath in importedAssets)
            {
                if (!assetPath.EndsWith(".asset"))
                {
                    continue;
                }

                if (!AlreadyProcessedAssets.Add(assetPath))
                {
                    continue;
                }

                Delayed.Add(assetPath);
                runDelayed = true;
            }

            if (runDelayed)
            {
                EditorApplication.delayCall -= DelayedExecution;
                EditorApplication.delayCall += DelayedExecution;
            }
        }

        private static void DelayedExecution()
        {
            EditorApplication.delayCall -= DelayedExecution;

            foreach (var processor in Processors)
            {
                processor.Value.Reset();
            }

            foreach (var assetPath in Delayed)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (!asset)
                {
                    continue;
                }

                ProcessAsset(asset);

                foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    var scriptableObject = subAsset as ScriptableObject;

                    if (!scriptableObject)
                    {
                        continue;
                    }

                    ProcessAsset(scriptableObject);
                }
            }

            Delayed.Clear();
        }

        private static void ProcessAsset(Object asset)
        {
            if (CheckAutoID(asset))
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static bool CheckAutoID(Object asset)
        {
            if (asset is not IUID)
            {
                return false;
            }

            var current = asset.GetType();

            while (true)
            {
                var baseType = current.BaseType;

                if (baseType == null || !typeof(IUID).IsAssignableFrom(baseType))
                {
                    if (!Processors.TryGetValue(current, out var processor))
                    {
                        processor = Processors[current] = new Processor(current);
                    }

                    return processor.Process(asset);
                }

                current = baseType;
            }
        }

        private static int GetFirstFreeID(IReadOnlyDictionary<int, int> map)
        {
            for (var i = 1; i < int.MaxValue; i++)
            {
                if (!map.ContainsKey(i))
                {
                    return i;
                }
            }

            return 0;
        }

        private class Processor
        {
            private readonly string _filter;
            private readonly Dictionary<int, int> _map = new();
            private readonly Type _type;

            private bool _isInitialized;

            public Processor(Type type)
            {
                _type = type;
                _filter = $"t:{type.Name}";
            }

            public void Reset()
            {
                _isInitialized = false;
                _map.Clear();
            }

            public bool Process(Object obj)
            {
                var asset = (IUID)obj;

                if (!_isInitialized)
                {
                    _isInitialized = true;
                    BuildMap();
                }

                _map.TryGetValue(asset.ID, out var count);

                if (asset.ID == 0 || count > 1)
                {
                    var newId = GetFirstFreeID(_map);
                    _map[asset.ID] = count - 1;
                    asset.ID = newId;
                    _map[newId] = 1;

                    EditorUtility.SetDirty(obj);
                    return true;
                }

                return false;
            }

            private void BuildMap()
            {
                var paths = AssetDatabase.FindAssets(_filter).Select(AssetDatabase.GUIDToAssetPath).Distinct();

                foreach (var path in paths)
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);

                    foreach (var asset in assets)
                    {
                        if (!asset)
                        {
                            continue;
                        }

                        if (!_type.IsInstanceOfType(asset))
                        {
                            continue;
                        }

                        var uid = (IUID)asset;
                        _map.TryGetValue(uid.ID, out var count);
                        count++;
                        _map[uid.ID] = count;
                    }
                }
            }
        }
    }
}
