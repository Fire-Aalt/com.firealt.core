using FireAlt.Core.Utility;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace FireAlt.Core.Timers
{
    internal static partial class TimerBootstrapper
    {
        [OnEnteringPlayMode]
        internal static void Initialize()
        {
            PlayerLoopUtils.AddRuntimePlayerLoopSystem<Update>(typeof(TimerManager), TimerManager.UpdateTimers,
                TimerManager.Clear);
        }
    }
}
