using UnityEngine;

namespace NightDash.Runtime
{
    public static class NightDashLog
    {
        public static void Info(string message)
        {
            if (NightDashRuntimeToggles.VerboseRuntimeLogs || Debug.isDebugBuild)
            {
                Debug.Log(message);
            }
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            Debug.LogError(message);
        }
    }
}
