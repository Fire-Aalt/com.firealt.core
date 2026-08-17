using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace FireAlt.Core.Editor
{
    [InitializeOnLoad]
    public static class EditorBakingWorld
    {
        private static readonly Dictionary<World, BlobAssetStore> Stores = new();
        private static readonly List<World> StaleWorlds = new();

        static EditorBakingWorld()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static Entity[] BakeInto(GameObject[] gameObjects, World editorWorld)
        {
            PruneStores();
            if (!Stores.TryGetValue(editorWorld, out var blobAssetStore))
            {
                blobAssetStore = new BlobAssetStore(128);
                Stores.Add(editorWorld, blobAssetStore);
            }

            var bakingWorld = new World("Editor Baking World", WorldFlags.Conversion);
            try
            {
                var settings = new BakingSettings
                {
                    FilterFlags = WorldSystemFilterFlags.BakingSystem,
                    BakingFlags = BakingUtility.BakingFlags.AddEntityGUID |
                                  BakingUtility.BakingFlags.AssignName,
                    BlobAssetStore = blobAssetStore,
                };
                BakingUtility.BakeGameObjects(bakingWorld, gameObjects, settings);

                editorWorld.EntityManager.MoveEntitiesFrom(out var movedEntities, bakingWorld.EntityManager);
                var result = movedEntities.ToArray();
                movedEntities.Dispose();
                return result;
            }
            finally
            {
                bakingWorld.Dispose();
            }
        }

        private static void PruneStores()
        {
            StaleWorlds.Clear();
            foreach (var world in Stores.Keys)
            {
                if (!world.IsCreated) StaleWorlds.Add(world);
            }

            foreach (var world in StaleWorlds)
            {
                var store = Stores[world];
                if (store.IsCreated) store.Dispose();
                Stores.Remove(world);
            }

            StaleWorlds.Clear();
        }

        private static void Dispose()
        {
            foreach (var store in Stores.Values)
            {
                if (store.IsCreated) store.Dispose();
            }

            Stores.Clear();
            StaleWorlds.Clear();
        }
    }
}
