using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;

namespace FireAlt.Core.Timers
{
    [NoAutoStaticsCleanup]
    public static class TimerManager
    {
        private static readonly HashSet<Timer> Timers = new();

        public static void RegisterTimer(Timer timer) => Timers.Add(timer);
        public static void DeregisterTimer(Timer timer) => Timers.Remove(timer);

        public static void UpdateTimers()
        {
            if (Timers.Count == 0) return;

            foreach (var timer in Timers)
            {
                timer.Tick();
            }
        }

        public static void Clear()
        {
            foreach (var timer in Timers.ToList())
            {
                timer.Dispose();
            }
            Timers.Clear();
        }
    }
}
