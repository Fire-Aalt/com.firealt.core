using System.Reflection;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;

namespace FireAlt.Core.Editor
{
    // Shows each linked GameObject's real Hierarchy row under its HybridEntity host. The GameObject handler
    // rebuilds rows from Transforms on every update, so the rows are moved back after each one.
    // Registration goes through HierarchyWindow's internal RegisterNodeTypeHandler, as Entities does.
    internal sealed class HybridGameObjectHierarchyHandler : HierarchyNodeTypeHandler
    {
        [InitializeOnLoadMethod]
        private static void RegisterOnLoad()
        {
            EditorApplication.delayCall += Register;
        }

        private static void Register()
        {
            typeof(HierarchyWindow)
                .GetMethod("RegisterNodeTypeHandler", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(HybridGameObjectHierarchyHandler))
                .Invoke(null, null);
        }

        public override string GetNodeTypeName() => "HybridGameObjectLink";

        protected override void UpdateEnd()
        {
            var gameObjects = Hierarchy.GetNodeTypeHandlerBase<HierarchyGameObjectHandler>();
            foreach (var host in Object.FindObjectsByType<HybridEntity>(FindObjectsInactive.Include))
            {
                if (host.EditorLink != null)
                {
                    ShowUnder(gameObjects, host.EditorLink.gameObject, host.gameObject);
                }
            }
        }

        private void ShowUnder(HierarchyGameObjectHandler gameObjects, GameObject child, GameObject parent)
        {
            var childNode = gameObjects.GetNodeFromEntityId(child.GetEntityId());
            var parentNode = gameObjects.GetNodeFromEntityId(parent.GetEntityId());
            if (Hierarchy.Exists(childNode) && Hierarchy.Exists(parentNode) && Hierarchy.GetParent(childNode) != parentNode)
            {
                Hierarchy.SetParent(childNode, parentNode);
            }
        }
    }
}
