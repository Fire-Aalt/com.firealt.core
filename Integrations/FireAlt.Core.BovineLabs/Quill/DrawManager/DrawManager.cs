#if BL_QUILL && UNITY_EDITOR
using Unity.Scripting.LifecycleManagement;
using System;
using System.Collections.Generic;
using BovineLabs.Quill;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;

namespace FireAlt.Core.Quill
{
    [NoAutoStaticsCleanup]
    internal static partial class DrawManager
    {
        private static readonly Dictionary<Type, List<IDraw>> Drawers = new(8);
        private static readonly Dictionary<Type, List<IDraw>> SelectedDrawers = new(8);
        
        private static readonly List<IDraw> UninitializedDrawers = new(64);
        private static readonly HashSet<GameObject> SelectedGameObjects = new(16);
        
        [OnCodeInitializing]
        private static void Initialize()
        {
            Selection.selectionChanged += SelectionChanged;
            EditorApplication.update += Update;
        }

        [OnCodeUnloading]
        private static void Unload()
        {
            Selection.selectionChanged -= SelectionChanged;
            EditorApplication.update -= Update;
        }
        
        public static void Register(IDraw drawer)
        {
            AddToMap(drawer, Drawers);
            UninitializedDrawers.Add(drawer);
        }

        private static void SelectionChanged()
        {
            SelectedGameObjects.Clear();
            foreach (var go in Selection.gameObjects)
            {
                SelectedGameObjects.Add(go);
            }

            SelectedDrawers.Clear();
            foreach (var root in SelectedGameObjects)
            {
                AddSelectedDrawers(root);
            }
        }

        private static void AddSelectedDrawers(GameObject root)
        {
            ListPool<IDraw>.Get(out var buffer);
            root.GetComponentsInChildren(false, buffer);
            
            foreach (var drawer in buffer)
            {
                AddToMap(drawer, SelectedDrawers);
            }
            ListPool<IDraw>.Release(buffer);
        }

        private struct FrameMarker
        {
        }

        private static void Update()
        {
            if (!FrameUtility.IsNewFrame<FrameMarker>())
            {
                return;
            }

            // lastActiveSceneView can be null if no SceneView exists
            var lastActiveSceneView = SceneView.lastActiveSceneView;
            if (lastActiveSceneView == null || !lastActiveSceneView.drawGizmos)
            {
                return;
            }
            
            var currentStage = StageUtility.GetCurrentStage();
            var isInNonMainStage = currentStage != StageUtility.GetMainStage();
            var currentStageHandle = currentStage.stageHandle;

            // Check if already selected
            foreach (var drawer in UninitializedDrawers)
            {
                var mb = drawer as MonoBehaviour;
                if (mb == null) continue;
                
                // Initial selection does not trigger selectionChanged event so this has to be done manually
                var go = mb.gameObject;
                if (go == Selection.activeGameObject)
                {
                    AddSelectedDrawers(go);
                }
                
                var transform = mb.transform;
                var isSelected = false;
                while (!isSelected && transform.parent != null)
                {
                    isSelected = SelectedGameObjects.Contains(transform.gameObject);
                    transform = transform.parent;
                }

                if (isSelected)
                {
                    AddToMap(drawer, SelectedDrawers);
                }
            }
            UninitializedDrawers.Clear();
            
            // Clear invalid drawers
            foreach (var (_, drawers) in Drawers)
            {
                for (int i = 0; i < drawers.Count; i++)
                {
                    if (drawers[i] as MonoBehaviour) continue;
                    drawers.RemoveAtSwapBack(i);
                    i--;
                }
            }
            
            // Draw always
            foreach (var (type, drawers) in Drawers)
            {
                if (!CanDrawType(drawers, type)) continue;

                foreach (var drawer in drawers)
                {
                    if (!CanDraw(drawer, isInNonMainStage, currentStageHandle))
                    {
                        continue;
                    }
                    drawer.Draw();
                }
            }
            
            // Draw selected
            foreach (var (type, drawers) in SelectedDrawers)
            {
                if (!CanDrawType(drawers, type)) continue;
                
                foreach (var drawer in drawers)
                {
                    if (!CanDraw(drawer, isInNonMainStage, currentStageHandle))
                    {
                        continue;
                    }
                    drawer.DrawSelected();
                }
            }
        }
        
        private static bool CanDrawType(List<IDraw> drawers, Type type)
        {
            if (drawers.Count == 0) return false;
            
            var enabled = !GizmoUtility.TryGetGizmoInfo(type, out var gizmoInfo) || gizmoInfo.gizmoEnabled;
            return enabled;
        }

        private static bool CanDraw(IDraw drawer, bool isInNonMainStage, StageHandle currentStageHandle)
        {
            var mb = drawer as MonoBehaviour;
            if (mb == null || !mb.isActiveAndEnabled || (mb.hideFlags & HideFlags.HideInHierarchy) != 0)
            {
                return false;
            }

            var disabledDueToIsolationMode = isInNonMainStage && !currentStageHandle.Contains(mb.gameObject);
            return !disabledDueToIsolationMode;
        }
        
        private static void AddToMap(IDraw drawer, Dictionary<Type, List<IDraw>> map)
        {
            var type = drawer.GetType();
            
            if (!map.ContainsKey(type))
            {
                map.Add(type, new List<IDraw>());
            }

            map[type].Add(drawer);
        }
    }
}
#endif
