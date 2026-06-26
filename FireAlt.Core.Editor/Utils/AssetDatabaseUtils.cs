using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FireAlt.Core.Editor
{
    public static class AssetDatabaseUtils
    {
        public static string ToGuid(string path)
        {
            return AssetDatabase.GUIDFromAssetPath(path).ToString();
        }
        
        public static string ToGuid(Object asset)
        {
            return ToGuid(AssetDatabase.GetAssetPath(asset));
        }
        
        public static Unity.Entities.Hash128 ToGuidHash(Object asset)
        {
            return AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(asset));
        }

        public static void EnsureValidFolder(string assetPath)
        {
            var folderPath = GetFolderPath(assetPath);
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
        
        public static T GetOrCreateAsset<T>(string assetPath, bool recreateIfExists, Func<T> createNew) where T : Object
        {
            var loadedAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (loadedAsset != null && recreateIfExists)
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();

                loadedAsset = null;
            }

            if (loadedAsset == null)
            {
                loadedAsset = createNew();
                Debug.Log($"Created {loadedAsset} asset at '{assetPath}'");
            }
                
            return loadedAsset;
        }
        
        public static T CreateNewScriptableObjectAsset<T>(string defaultName, ScriptableObject caller) where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            var assetPath = AssetDatabase.GetAssetPath(caller);
            var folderPath = GetFolderPath(assetPath);
            
            var i = 0;
            var newAssetPath = folderPath + $"/{defaultName}.asset";
            while (AssetDatabase.AssetPathExists(newAssetPath))
            {
                var iStr = '_' + i.ToString();
                if (i == 0) iStr = "";
                
                newAssetPath = newAssetPath.Remove(newAssetPath.Length - 6 - iStr.Length, 6 + iStr.Length);
                newAssetPath += $"_{i}.asset";
                i++;
            }
            SaveAssetToDatabase(instance, newAssetPath);
            EditorUtility.SetDirty(caller);
            EditorUtility.SetDirty(instance);
            return instance;
        }
        
        public static GameObject GetOrCreatePrefabVariant(GameObject sourcePrefab, string targetAssetPath, bool recreate, Action<GameObject> modify = null)
        {
            if (sourcePrefab == null) throw new ArgumentNullException(nameof(sourcePrefab));
            if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentNullException(nameof(targetAssetPath));

            if (!targetAssetPath.EndsWith(".prefab")) targetAssetPath += ".prefab";
            EnsureValidFolder(targetAssetPath);
            
            var prefab = GetOrCreateAsset(targetAssetPath, recreate, () =>
            {
                // Instantiate a prefab instance (keeps link to source prefab)
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);

                // Save the instance as a new Prefab Asset. Because the input is a prefab instance root,
                // Unity will create a Prefab Variant that keeps the link to the original prefab.
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, targetAssetPath);
                if (saved == null)
                    Debug.LogError($"Failed to save prefab variant to '{targetAssetPath}'.");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Object.DestroyImmediate(instance);
                return saved;
            });
            
            modify?.Invoke(prefab);
            SaveAssetToDatabase(prefab, targetAssetPath);
            
            return prefab;
        }

        public static string GetFolderPath(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
            var folderPath = Directory.GetParent(path).FullName;
            var root = Application.dataPath.Length - "Assets".Length;
            return folderPath[root..];
        }

        public static void SaveAssetToDatabase(Object asset, string assetPath)
        {
            EnsureValidFolder(assetPath);

            if (AssetDatabase.Contains(asset))
            {
                EditorUtility.SetDirty(asset);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Searches in "Editor Resources"
        /// </summary>
        public static T LoadEditorResource<T>(string relativeFilePath, string rootFolderValidationName) where T : Object
        {
            const string editorResources = "Editor Default Resources";
            
            var guids = AssetDatabase.FindAssets("\"" + editorResources + "\"");
            var objectList = new List<Object>(guids.Length);
            
            foreach (var guid in guids)
            {
                var editorResourcesFolderPath = AssetDatabase.GUIDToAssetPath(guid);
                var rootDirectoryName = Directory.GetParent(editorResourcesFolderPath)?.Name;
                
                if (rootDirectoryName != rootFolderValidationName)
                {
                    continue;
                }

                var assetPath = Path.Combine(editorResourcesFolderPath, relativeFilePath);

                if (AssetDatabase.AssetPathExists(assetPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                    objectList.Add(asset);
                }
            }

            if (objectList.Count == 1)
            {
                var obj = objectList[0] as T;
                return obj;
            }

            if (objectList.Count > 1)
            {
                throw new Exception($"Found {objectList.Count} objects with relative path: {rootFolderValidationName}/Editor Default Resources/{relativeFilePath}");
            }
            throw new Exception($"Not found anything at path: {rootFolderValidationName}/Editor Default Resources/{relativeFilePath}");
        }
        
        public static Object[] LoadAllAssetsAtPath(string path)
        {
            if (path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }
            var guids = AssetDatabase.FindAssets("", new[] {path});
            var objectList = new Object[guids.Length];
            
            for (int index = 0; index < guids.Length; index++)
            {
                var guid = guids[index];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object));
                objectList[index] = asset;
            }

            return objectList;
        }
        
        public static T[] LoadAllAssetsOfType<T>(string optionalPath = "") where T : Object
        {
            string[] guids;
            if(optionalPath != "")
            {
                if(optionalPath.EndsWith("/"))
                {
                    optionalPath = optionalPath.TrimEnd('/');
                }
                guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}",new[] { optionalPath });
            }
            else
            {
                guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            }
            var objectList = new T[guids.Length];

            for (int index = 0; index < guids.Length; index++)
            {
                var guid = guids[index];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T)) as T;
                objectList[index] = asset;
            }

            return objectList;
        }
        
        public static string[] GetAllAssetPathsAtPath(string path)
        {
            if(path.EndsWith("/"))
            {
                path = path.TrimEnd('/');
            }
            
            var guids = AssetDatabase.FindAssets("", new string[] {path});
            var pathList = new string[guids.Length];
            
            for (int index = 0; index < guids.Length; index++)
            {
                pathList[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            return pathList;
        }

        public static List<T> LoadAllPrefabsWithComponent<T>() where T : Component
        {
            var prefabs = LoadAllAssetsOfType<GameObject>();
            
            var list = new List<T>(64);
            foreach (var prefab in prefabs)
            {
                if (prefab.TryGetComponent(out T component))
                {
                    list.Add(component);
                }
            }
            return list;
        }
    }
}