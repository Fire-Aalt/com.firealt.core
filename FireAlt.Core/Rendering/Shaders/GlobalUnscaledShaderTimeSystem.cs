using FireAlt.Core.Utility;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace FireAlt.Core.Rendering
{
    [NoAutoStaticsCleanup]
    public static partial class GlobalUnscaledShaderTimeSystem
    {
        private static readonly int UnscaledTime = Shader.PropertyToID("UnscaledTime");
        
        [OnCodeLoaded]
        private static void Initialize()
        {
            PlayerLoopUtils.AddPlayerLoopSystem<Update>(typeof(GlobalUnscaledShaderTimeSystem), UpdateGlobalUnscaledShaderTime);
        }

        private static void UpdateGlobalUnscaledShaderTime()
        {
            Shader.SetGlobalFloat(UnscaledTime, Time.unscaledTime);
        }
    }
}
