using UnityEngine;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Jobs;

namespace FireAlt.Core
{
    [NoAutoStaticsCleanup]
    public static class HybridEntityUtils
    {
        private static readonly bool ShowRuntime;
        
        static HybridEntityUtils()
        {
#if UNITY_EDITOR
            ShowRuntime = UnityEditor.EditorPrefs.GetBool("Unity.Entities.Streaming.SubScene.LiveConversionSceneViewShowRuntime", false);
#endif
        }
        
        public static bool IsEntityEnabled(MonoBehaviour mb)
        {
            if (!Application.isPlaying && InSubScene(mb))
            {
                return mb.isActiveAndEnabled && !ShowRuntime;
            }
            return mb.isActiveAndEnabled;
        }

#if UNITY_EDITOR
        public static bool InPrefabStage(MonoBehaviour component) => UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(component.gameObject.scene);
#endif
        
        public static bool IsNonUniformScale(Transform transform)
        {
            var localScale = transform.localScale;
            return !Mathf.Approximately(localScale.x, localScale.y) || !Mathf.Approximately(localScale.y, localScale.z);
        }
        
        public static bool IsNonUniformScale(TransformAccess transform)
        {
            var localScale = transform.localScale;
            return !Mathf.Approximately(localScale.x, localScale.y) || !Mathf.Approximately(localScale.y, localScale.z);
        }
        
        private static bool InSubScene(MonoBehaviour component) => component.gameObject.scene.isSubScene;
    }
}
